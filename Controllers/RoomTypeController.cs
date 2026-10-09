using System.Globalization;
using System.Security.Claims;
using Hotel_Management_Website.Filters;
using Hotel_Management_Website.IServices;
using Hotel_Management_Website.Models.ViewModels.RoomType;
using Microsoft.AspNetCore.Mvc;

namespace Hotel_Management_Website.Controllers;

[ReceptionAccess]
public sealed class RoomTypeController : Controller
{
    private readonly IRoomTypeService _roomTypeService;
    private readonly ILogger<RoomTypeController> _logger;

    public RoomTypeController(
        IRoomTypeService roomTypeService,
        ILogger<RoomTypeController> logger)
    {
        _roomTypeService = roomTypeService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        RoomTypeIndexQueryViewModel query,
        CancellationToken cancellationToken)
    {
        query ??= new RoomTypeIndexQueryViewModel();
        query.Filter ??= new RoomTypeListFilterViewModel();

        if (string.Equals(query.Reset, "roomtype", StringComparison.OrdinalIgnoreCase))
        {
            query.Filter = new RoomTypeListFilterViewModel();
        }

        try
        {
            var model = await _roomTypeService.GetIndexAsync(
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
            _logger.LogError(ex, "Không thể tải danh sách loại phòng.");
            return View(new RoomTypeIndexViewModel
            {
                Filter = query.Filter,
                RoomTypes = Array.Empty<RoomTypeListItemViewModel>(),
                TotalCount = 0,
                LoadError = "Không thể tải danh sách loại phòng. Vui lòng thử lại."
            });
        }
    }

    [HttpGet]
    public async Task<IActionResult> Details(
        Guid id,
        CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
            return NotFound();

        try
        {
            var model = await _roomTypeService.GetDetailsAsync(
                id,
                cancellationToken);

            return model is null
                ? NotFound()
                : View(model);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Lỗi khi tải chi tiết loại phòng {RoomTypeId}.",
                id);

            TempData["ErrorMessage"] =
                "Không thể tải chi tiết loại phòng. Vui lòng thử lại.";

            return RedirectToAction(nameof(Index));
        }
    }

    [AdminAccess]
    [HttpGet]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        try
        {
            return View(await _roomTypeService.PrepareCreateAsync(cancellationToken));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Không thể mở trang tạo loại phòng.");
            TempData["ErrorMessage"] = "Không thể mở trang tạo loại phòng. Vui lòng thử lại.";
            return RedirectToAction(nameof(Index));
        }
    }

    [AdminAccess]
    [HttpGet]
    public async Task<IActionResult> GetNextCode(CancellationToken cancellationToken)
    {
        try
        {
            var code = await _roomTypeService.GetNextRoomTypeCodeAsync(cancellationToken);
            return Json(new { code });
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Không thể tự sinh mã loại phòng.");
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                message = "Không thể tự sinh mã loại phòng. Vui lòng thử lại."
            });
        }
    }

    [AdminAccess]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        RoomTypeCreateViewModel model,
        CancellationToken cancellationToken)
    {
        model ??= new RoomTypeCreateViewModel();
        model.RoomTypeCode = (model.RoomTypeCode ?? string.Empty).Trim();

        if (!ModelState.IsValid)
        {
            await _roomTypeService.PopulateCreateLookupsAsync(model, cancellationToken);
            return View(model);
        }

        if (!TryGetCurrentUserId(out var currentUserId))
            return Challenge();

        try
        {
            var result = await _roomTypeService.CreateAsync(
                model,
                currentUserId,
                cancellationToken);

            AddFieldErrors(result.FieldErrors);

            if (!result.Succeeded)
            {
                await _roomTypeService.PopulateCreateLookupsAsync(model, cancellationToken);
                return View(model);
            }

            TempData["SuccessMessage"] = $"Đã tạo loại phòng {model.RoomTypeCode} thành công.";
            return RedirectToAction(nameof(Index));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi tạo loại phòng {RoomTypeCode}.", model.RoomTypeCode);
            ModelState.AddModelError(string.Empty, "Không thể tạo loại phòng. Vui lòng thử lại.");
            await _roomTypeService.PopulateCreateLookupsAsync(model, cancellationToken);
            return View(model);
        }
    }

    [AdminAccess]
    [HttpGet]
    public async Task<IActionResult> Edit(
        Guid id,
        string? returnUrl,
        CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
            return NotFound();

        var model = await _roomTypeService.GetEditModelAsync(id, returnUrl, cancellationToken);
        return model is null ? NotFound() : View(model);
    }

    [AdminAccess]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        RoomTypeEditViewModel model,
        CancellationToken cancellationToken)
    {
        model ??= new RoomTypeEditViewModel();

        // Khi đang có Booking hoạt động, 5 thuộc tính bị khóa trên UI
        // không được phép tham gia vào model binding/validation của request.
        // Lấy snapshot hiện tại từ DB và giữ nguyên chúng trước khi kiểm tra ModelState.
        if (model.Id != Guid.Empty)
        {
            var current = await _roomTypeService.GetEditModelAsync(
                model.Id,
                model.ReturnUrl,
                cancellationToken);

            if (current is not null)
            {
                // Trạng thái không được sửa tại màn hình Edit.
                model.IsActive = current.IsActive;
                ModelState.Remove(nameof(RoomTypeEditViewModel.IsActive));

                if (current.HasActiveBookings)
                {
                    model.RoomTypeCode = current.RoomTypeCode;
                    ModelState.Remove(nameof(RoomTypeEditViewModel.RoomTypeCode));
                    model.MaxAdults = current.MaxAdults;
                    model.MaxChildren = current.MaxChildren;
                    model.PricePerNight = current.PricePerNight;
                    model.DepositPercent = current.DepositPercent;
                    model.HasActiveBookings = true;

                    ModelState.Remove(nameof(RoomTypeEditViewModel.MaxAdults));
                    ModelState.Remove(nameof(RoomTypeEditViewModel.MaxChildren));
                    ModelState.Remove(nameof(RoomTypeEditViewModel.PricePerNight));
                    ModelState.Remove(nameof(RoomTypeEditViewModel.DepositPercent));
                }
            }
        }

        if (!ModelState.IsValid)
        {
            await _roomTypeService.PopulateEditLookupsAsync(model, false, cancellationToken);
            return View(model);
        }

        if (!TryGetCurrentUserId(out var currentUserId))
            return Challenge();

        try
        {
            var result = await _roomTypeService.UpdateAsync(
                model,
                currentUserId,
                cancellationToken);

            AddFieldErrors(result.FieldErrors);

            if (!result.Succeeded)
            {
                ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Không thể lưu thay đổi loại phòng.");
                await _roomTypeService.PopulateEditLookupsAsync(model, false, cancellationToken);
                return View(model);
            }

            TempData["SuccessMessage"] = $"Đã cập nhật loại phòng {model.RoomTypeCode} thành công.";
            return RedirectToLocalReturnUrl(model.ReturnUrl);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi sửa loại phòng {RoomTypeId}.", model.Id);
            ModelState.AddModelError(string.Empty, "Không thể lưu thay đổi loại phòng. Vui lòng thử lại.");
            await _roomTypeService.PopulateEditLookupsAsync(model, false, cancellationToken);
            return View(model);
        }
    }

    [AdminAccess]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(
        Guid id,
        string? returnUrl,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
            return Challenge();

        var result = await _roomTypeService.DeleteAsync(id, currentUserId, cancellationToken);
        TempData[result.Succeeded ? "SuccessMessage" : "ErrorMessage"] =
            result.Succeeded
                ? "Đã xóa loại phòng thành công."
                : result.ErrorMessage ?? "Không thể xóa loại phòng.";

        return RedirectToLocalReturnUrl(returnUrl);
    }

    [AdminAccess]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Deactivate(
        Guid id,
        string? returnUrl,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
            return Challenge();

        var result = await _roomTypeService.DeactivateAsync(id, currentUserId, cancellationToken);
        TempData[result.Succeeded ? "SuccessMessage" : "ErrorMessage"] =
            result.Succeeded
                ? "Đã dừng hoạt động loại phòng và các phòng đang hoạt động được gán cho loại phòng này."
                : result.ErrorMessage ?? "Không thể dừng hoạt động loại phòng.";

        return RedirectToLocalReturnUrl(returnUrl);
    }

    [AdminAccess]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Activate(
        Guid id,
        bool reopenRooms,
        string? returnUrl,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
            return Challenge();

        var result = await _roomTypeService.ActivateAsync(
            id,
            reopenRooms,
            currentUserId,
            cancellationToken);

        TempData[result.Succeeded ? "SuccessMessage" : "ErrorMessage"] =
            result.Succeeded
                ? (result.ReopenedRoomCount > 0
                    ? $"Đã mở hoạt động loại phòng và {result.ReopenedRoomCount} phòng liên quan."
                    : "Đã mở hoạt động loại phòng.")
                : result.ErrorMessage ?? "Không thể mở hoạt động loại phòng.";

        return RedirectToLocalReturnUrl(returnUrl);
    }

    private void AddFieldErrors(IReadOnlyDictionary<string, string> fieldErrors)
    {
        foreach (var item in fieldErrors)
            ModelState.AddModelError(item.Key, item.Value);
    }

    private void ApplyPaginationUrl(RoomTypeIndexViewModel model)
    {
        var baseUrl = Url.Action(nameof(Index), "RoomType") ?? "/RoomType/Index";

        model.Pagination = new Models.ViewModels.UI.PaginationViewModel
        {
            CurrentPage = model.Pagination.CurrentPage,
            PageSize = model.Pagination.PageSize,
            TotalItems = model.Pagination.TotalItems,
            PageParam = model.Pagination.PageParam,
            PageSizeParam = model.Pagination.PageSizeParam,
            BaseUrl = baseUrl,
            ItemLabel = "loại phòng",
            QueryParameters = new Dictionary<string, string?>
            {
                ["Filter.Search"] = model.Filter.Search,
                ["Filter.Sort"] = ((byte)model.Filter.Sort).ToString(CultureInfo.InvariantCulture),
                ["Filter.IsActive"] = model.Filter.IsActive?.ToString().ToLowerInvariant(),
                ["Filter.CurrentPage"] = model.Filter.CurrentPage.ToString(CultureInfo.InvariantCulture),
                ["Filter.PageSize"] = model.Filter.PageSize.ToString(CultureInfo.InvariantCulture)
            }
        };
    }

    private IActionResult RedirectToLocalReturnUrl(string? returnUrl)
    {
        return !string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl)
            ? Redirect(returnUrl)
            : RedirectToAction(nameof(Index));
    }

    private bool TryGetCurrentUserId(out Guid userId)
    {
        userId = Guid.Empty;
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out userId) && userId != Guid.Empty;
    }
}
