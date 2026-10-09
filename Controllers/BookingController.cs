using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using Hotel_Management_Website.Filters;
using Hotel_Management_Website.IServices;
using Hotel_Management_Website.Models.ViewModels.Booking;
using Hotel_Management_Website.Models.ViewModels.UI;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.AspNetCore.Mvc;

namespace Hotel_Management_Website.Controllers;

[ReceptionAccess]
public sealed class BookingController : Controller
{
    private readonly IBookingService _bookingService;
    private readonly ILogger<BookingController> _logger;
    private readonly IDistributedCache _distributedCache;

    public BookingController(
        IBookingService bookingService,
        ILogger<BookingController> logger,
        IDistributedCache distributedCache)
    {
        _bookingService = bookingService;
        _logger = logger;
        _distributedCache = distributedCache;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        BookingIndexQueryViewModel query,
        CancellationToken cancellationToken)
    {
        query ??= new BookingIndexQueryViewModel();
        query.Filter ??= new BookingListFilterViewModel();

        if (string.Equals(query.Reset, "booking", StringComparison.OrdinalIgnoreCase))
        {
            query.Filter = new BookingListFilterViewModel();
        }

        try
        {
            var model = await _bookingService.GetIndexAsync(
                query.Filter,
                cancellationToken);

            ApplyPaginationUrl(model);
            return View(model);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi tải danh sách đặt phòng.");

            return View(new BookingIndexViewModel
            {
                Filter = new BookingListFilterViewModel
                {
                    CurrentPage = 1,
                    PageSize = 10
                },
                Bookings = Array.Empty<BookingListItemViewModel>(),
                Pagination = new PaginationViewModel
                {
                    CurrentPage = 1,
                    PageSize = 10,
                    TotalItems = 0,
                    PageParam = "Filter.CurrentPage",
                    PageSizeParam = "Filter.PageSize",
                    BaseUrl = "/Booking/Index",
                    ItemLabel = "đặt phòng"
                },
                LoadError = "Không thể tải danh sách đặt phòng. Vui lòng thử lại."
            });
        }
    }

    [HttpGet]
    public async Task<IActionResult> Details(
        Guid id,
        CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
        {
            return NotFound();
        }

        try
        {
            var model = await _bookingService.GetDetailsAsync(id, cancellationToken);

            if (model is null)
            {
                return NotFound();
            }

            return View(model);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi tải chi tiết đặt phòng {BookingId}.", id);
            TempData["ErrorMessage"] = "Không thể tải chi tiết đặt phòng. Vui lòng thử lại.";
            return RedirectToAction(nameof(Index));
        }
    }

    [ReceptionOnlyAccess]
    [HttpGet]
    public async Task<IActionResult> Edit(
        Guid id,
        CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
        {
            return NotFound();
        }

        try
        {
            var model = await _bookingService.GetEditModelAsync(id, cancellationToken);
            if (model is null)
            {
                return NotFound();
            }

            return View(model);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi mở chỉnh sửa đặt phòng {BookingId}.", id);
            TempData["ErrorMessage"] = "Không thể mở chức năng chỉnh sửa lịch đặt phòng. Vui lòng thử lại.";
            return RedirectToAction(nameof(Index));
        }
    }

    [ReceptionOnlyAccess]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        BookingEditViewModel model,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Challenge();
        }

        if (!ModelState.IsValid)
        {
            var rebuilt = await _bookingService.GetEditModelAsync(model?.BookingId ?? Guid.Empty, cancellationToken);
            if (rebuilt is not null)
            {
                CopyEditPostedFields(rebuilt, model);
                return View(rebuilt);
            }

            TempData["ErrorMessage"] = "Lịch đặt phòng không còn tồn tại hoặc đã bị thay đổi. Vui lòng mở lại danh sách đặt phòng.";
            return RedirectToAction(nameof(Index));
        }

        var result = await _bookingService.UpdateEditAsync(
            model,
            currentUserId,
            cancellationToken);

        if (!result.Succeeded)
        {
            ModelState.Clear();
            ApplyFieldErrors(result.FieldErrors);

            if (!result.FieldErrors.ContainsKey(string.Empty))
            {
                ModelState.AddModelError(
                    string.Empty,
                    result.ErrorMessage ?? "Không thể cập nhật lịch đặt phòng.");
            }

            var rebuilt = await _bookingService.GetEditModelAsync(model.BookingId, cancellationToken);
            if (rebuilt is not null)
            {
                CopyEditPostedFields(rebuilt, model);
                return View(rebuilt);
            }

            TempData["ErrorMessage"] = result.ErrorMessage ?? "Lịch đặt phòng không còn tồn tại hoặc đã bị thay đổi.";
            return RedirectToAction(nameof(Index));
        }

        TempData["SuccessMessage"] = $"Đã cập nhật lịch đặt phòng {model.BookingCode} thành công.";
        return RedirectToAction(nameof(Index));
    }

    [ReceptionOnlyAccess]
    [HttpGet]
    public async Task<IActionResult> Create(
        string? draftId,
        int? step,
        byte? bookingType,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!bookingType.HasValue)
            {
                return View("CreateType");
            }

            if (bookingType.Value == 1)
            {
                if (!string.IsNullOrWhiteSpace(draftId) || step is not null)
                {
                    return RedirectToAction(nameof(Create), new { bookingType = 1 });
                }

                var directModel = await _bookingService.GetDirectCreateModelAsync(cancellationToken);
                return View("CreateDirect", directModel);
            }

            if (bookingType.Value != 0)
            {
                TempData["ErrorMessage"] = "Loại tạo lịch đặt phòng không hợp lệ.";
                return RedirectToAction(nameof(Create));
            }

            var requestedStep = step is 2 ? 2 : 1;

            // Chỉ tiếp tục một draft khi draftId được truyền tường minh qua URL.
            // Không lấy draftId từ cookie khi người dùng mở mới /Booking/Create;
            // như vậy mỗi lần đi từ danh sách hoặc menu Tạo lịch đặt phòng đều là
            // một phiên tạo mới, không khôi phục dữ liệu cũ ngoài ý muốn.
            var normalizedDraftId = NormalizeDraftId(draftId);

            if (!TryGetCurrentUserId(out var currentUserId))
            {
                return Challenge();
            }

            if (normalizedDraftId is not null)
            {
                var step1Draft = await LoadStep1DraftAsync(
                    currentUserId,
                    normalizedDraftId,
                    cancellationToken);

                if (step1Draft is not null)
                {
                    step1Draft.DraftId = normalizedDraftId;
                    var refreshedStep1 = await _bookingService.RefreshCreateStep1Async(
                        step1Draft,
                        cancellationToken);
                    refreshedStep1.DraftId = normalizedDraftId;

                    await SaveStep1DraftAsync(
                        currentUserId,
                        normalizedDraftId,
                        refreshedStep1,
                        cancellationToken);

                    if (requestedStep == 2)
                    {
                        refreshedStep1.Step2StartedAt = TruncateToMinute(DateTime.Now);
                        await SaveStep1DraftAsync(
                            currentUserId,
                            normalizedDraftId,
                            refreshedStep1,
                            cancellationToken);

                        var step2Model = await _bookingService.GetCreateStep2ModelAsync(
                            refreshedStep1,
                            normalizedDraftId,
                            cancellationToken);
                        step2Model.Step2StartedAt = refreshedStep1.Step2StartedAt;

                        return View(
                            "Create",
                            MapStep2ToViewModel(step2Model));
                    }

                    return View(
                        "Create",
                        MapStep1ToViewModel(refreshedStep1, normalizedDraftId));
                }

                if (requestedStep == 2)
                {
                    TempData["ErrorMessage"] =
                        "Phiên tạo lịch đặt phòng đã hết hạn. Vui lòng nhập lại bước 1.";
                }
            }
            else
            {
                // Mở /Booking/Create mà không có draftId = bắt đầu một phiên mới.
                // Xóa cookie draft cũ để lần POST đầu tiên không vô tình nối vào draft trước.
                ClearCurrentBookingDraftCookie();

                if (requestedStep == 2)
                {
                    TempData["ErrorMessage"] =
                        "Phiên tạo lịch đặt phòng không hợp lệ. Vui lòng thực hiện lại bước 1.";
                }
            }

            var step1Model = await _bookingService.GetCreateStep1ModelAsync(
                cancellationToken);

            return View(
                "Create",
                MapStep1ToViewModel(step1Model, null));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi mở form tạo đặt phòng.");
            TempData["ErrorMessage"] =
                "Không thể mở chức năng tạo lịch đặt phòng. Vui lòng thử lại.";
            return RedirectToAction(nameof(Index));
        }
    }

    [ReceptionOnlyAccess]
    [HttpGet]
    public async Task<IActionResult> CancelCreate(
        string? draftId,
        CancellationToken cancellationToken)
    {
        try
        {
            if (TryGetCurrentUserId(out var currentUserId))
            {
                var normalizedDraftId = NormalizeDraftId(draftId);

                if (normalizedDraftId is null)
                {
                    normalizedDraftId = NormalizeDraftId(
                        Request.Cookies[BookingDraftCookieName]);
                }

                if (normalizedDraftId is not null)
                {
                    await RemoveStep1DraftAsync(
                        currentUserId,
                        normalizedDraftId,
                        cancellationToken);
                }
            }

            ClearCurrentBookingDraftCookie();
            return RedirectToAction(nameof(Index));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Không thể xóa draft tạo Booking khi hủy.");
            ClearCurrentBookingDraftCookie();
            return RedirectToAction(nameof(Index));
        }
    }

    [ReceptionOnlyAccess]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RefreshCreate(
        BookingCreateStep1ViewModel model,
        CancellationToken cancellationToken)
    {
        model ??= new BookingCreateStep1ViewModel();

        try
        {
            if (!TryGetCurrentUserId(out var currentUserId))
            {
                return Challenge();
            }

            var bindingErrors = GetModelStateErrors();

            // Không làm mới danh sách phòng khi hai ngày lưu trú chưa hợp lệ.
            // Bước này phải chặn ở server để request thủ công cũng không thể
            // bỏ qua validation phía trình duyệt.
            var dateErrors = new Dictionary<string, string>();
            var today = DateTime.Today;

            if (model.CheckInDate == default)
            {
                dateErrors[nameof(BookingCreateStep1ViewModel.CheckInDate)] =
                    "Vui lòng chọn ngày check in dự kiến.";
            }
            else if (model.CheckInDate.Date < today)
            {
                dateErrors[nameof(BookingCreateStep1ViewModel.CheckInDate)] =
                    "Ngày check in dự kiến không được trước ngày hiện tại.";
            }

            if (model.CheckOutDate == default)
            {
                dateErrors[nameof(BookingCreateStep1ViewModel.CheckOutDate)] =
                    "Vui lòng chọn ngày check out dự kiến.";
            }
            else if (model.CheckInDate != default && model.CheckOutDate.Date <= model.CheckInDate.Date)
            {
                dateErrors[nameof(BookingCreateStep1ViewModel.CheckOutDate)] =
                    "Ngày check out phải sau ngày check in.";
            }

            if (bindingErrors.Count > 0 || dateErrors.Count > 0)
            {
                ModelState.Clear();
                ApplyFieldErrors(bindingErrors);
                ApplyFieldErrors(dateErrors);

                // Không gọi RefreshCreateStep1Async khi ngày chưa hợp lệ.
                var fallback = await _bookingService.GetCreateStep1ModelAsync(cancellationToken);
                fallback.CheckInDate = model.CheckInDate == default ? fallback.CheckInDate : model.CheckInDate;
                fallback.CheckOutDate = model.CheckOutDate == default ? fallback.CheckOutDate : model.CheckOutDate;
                fallback.TotalAdults = Math.Max(0, model.TotalAdults);
                fallback.TotalChildren = Math.Max(0, model.TotalChildren);
                fallback.DraftId = ResolveDraftId(model.DraftId);

                var postedByRoomType = (model.RoomSelections ?? new List<BookingRoomSelectionViewModel>())
                    .GroupBy(x => x.RoomTypeId)
                    .ToDictionary(x => x.Key, x => x.First().Quantity);

                foreach (var row in fallback.RoomSelections)
                {
                    if (postedByRoomType.TryGetValue(row.RoomTypeId, out var quantity))
                    {
                        row.Quantity = Math.Clamp(quantity, 0, 999);
                    }
                }

                return View(
                    "Create",
                    MapStep1ToViewModel(fallback, fallback.DraftId));
            }

            var refreshed = await _bookingService.RefreshCreateStep1Async(
                model,
                cancellationToken);

            var draftId = ResolveDraftId(model.DraftId);
            refreshed.DraftId = draftId;

            if (!string.IsNullOrWhiteSpace(draftId))
            {
                await SaveStep1DraftAsync(
                    currentUserId,
                    draftId,
                    refreshed,
                    cancellationToken);
                SetCurrentBookingDraftCookie(draftId);
            }

            ModelState.Clear();
            ApplyFieldErrors(bindingErrors);

            return View(
                "Create",
                MapStep1ToViewModel(refreshed, draftId));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi làm mới danh sách loại phòng theo thời gian lưu trú.");
            ModelState.Clear();
            ModelState.AddModelError(
                string.Empty,
                "Không thể làm mới danh sách loại phòng. Vui lòng thử lại.");

            var fallback = await _bookingService.GetCreateStep1ModelAsync(
                cancellationToken);

            fallback.CheckInDate = model.CheckInDate == default
                ? fallback.CheckInDate
                : model.CheckInDate;
            fallback.CheckOutDate = model.CheckOutDate == default
                ? fallback.CheckOutDate
                : model.CheckOutDate;
            fallback.TotalAdults = model.TotalAdults;
            fallback.TotalChildren = Math.Max(0, model.TotalChildren);
            fallback.DraftId = ResolveDraftId(model.DraftId);

            return View(
                "Create",
                MapStep1ToViewModel(fallback, fallback.DraftId));
        }
    }

    [ReceptionOnlyAccess]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PrepareCreate(
        BookingCreateStep1ViewModel model,
        CancellationToken cancellationToken)
    {
        model ??= new BookingCreateStep1ViewModel();

        try
        {
            var result = await _bookingService.ValidateCreateStep1Async(
                model,
                cancellationToken);

            var bindingErrors = GetModelStateErrors();

            if (!result.Succeeded || bindingErrors.Count > 0)
            {
                ModelState.Clear();
                ApplyFieldErrors(bindingErrors);
                ApplyFieldErrors(result.FieldErrors);

                if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
                {
                    ModelState.AddModelError(string.Empty, result.ErrorMessage);
                }

                result.Model.DraftId = NormalizeDraftId(model.DraftId);
                return View(
                    "Create",
                    MapStep1ToViewModel(result.Model, result.Model.DraftId));
            }

            var draftId = ResolveDraftId(model.DraftId)
                ?? Guid.NewGuid().ToString("N");

            if (!TryGetCurrentUserId(out var currentUserId))
            {
                return Challenge();
            }

            result.Model.DraftId = draftId;
            await SaveStep1DraftAsync(
                currentUserId,
                draftId,
                result.Model,
                cancellationToken);

            SetCurrentBookingDraftCookie(draftId);

            return RedirectToAction(
                nameof(Create),
                new
                {
                    bookingType = 0,
                    step = 2,
                    draftId
                });
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi kiểm tra khả năng tạo lịch đặt phòng.");
            ModelState.Clear();
            ModelState.AddModelError(
                string.Empty,
                "Không thể kiểm tra khả năng đặt phòng. Vui lòng thử lại.");

            try
            {
                var rebuilt = await _bookingService.GetCreateStep1ModelAsync(
                    cancellationToken);

                rebuilt.CheckInDate = model.CheckInDate == default
                    ? rebuilt.CheckInDate
                    : model.CheckInDate;
                rebuilt.CheckOutDate = model.CheckOutDate == default
                    ? rebuilt.CheckOutDate
                    : model.CheckOutDate;
                rebuilt.TotalAdults = Math.Max(0, model.TotalAdults);
                rebuilt.TotalChildren = Math.Max(0, model.TotalChildren);

                var postedByRoomType = (model.RoomSelections ?? new List<BookingRoomSelectionViewModel>())
                    .GroupBy(x => x.RoomTypeId)
                    .ToDictionary(x => x.Key, x => x.First());

                foreach (var row in rebuilt.RoomSelections)
                {
                    if (postedByRoomType.TryGetValue(row.RoomTypeId, out var posted))
                    {
                        row.Quantity = Math.Clamp(posted.Quantity, 0, 999);
                    }
                }

                return View(
                    "Create",
                    MapStep1ToViewModel(rebuilt, null));
            }
            catch
            {
                return View(
                    "Create",
                    MapStep1ToViewModel(model, null));
            }
        }
    }

    [ReceptionOnlyAccess]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        BookingCreateStep2ViewModel model,
        string? draftId,
        CancellationToken cancellationToken)
    {
        model ??= new BookingCreateStep2ViewModel();

        // Action này chỉ dùng cho luồng Đặt trước. Trực tiếp tại quầy dùng CreateDirect.
        model.BookingType = 0;

        var normalizedDraftId = ResolveDraftId(
            draftId,
            model.DraftId,
            HttpContext.Request.Query["draftId"].FirstOrDefault(),
            HttpContext.Request.Form["DraftId"].FirstOrDefault());

        if (normalizedDraftId is null)
        {
            TempData["ErrorMessage"] =
                "Phiên tạo lịch đặt phòng không hợp lệ hoặc đã hết hạn.";
            return RedirectToAction(nameof(Create));
        }

        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Challenge();
        }

        var step1 = await LoadStep1DraftAsync(
            currentUserId,
            normalizedDraftId,
            cancellationToken);

        if (step1 is null)
        {
            TempData["ErrorMessage"] =
                "Phiên tạo lịch đặt phòng đã hết hạn. Vui lòng thực hiện lại bước 1.";
            return RedirectToAction(nameof(Create));
        }

        step1.DraftId = normalizedDraftId;
        if (!step1.Step2StartedAt.HasValue)
        {
            step1.Step2StartedAt = TruncateToMinute(DateTime.Now);
            await SaveStep1DraftAsync(currentUserId, normalizedDraftId, step1, cancellationToken);
        }
        model.DraftId = normalizedDraftId;
        model.Step2StartedAt = step1.Step2StartedAt;

        // Step 2 basic ModelState validation always comes first.
        if (!ModelState.IsValid)
        {
            try
            {
                var rebuilt = await _bookingService.GetCreateStep2ModelAsync(
                    step1,
                    normalizedDraftId,
                    cancellationToken);
                CopyPostedStep2Fields(rebuilt, model);
                return View(
                    "Create",
                    MapStep2ToViewModel(rebuilt));
            }
            catch
            {
                return View(
                    "Create",
                    MapStep2ToViewModel(model));
            }
        }

        try
        {
            var result = await _bookingService.CreateAsync(
                step1,
                model,
                currentUserId,
                cancellationToken);

            ApplyFieldErrors(result.FieldErrors);

            if (!result.Succeeded)
            {
                if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
                {
                    ModelState.AddModelError(string.Empty, result.ErrorMessage);
                }

                return View(
                    "Create",
                    MapStep2ToViewModel(result.Model));
            }

            await RemoveStep1DraftAsync(
                currentUserId,
                normalizedDraftId,
                cancellationToken);

            ClearCurrentBookingDraftCookie();

            if (result.Model.BookingId != Guid.Empty && result.Model.BookingType == 1)
            {
                TempData["SuccessMessage"] =
                    $"Đã tạo lịch đặt phòng {result.Model.BookingCode}. Lịch trực tiếp tại quầy đã chuyển sang bước check in.";

                return RedirectToAction(nameof(CheckIn), new { id = result.Model.BookingId });
            }

            TempData["SuccessMessage"] =
                $"Đã tạo lịch đặt phòng {result.Model.BookingCode} thành công.";

            return RedirectToAction(nameof(Index));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi tạo đặt phòng.");
            ModelState.AddModelError(
                string.Empty,
                "Không thể tạo lịch đặt phòng. Vui lòng kiểm tra lại thông tin và thử lại.");

            if (model.Guests.Count == 0)
            {
                try
                {
                    var rebuilt = await _bookingService.GetCreateStep2ModelAsync(
                        step1,
                        normalizedDraftId,
                        cancellationToken);
                    CopyMissingStep2Fields(model, rebuilt);
                    return View(
                        "Create",
                        MapStep2ToViewModel(rebuilt));
                }
                catch
                {
                    // Keep the posted Step 2 model when reference data cannot be rebuilt.
                }
            }

            return View(
                "Create",
                MapStep2ToViewModel(model));
        }
    }

    [ReceptionOnlyAccess]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateDirect(
        BookingDirectCreateViewModel model,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Challenge();
        }

        // Check-in trực tiếp luôn bắt đầu trong ngày hiện tại; không nhận ngày check-in từ client.
        ModelState.Remove(nameof(BookingDirectCreateViewModel.CheckInDate));
        ModelState.Remove(nameof(BookingDirectCreateViewModel.ExpectedCheckInAt));
        ModelState.Remove(nameof(BookingDirectCreateViewModel.ExpectedCheckOutAt));

        if (!model.FinalConfirmation)
        {
            ModelState.AddModelError(string.Empty, "Vui lòng xác nhận lần cuối trước khi tạo và check in.");
        }

        if (!ModelState.IsValid)
        {
            model.FinalConfirmation = false;
            var rebuilt = await _bookingService.RefreshDirectCreateModelAsync(model, cancellationToken);
            rebuilt.FinalConfirmation = false;
            return View("CreateDirect", rebuilt);
        }

        var result = await _bookingService.CreateDirectAsync(
            model,
            currentUserId,
            cancellationToken);

        if (!result.Succeeded)
        {
            result.Model.FinalConfirmation = false;
            ModelState.Clear();
            ApplyFieldErrors(result.FieldErrors);

            var message = result.ErrorMessage;
            if (!string.IsNullOrWhiteSpace(message) && !result.FieldErrors.ContainsKey(string.Empty))
            {
                ModelState.AddModelError(string.Empty, message);
            }

            return View("CreateDirect", result.Model);
        }

        TempData["SuccessMessage"] =
            $"Đã tạo và check in trực tiếp lịch đặt phòng {result.Model.BookingCode} thành công. Booking đang ở trạng thái Đang ở.";

        return RedirectToAction(nameof(Index));
    }

    [ReceptionOnlyAccess]
    [HttpGet]
    public async Task<IActionResult> GetNextBookingCode(CancellationToken cancellationToken)
    {
        var code = await _bookingService.GetNextBookingCodeAsync(cancellationToken);
        return Json(new { code });
    }

    [ReceptionOnlyAccess]
    [HttpGet]
    public async Task<IActionResult> GetNextGuestCode(CancellationToken cancellationToken)
    {
        var code = await _bookingService.GetNextGuestCodeAsync(cancellationToken);
        return Json(new { code });
    }

    [ReceptionOnlyAccess]
    [HttpGet]
    public async Task<IActionResult> Flow(
        Guid id,
        CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
            return NotFound();

        var model = await _bookingService.GetFlowAsync(id, cancellationToken);
        return model is null ? NotFound() : View(model);
    }

    [AdminAccess]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(
        Guid id,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Challenge();
        }

        if (id == Guid.Empty)
        {
            TempData["ErrorMessage"] = "Lịch đặt phòng không hợp lệ.";
            return RedirectToAction(nameof(Index));
        }

        var result = await _bookingService.DeleteAsync(id, currentUserId, cancellationToken);
        if (!result.Succeeded)
        {
            TempData["ErrorMessage"] = result.ErrorMessage ?? "Không thể xóa lịch đặt phòng.";
            return RedirectToAction(nameof(Index));
        }

        TempData["SuccessMessage"] = "Đã xóa lịch đặt phòng và toàn bộ dữ liệu khách hàng, phòng đặt, dịch vụ/phí phát sinh liên quan.";
        return RedirectToAction(nameof(Index));
    }

    [ReceptionOnlyAccess]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Confirm(
        BookingConfirmViewModel model,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
            return Challenge();

        var result = await _bookingService.ConfirmAsync(model, currentUserId, cancellationToken);
        if (!result.Succeeded)
        {
            TempData["ErrorMessage"] = result.ErrorMessage ?? "Không thể xác nhận lịch đặt phòng.";
            return RedirectToAction(nameof(Flow), new { id = model?.BookingId });
        }

        TempData["SuccessMessage"] = "Đã xác nhận lịch đặt phòng thành công.";

        return RedirectToAction(nameof(Flow), new { id = model.BookingId });
    }

    [ReceptionOnlyAccess]
    [HttpGet]
    public async Task<IActionResult> CheckIn(
        Guid id,
        CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
            return NotFound();

        var model = await _bookingService.GetCheckInModelAsync(id, cancellationToken);
        if (model is null)
        {
            TempData["ErrorMessage"] = "Chỉ có lịch đặt phòng ở trạng thái Chờ check in mới được check in.";
            return RedirectToAction(nameof(Flow), new { id });
        }

        return View(model);
    }

    [ReceptionOnlyAccess]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CheckIn(
        BookingCheckInViewModel model,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
            return Challenge();

        if (!ModelState.IsValid)
        {
            // Validate the guest section first. If any guest annotation/format/required
            // validation fails, do not surface room-allocation errors in the same round.
            if (HasCheckInGuestModelStateErrors())
            {
                foreach (var key in ModelState.Keys
                    .Where(k => k == nameof(BookingCheckInViewModel.TotalAdults)
                        || k == nameof(BookingCheckInViewModel.TotalChildren)
                        || k == nameof(BookingCheckInViewModel.BookingNote)
                        || k.StartsWith("Rooms[", StringComparison.OrdinalIgnoreCase))
                    .ToList())
                {
                    ModelState.Remove(key);
                }
            }

            var rebuilt = await _bookingService.GetCheckInModelAsync(model.BookingId, cancellationToken);
            if (rebuilt is not null)
            {
                CopyCheckInPostedFields(rebuilt, model);
                return View(rebuilt);
            }

            TempData["ErrorMessage"] = "Lịch đặt phòng không còn ở trạng thái Chờ check in hoặc đã bị thay đổi. Vui lòng mở lại quản lý luồng.";
            return RedirectToAction(nameof(Flow), new { id = model.BookingId });
        }

        var result = await _bookingService.CheckInAsync(model, currentUserId, cancellationToken);
        if (!result.Succeeded)
        {
            ModelState.Clear();
            ApplyFieldErrors(result.FieldErrors);
            if (!result.FieldErrors.ContainsKey(string.Empty))
            {
                ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Không thể thực hiện check in.");
            }

            var rebuilt = await _bookingService.GetCheckInModelAsync(model.BookingId, cancellationToken);
            if (rebuilt is not null)
            {
                CopyCheckInPostedFields(rebuilt, model);
                return View(rebuilt);
            }

            return RedirectToAction(nameof(Flow), new { id = model.BookingId });
        }

        TempData["SuccessMessage"] = "Đã check in khách hàng thành công. Booking đã chuyển sang trạng thái Đang ở.";
        return RedirectToAction(nameof(Flow), new { id = model.BookingId });
    }

    [ReceptionOnlyAccess]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(
        BookingCancelViewModel model,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
            return Challenge();

        var result = await _bookingService.CancelBeforeCheckInAsync(model, currentUserId, cancellationToken);
        if (!result.Succeeded)
        {
            TempData["ErrorMessage"] = result.ErrorMessage ?? "Không thể hủy lịch đặt phòng.";
            return RedirectToAction(nameof(Flow), new { id = model?.BookingId });
        }

        TempData["SuccessMessage"] = model.CancelStatus switch
        {
            4 => "Đã cập nhật Booking thành Xác nhận thất bại.",
            5 => "Đã cập nhật Booking thành Check in thất bại.",
            7 => "Đã cập nhật Booking thành Khách hủy trước check in.",
            8 => "Đã cập nhật Booking thành Vắng mặt không thông báo/liên lạc được.",
            9 => "Đã cập nhật Booking thành Khách sạn hủy.",
            _ => "Đã cập nhật trạng thái Booking."
        };

        return RedirectToAction(nameof(Flow), new { id = model.BookingId });
    }

    [ReceptionOnlyAccess]
    [HttpGet]
    public async Task<IActionResult> PaymentRecovery(
        Guid id,
        CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
            return NotFound();

        var model = await _bookingService.GetPaymentRecoveryModelAsync(id, cancellationToken);
        if (model is null)
        {
            TempData["ErrorMessage"] = "Chỉ có lịch đặt phòng ở trạng thái Check out thất bại và có hóa đơn Thanh toán thất bại mới được tiếp nhận thanh toán lại.";
            return RedirectToAction(nameof(Flow), new { id });
        }

        return View(model);
    }

    [ReceptionOnlyAccess]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PaymentRecovery(
        BookingPaymentRecoveryViewModel model,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
            return Challenge();

        if (!ModelState.IsValid)
        {
            var rebuilt = await _bookingService.GetPaymentRecoveryModelAsync(model.BookingId, cancellationToken);
            if (rebuilt is not null)
            {
                CopyPaymentRecoveryPostedFields(rebuilt, model);
                return View(rebuilt);
            }

            TempData["ErrorMessage"] = "Lịch đặt phòng không còn ở trạng thái Check out thất bại hoặc hóa đơn đã được xử lý.";
            return RedirectToAction(nameof(Flow), new { id = model.BookingId });
        }

        var result = await _bookingService.CompletePaymentRecoveryAsync(model, currentUserId, cancellationToken);
        if (!result.Succeeded)
        {
            ModelState.Clear();
            ApplyFieldErrors(result.FieldErrors);
            if (!result.FieldErrors.ContainsKey(string.Empty))
                ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Không thể ghi nhận thanh toán lại.");

            var rebuilt = await _bookingService.GetPaymentRecoveryModelAsync(model.BookingId, cancellationToken);
            if (rebuilt is not null)
            {
                CopyPaymentRecoveryPostedFields(rebuilt, model);
                return View(rebuilt);
            }

            return RedirectToAction(nameof(Flow), new { id = model.BookingId });
        }

        TempData["SuccessMessage"] = "Đã ghi nhận thanh toán lại thành công. Booking đã chuyển từ Check out thất bại sang Đã check out.";
        return RedirectToAction(nameof(Flow), new { id = model.BookingId });
    }

    [ReceptionOnlyAccess]
    [HttpGet]
    public async Task<IActionResult> CheckOut(
        Guid id,
        CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
            return NotFound();

        var model = await _bookingService.GetCheckOutModelAsync(id, cancellationToken);
        if (model is null)
        {
            TempData["ErrorMessage"] = "Chỉ có lịch đặt phòng ở trạng thái Đang ở mới được check out.";
            return RedirectToAction(nameof(Flow), new { id });
        }

        return View(model);
    }

    [ReceptionOnlyAccess]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CheckOut(
        BookingCheckOutViewModel model,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
            return Challenge();

        if (!ModelState.IsValid)
        {
            var rebuilt = await _bookingService.GetCheckOutModelAsync(model.BookingId, cancellationToken);
            if (rebuilt is not null)
            {
                CopyCheckOutPostedFields(rebuilt, model);
                return View(rebuilt);
            }

            TempData["ErrorMessage"] = "Lịch đặt phòng không còn ở trạng thái Đang ở hoặc đã bị thay đổi. Vui lòng mở lại quản lý luồng.";
            return RedirectToAction(nameof(Flow), new { id = model.BookingId });
        }

        var result = await _bookingService.CheckOutAsync(model, currentUserId, cancellationToken);
        if (!result.Succeeded)
        {
            ModelState.Clear();
            ApplyFieldErrors(result.FieldErrors);
            if (!result.FieldErrors.ContainsKey(string.Empty))
            {
                ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Không thể thực hiện check out.");
            }

            var rebuilt = await _bookingService.GetCheckOutModelAsync(model.BookingId, cancellationToken);
            if (rebuilt is not null)
            {
                CopyCheckOutPostedFields(rebuilt, model);
                return View(rebuilt);
            }

            return RedirectToAction(nameof(Flow), new { id = model.BookingId });
        }

        TempData["SuccessMessage"] = model.PaymentReceived
            ? "Đã check out và ghi nhận thanh toán thành công."
            : "Đã ghi nhận check out thất bại và tạo hóa đơn ở trạng thái thanh toán thất bại.";

        return RedirectToAction(nameof(Flow), new { id = model.BookingId });
    }

    private static void CopyEditPostedFields(
        BookingEditViewModel target,
        BookingEditViewModel? source)
    {
        if (source is null)
        {
            return;
        }

        target.TotalAdults = source.TotalAdults;
        target.TotalChildren = source.TotalChildren;
        target.Note = source.Note;

        if (target.CanEditGuestDetails)
        {
            target.Guest.GuestCode = source.Guest?.GuestCode ?? target.Guest.GuestCode;
            target.Guest.Name = source.Guest?.Name ?? target.Guest.Name;
            target.Guest.DateOfBirth = source.Guest?.DateOfBirth;
            target.Guest.Sex = source.Guest?.Sex;
            target.Guest.Address = source.Guest?.Address;
            target.Guest.PhoneNumber = source.Guest?.PhoneNumber;
            target.Guest.Email = source.Guest?.Email;
            target.Guest.IdentityNumber = source.Guest?.IdentityNumber;
            target.Guest.IdentityType = source.Guest?.IdentityType;
            target.Guest.Note = source.Guest?.Note;
            target.Guest.SexName = source.Guest?.Sex is 0 ? "Nam"
                : source.Guest?.Sex is 1 ? "Nữ"
                : source.Guest?.Sex is 2 ? "Khác"
                : "Không xác định";
            target.Guest.IdentityTypeName = source.Guest?.IdentityType is 0 ? "CCCD"
                : source.Guest?.IdentityType is 1 ? "Giấy phép lái xe"
                : source.Guest?.IdentityType is 2 ? "Hộ chiếu"
                : "Không xác định";
        }
        else
        {
            target.Guest.Note = source.Guest?.Note ?? target.Guest.Note;
        }

        if (source.Rooms is null)
        {
            return;
        }

        var sourceById = source.Rooms
            .Where(x => x is not null && x.BookingRoomId != Guid.Empty)
            .ToDictionary(x => x.BookingRoomId);

        foreach (var room in target.Rooms)
        {
            if (!sourceById.TryGetValue(room.BookingRoomId, out var postedRoom))
            {
                continue;
            }

            room.Note = postedRoom.Note;
            room.ServiceInputs = postedRoom.ServiceInputs ?? new List<BookingEditServiceItemViewModel>();
            room.FeeInputs = postedRoom.FeeInputs ?? new List<BookingEditFeeItemViewModel>();
        }
    }

    private static void CopyPaymentRecoveryPostedFields(
        BookingPaymentRecoveryViewModel target,
        BookingPaymentRecoveryViewModel source)
    {
        target.GuestCode = source.GuestCode;
        target.GuestName = source.GuestName;
        target.GuestDateOfBirth = source.GuestDateOfBirth;
        target.GuestSex = source.GuestSex;
        target.GuestAddress = source.GuestAddress;
        target.GuestPhoneNumber = source.GuestPhoneNumber;
        target.GuestEmail = source.GuestEmail;
        target.GuestIdentityNumber = source.GuestIdentityNumber;
        target.GuestIdentityType = source.GuestIdentityType;
        target.GuestNote = source.GuestNote;
        target.BookingNote = source.BookingNote;
        target.BillNote = source.BillNote;
        target.PaymentReceived = source.PaymentReceived;
        target.PaymentMethod = source.PaymentMethod;
        target.ReferenceCode = source.ReferenceCode;

        var postedRooms = (source.Rooms ?? new List<BookingPaymentRecoveryRoomViewModel>())
            .Where(x => x is not null && x.BookingRoomId != Guid.Empty)
            .GroupBy(x => x.BookingRoomId)
            .ToDictionary(x => x.Key, x => x.First());

        foreach (var room in target.Rooms)
        {
            if (!postedRooms.TryGetValue(room.BookingRoomId, out var postedRoom))
                continue;

            room.Note = postedRoom.Note;

            var postedServices = (postedRoom.Services ?? new List<BookingPaymentRecoveryServiceViewModel>())
                .GroupBy(x => x.Id)
                .ToDictionary(x => x.Key, x => x.First());
            foreach (var service in room.Services)
            {
                if (postedServices.TryGetValue(service.Id, out var postedService))
                    service.Note = postedService.Note;
            }

            var postedFees = (postedRoom.Fees ?? new List<BookingPaymentRecoveryFeeViewModel>())
                .GroupBy(x => x.Id)
                .ToDictionary(x => x.Key, x => x.First());
            foreach (var fee in room.Fees)
            {
                if (postedFees.TryGetValue(fee.Id, out var postedFee))
                    fee.Description = postedFee.Description;
            }
        }
    }

    private static void CopyCheckInPostedFields(BookingCheckInViewModel target, BookingCheckInViewModel source)
    {
        target.BookingId = source.BookingId;
        target.GuestId = source.GuestId;
        target.CreateNewGuest = source.CreateNewGuest;
        target.GuestCode = source.GuestCode;
        target.AutoGeneratedGuestCode = source.AutoGeneratedGuestCode;
        target.GuestName = source.GuestName;
        target.GuestDateOfBirth = source.GuestDateOfBirth;
        target.GuestSex = source.GuestSex;
        target.GuestAddress = source.GuestAddress;
        target.GuestPhoneNumber = source.GuestPhoneNumber;
        target.GuestEmail = source.GuestEmail;
        target.GuestIdentityNumber = source.GuestIdentityNumber;
        target.GuestIdentityType = source.GuestIdentityType;
        target.GuestNote = source.GuestNote;
        target.TotalAdults = source.TotalAdults;
        target.TotalChildren = source.TotalChildren;
        target.BookingNote = source.BookingNote;

        if (source.Rooms is null)
            return;

        var targetById = target.Rooms.ToDictionary(x => x.BookingRoomId);
        foreach (var posted in source.Rooms)
        {
            if (!targetById.TryGetValue(posted.BookingRoomId, out var row))
                continue;

            row.RoomId = posted.RoomId;
            row.Adults = posted.Adults;
            row.Children = posted.Children;
            row.Note = posted.Note;
        }
    }

    private static void CopyCheckOutPostedFields(BookingCheckOutViewModel target, BookingCheckOutViewModel source)
    {
        target.BookingId = source.BookingId;
        target.GuestCode = source.GuestCode;
        target.GuestName = source.GuestName;
        target.GuestDateOfBirth = source.GuestDateOfBirth;
        target.GuestSex = source.GuestSex;
        target.GuestAddress = source.GuestAddress;
        target.GuestPhoneNumber = source.GuestPhoneNumber;
        target.GuestEmail = source.GuestEmail;
        target.GuestIdentityNumber = source.GuestIdentityNumber;
        target.GuestIdentityType = source.GuestIdentityType;
        target.GuestNote = source.GuestNote;
        target.BillCode = source.BillCode;
        target.BillNote = source.BillNote;
        target.BookingNote = source.BookingNote;
        target.PaymentReceived = source.PaymentReceived;
        target.PaymentMethod = source.PaymentMethod;
        target.ReferenceCode = source.ReferenceCode;

        var targetById = target.Rooms.ToDictionary(x => x.BookingRoomId);
        foreach (var posted in source.Rooms ?? new List<BookingCheckOutRoomViewModel>())
        {
            if (!targetById.TryGetValue(posted.BookingRoomId, out var row))
                continue;

            row.Note = posted.Note;
            row.NeedsMaintenance = posted.NeedsMaintenance;
            row.ServiceInputs = posted.ServiceInputs ?? new List<BookingCheckOutServiceInputViewModel>();
            row.FeeInputs = posted.FeeInputs ?? new List<BookingCheckOutFeeInputViewModel>();
        }
    }

    private static BookingCreateViewModel MapStep1ToViewModel(
        BookingCreateStep1ViewModel source,
        string? draftId)
    {
        return new BookingCreateViewModel
        {
            DraftId = draftId ?? source.DraftId,
            Step = 1,
            CheckInDate = source.CheckInDate,
            CheckOutDate = source.CheckOutDate,
            DefaultCheckInTime = source.DefaultCheckInTime,
            DefaultCheckOutTime = source.DefaultCheckOutTime,
            Nights = source.Nights,
            EstimatedRoomAmount = source.EstimatedRoomAmount,
            DepositAmount = source.DepositAmount,
            Step2StartedAt = source.Step2StartedAt,
            TotalAdults = source.TotalAdults,
            TotalChildren = source.TotalChildren,
            AvailabilityMessage = source.AvailabilityMessage,
            AvailabilitySucceeded = source.AvailabilitySucceeded,
            RoomSelections = source.RoomSelections?.ToList() ?? new List<BookingRoomSelectionViewModel>()
        };
    }

    private static BookingCreateViewModel MapStep2ToViewModel(
        BookingCreateStep2ViewModel source)
    {
        return new BookingCreateViewModel
        {
            DraftId = source.DraftId,
            BookingId = source.BookingId,
            Step = 2,
            BookingType = source.BookingType,
            CheckInDate = source.CheckInDate,
            CheckOutDate = source.CheckOutDate,
            DefaultCheckInTime = source.DefaultCheckInTime,
            DefaultCheckOutTime = source.DefaultCheckOutTime,
            BookingCode = source.BookingCode,
            BookingNote = source.BookingNote,
            GuestId = source.GuestId,
            GuestCode = source.GuestCode,
            GuestName = source.GuestName,
            GuestDateOfBirth = source.GuestDateOfBirth,
            GuestSex = source.GuestSex,
            GuestAddress = source.GuestAddress,
            GuestPhoneNumber = source.GuestPhoneNumber,
            GuestEmail = source.GuestEmail,
            GuestIdentityNumber = source.GuestIdentityNumber,
            GuestIdentityType = source.GuestIdentityType,
            GuestNote = source.GuestNote,
            Nights = source.Nights,
            EstimatedRoomAmount = source.EstimatedRoomAmount,
            DepositAmount = source.DepositAmount,
            TotalAdults = source.TotalAdults,
            TotalChildren = source.TotalChildren,
            PaymentMethod = source.PaymentMethod,
            PaymentDate = source.PaymentDate,
            ReferenceCode = source.ReferenceCode,
            DepositConfirmed = source.DepositConfirmed,
            AvailabilityMessage = source.AvailabilityMessage,
            AvailabilitySucceeded = source.AvailabilitySucceeded,
            RoomSelections = source.RoomSelections?.ToList() ?? new List<BookingRoomSelectionViewModel>(),
            Guests = source.Guests ?? Array.Empty<GuestOptionViewModel>()
        };
    }

    private const string BookingStep1CachePrefix = "Booking.Create.Step1.";
    private static readonly TimeSpan BookingDraftLifetime = TimeSpan.FromMinutes(30);

    private async Task SaveStep1DraftAsync(
        Guid userId,
        string draftId,
        BookingCreateStep1ViewModel model,
        CancellationToken cancellationToken)
    {
        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = BookingDraftLifetime
        };

        var json = JsonSerializer.Serialize(model);

        await _distributedCache.SetStringAsync(
            GetStep1CacheKey(userId, draftId),
            json,
            options,
            cancellationToken);
    }

    private async Task<BookingCreateStep1ViewModel?> LoadStep1DraftAsync(
        Guid userId,
        string draftId,
        CancellationToken cancellationToken)
    {
        var json = await _distributedCache.GetStringAsync(
            GetStep1CacheKey(userId, draftId),
            cancellationToken);

        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<BookingCreateStep1ViewModel>(json);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(
                ex,
                "Draft tạo Booking {DraftId} của user {UserId} không thể deserialize.",
                draftId,
                userId);

            await RemoveStep1DraftAsync(
                userId,
                draftId,
                cancellationToken);

            return null;
        }
    }

    private Task RemoveStep1DraftAsync(
        Guid userId,
        string draftId,
        CancellationToken cancellationToken)
    {
        return _distributedCache.RemoveAsync(
            GetStep1CacheKey(userId, draftId),
            cancellationToken);
    }

    private static string GetStep1CacheKey(
        Guid userId,
        string draftId) =>
        $"{BookingStep1CachePrefix}{userId:N}:{draftId}";

    private const string BookingDraftCookieName = "HotelManagement.BookingCreateDraftId";

    private string? ResolveDraftId(params string?[] candidates)
    {
        foreach (var candidate in candidates)
        {
            var normalized = NormalizeDraftId(candidate);
            if (normalized is not null)
            {
                return normalized;
            }
        }

        return NormalizeDraftId(Request.Cookies[BookingDraftCookieName]);
    }

    private void SetCurrentBookingDraftCookie(string draftId)
    {
        Response.Cookies.Append(
            BookingDraftCookieName,
            draftId,
            new CookieOptions
            {
                HttpOnly = true,
                Secure = Request.IsHttps,
                SameSite = SameSiteMode.Lax,
                IsEssential = true,
                MaxAge = BookingDraftLifetime
            });
    }

    private void ClearCurrentBookingDraftCookie()
    {
        Response.Cookies.Delete(
            BookingDraftCookieName,
            new CookieOptions
            {
                HttpOnly = true,
                Secure = Request.IsHttps,
                SameSite = SameSiteMode.Lax,
                IsEssential = true
            });
    }

    private static string? NormalizeDraftId(string? draftId)
    {
        if (!Guid.TryParse(draftId, out var id) || id == Guid.Empty)
        {
            return null;
        }

        return id.ToString("N");
    }

    private static void CopyPostedStep2Fields(
        BookingCreateStep2ViewModel target,
        BookingCreateStep2ViewModel posted)
    {
        target.DraftId = posted.DraftId;
        target.Step2StartedAt = posted.Step2StartedAt;
        target.BookingId = posted.BookingId;
        target.BookingType = posted.BookingType;
        target.BookingCode = posted.BookingCode;
        target.BookingNote = posted.BookingNote;
        target.GuestId = posted.GuestId;
        target.GuestCode = posted.GuestCode;
        target.GuestName = posted.GuestName;
        target.GuestDateOfBirth = posted.GuestDateOfBirth;
        target.GuestSex = posted.GuestSex;
        target.GuestAddress = posted.GuestAddress;
        target.GuestPhoneNumber = posted.GuestPhoneNumber;
        target.GuestEmail = posted.GuestEmail;
        target.GuestIdentityNumber = posted.GuestIdentityNumber;
        target.GuestIdentityType = posted.GuestIdentityType;
        target.GuestNote = posted.GuestNote;
        target.PaymentMethod = posted.PaymentMethod;
        target.PaymentDate = posted.PaymentDate;
        target.ReferenceCode = posted.ReferenceCode;
        target.DepositConfirmed = posted.DepositConfirmed;
    }

    private static void CopyMissingStep2Fields(
        BookingCreateStep2ViewModel target,
        BookingCreateStep2ViewModel rebuilt)
    {
        target.CheckInDate = rebuilt.CheckInDate;
        target.Step2StartedAt = rebuilt.Step2StartedAt;
        target.CheckOutDate = rebuilt.CheckOutDate;
        target.DefaultCheckInTime = rebuilt.DefaultCheckInTime;
        target.DefaultCheckOutTime = rebuilt.DefaultCheckOutTime;
        target.TotalAdults = rebuilt.TotalAdults;
        target.TotalChildren = rebuilt.TotalChildren;
        target.Nights = rebuilt.Nights;
        target.EstimatedRoomAmount = rebuilt.EstimatedRoomAmount;
        target.DepositAmount = rebuilt.DepositAmount;
        target.AvailabilityMessage = rebuilt.AvailabilityMessage;
        target.AvailabilitySucceeded = rebuilt.AvailabilitySucceeded;
        target.RoomSelections = rebuilt.RoomSelections;
        target.Guests = rebuilt.Guests;
    }

    private bool HasCheckInGuestModelStateErrors()
    {
        var guestKeys = new[]
        {
            nameof(BookingCheckInViewModel.GuestId),
            nameof(BookingCheckInViewModel.CreateNewGuest),
            nameof(BookingCheckInViewModel.GuestCode),
            nameof(BookingCheckInViewModel.GuestName),
            nameof(BookingCheckInViewModel.GuestDateOfBirth),
            nameof(BookingCheckInViewModel.GuestSex),
            nameof(BookingCheckInViewModel.GuestAddress),
            nameof(BookingCheckInViewModel.GuestPhoneNumber),
            nameof(BookingCheckInViewModel.GuestEmail),
            nameof(BookingCheckInViewModel.GuestIdentityNumber),
            nameof(BookingCheckInViewModel.GuestIdentityType),
            nameof(BookingCheckInViewModel.GuestNote)
        };

        return guestKeys.Any(key => ModelState.TryGetValue(key, out var entry) && entry.Errors.Count > 0);
    }

    private static DateTime TruncateToMinute(DateTime value)
    {
        return new DateTime(
            value.Year, value.Month, value.Day,
            value.Hour, value.Minute, 0,
            DateTimeKind.Unspecified);
    }

    private void ApplyPaginationUrl(BookingIndexViewModel model)
    {
        var baseUrl = Url.Action(nameof(Index), "Booking") ?? "/Booking/Index";

        model.Pagination = new PaginationViewModel
        {
            CurrentPage = model.Pagination.CurrentPage,
            PageSize = model.Pagination.PageSize,
            TotalItems = model.Pagination.TotalItems,
            PageParam = model.Pagination.PageParam,
            PageSizeParam = model.Pagination.PageSizeParam,
            BaseUrl = baseUrl,
            ItemLabel = model.Pagination.ItemLabel,
            QueryParameters = new Dictionary<string, string?>
            {
                ["Filter.Search"] = model.Filter.Search,
                ["Filter.Sort"] = ((byte)model.Filter.Sort).ToString(CultureInfo.InvariantCulture),
                ["Filter.Status"] = model.Filter.Status?.ToString(CultureInfo.InvariantCulture),
                ["Filter.CurrentPage"] = model.Filter.CurrentPage.ToString(CultureInfo.InvariantCulture),
                ["Filter.PageSize"] = model.Filter.PageSize.ToString(CultureInfo.InvariantCulture)
            }
        };
    }

    private Dictionary<string, string> GetModelStateErrors()
    {
        var errors = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in ModelState)
        {
            var firstError = entry.Value?.Errors
                .Select(x => x.ErrorMessage)
                .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));

            if (!string.IsNullOrWhiteSpace(firstError))
            {
                errors[entry.Key] = firstError;
            }
        }
        return errors;
    }

    private void ApplyFieldErrors(IReadOnlyDictionary<string, string> fieldErrors)
    {
        foreach (var error in fieldErrors)
        {
            ModelState.AddModelError(error.Key, error.Value);
        }
    }

    private bool TryGetCurrentUserId(out Guid userId)
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(raw, out userId) && userId != Guid.Empty;
    }
}
