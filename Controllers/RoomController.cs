using System.Globalization;
using System.Security.Claims;
using Hotel_Management_Website.Filters;
using Hotel_Management_Website.IServices;
using Hotel_Management_Website.Models.ViewModels.Room;
using Microsoft.AspNetCore.Mvc;

namespace Hotel_Management_Website.Controllers;

[ReceptionAccess]
public sealed class RoomController : Controller
{
    private readonly IRoomService _roomService;
    private readonly ILogger<RoomController> _logger;

    public RoomController(
        IRoomService roomService,
        ILogger<RoomController> logger)
    {
        _roomService = roomService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        RoomListFilterViewModel filter,
        string? reset,
        CancellationToken cancellationToken)
    {
        filter ??= new RoomListFilterViewModel();

        if (string.Equals(reset, "room", StringComparison.OrdinalIgnoreCase))
        {
            filter = new RoomListFilterViewModel();
        }

        try
        {
            var model = await _roomService.GetIndexAsync(filter, cancellationToken);
            return View(model);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Không thể tải danh sách phòng.");

            return View(new RoomIndexViewModel
            {
                Filter = filter,
                Rows = Array.Empty<RoomFloorRowViewModel>(),
                RoomTypes = Array.Empty<RoomTypeOptionViewModel>(),
                TotalRooms = 0,
                TotalPages = 1,
                LoadError = "Không thể tải danh sách phòng. Vui lòng thử lại."
            });
        }
    }

    [HttpGet]
    public async Task<IActionResult> Details(
        Guid id,
        string? returnUrl,
        CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
            return NotFound();

        try
        {
            var model = await _roomService.GetDetailsAsync(
                id,
                returnUrl,
                cancellationToken);

            return model is null
                ? NotFound()
                : View(model);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi tải chi tiết phòng {RoomId}.", id);
            TempData["ErrorMessage"] = "Không thể tải chi tiết phòng. Vui lòng thử lại.";
            return RedirectToAction(nameof(Index));
        }
    }

    [AdminAccess]
    [HttpGet]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        try
        {
            return View(await _roomService.GetCreateModelAsync(cancellationToken));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Không thể mở trang tạo phòng.");
            TempData["ErrorMessage"] = "Không thể mở trang tạo phòng. Vui lòng thử lại.";
            return RedirectToAction(nameof(Index));
        }
    }

    [AdminAccess]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        RoomCreateViewModel model,
        CancellationToken cancellationToken)
    {
        model ??= new RoomCreateViewModel();
        model.RoomCode = (model.RoomCode ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(model.RoomCode))
        {
            model.RoomCode = await _roomService.GetNextRoomCodeAsync(cancellationToken);
            ModelState.Remove(nameof(RoomCreateViewModel.RoomCode));
        }

        if (!ModelState.IsValid)
        {
            await PopulateCreateLookupsAsync(model, cancellationToken);
            return View(model);
        }

        if (!TryGetCurrentUserId(out var currentUserId))
            return Challenge();

        try
        {
            var result = await _roomService.CreateAsync(
                model,
                currentUserId,
                cancellationToken);

            AddFieldErrors(result.FieldErrors);

            if (!result.Succeeded)
            {
                ModelState.AddModelError(
                    string.Empty,
                    result.ErrorMessage ?? "Không thể tạo phòng.");

                await PopulateCreateLookupsAsync(model, cancellationToken);
                return View(model);
            }

            TempData["SuccessMessage"] =
                $"Đã tạo phòng {model.RoomCode} thành công.";

            return RedirectToAction(nameof(Index));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi tạo phòng {RoomCode}.", model.RoomCode);
            ModelState.AddModelError(
                string.Empty,
                "Không thể tạo phòng. Vui lòng thử lại.");

            await PopulateCreateLookupsAsync(model, cancellationToken);
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

        var model = await _roomService.GetEditModelAsync(
            id,
            returnUrl,
            cancellationToken);

        return model is null
            ? NotFound()
            : View(model);
    }

    [AdminAccess]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        RoomEditViewModel model,
        CancellationToken cancellationToken)
    {
        model ??= new RoomEditViewModel();

        // Lấy snapshot thực tế từ DB để RoomCode/Status không thể bị giả mạo.
        if (model.Id != Guid.Empty)
        {
            var current = await _roomService.GetEditModelAsync(
                model.Id,
                model.ReturnUrl,
                cancellationToken);

            if (current is not null)
            {
                model.Status = current.Status;

                // Khi phòng đang sử dụng, mã và mọi thuộc tính khác ngoài Mô tả/Ghi chú đều bị khóa.
                if (current.Status == 1)
                {
                    model.RoomCode = current.RoomCode;
                    ModelState.Remove(nameof(RoomEditViewModel.RoomCode));
                    model.RoomName = current.RoomName;
                    model.Floor = current.Floor;
                    model.Row = current.Row;
                    model.LeftToRightOrder = current.LeftToRightOrder;
                    model.RoomTypeId = current.RoomTypeId;
                    model.IsActive = current.IsActive;

                    foreach (var field in new[]
                    {
                        nameof(RoomEditViewModel.RoomName),
                        nameof(RoomEditViewModel.Floor),
                        nameof(RoomEditViewModel.Row),
                        nameof(RoomEditViewModel.LeftToRightOrder),
                        nameof(RoomEditViewModel.RoomTypeId),
                        nameof(RoomEditViewModel.IsActive)
                    })
                    {
                        ModelState.Remove(field);
                    }
                }
            }
        }

        if (!ModelState.IsValid)
        {
            await PopulateEditLookupsAsync(model, cancellationToken);
            return View(model);
        }

        if (!TryGetCurrentUserId(out var currentUserId))
            return Challenge();

        try
        {
            var result = await _roomService.UpdateAsync(
                model,
                currentUserId,
                cancellationToken);

            AddFieldErrors(result.FieldErrors);

            if (!result.Succeeded)
            {
                ModelState.AddModelError(
                    string.Empty,
                    result.ErrorMessage ?? "Không thể lưu thay đổi phòng.");

                await PopulateEditLookupsAsync(model, cancellationToken);
                return View(model);
            }

            TempData["SuccessMessage"] =
                $"Đã cập nhật phòng {model.RoomCode} thành công.";

            return RedirectToLocalReturnUrl(model.ReturnUrl);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi sửa phòng {RoomId}.", model.Id);
            ModelState.AddModelError(
                string.Empty,
                "Không thể lưu thay đổi phòng. Vui lòng thử lại.");

            await PopulateEditLookupsAsync(model, cancellationToken);
            return View(model);
        }
    }

    [AdminAccess]
    [HttpGet]
    public async Task<IActionResult> GetPositionSuggestions(
        int floor,
        int? row,
        CancellationToken cancellationToken)
    {
        var result = await _roomService.GetPositionSuggestionsAsync(
            floor,
            row,
            cancellationToken);

        return Json(result);
    }

    [AdminAccess]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Deactivate(
        Guid id,
        bool isMaintenance,
        string? returnUrl,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
            return Challenge();

        var result = await _roomService.DeactivateAsync(
            id,
            currentUserId,
            isMaintenance,
            cancellationToken);

        TempData[result.Succeeded ? "SuccessMessage" : "ErrorMessage"] =
            result.Succeeded
                ? "Đã dừng hoạt động phòng thành công."
                : result.ErrorMessage ?? "Không thể dừng hoạt động phòng.";

        return RedirectToLocalReturnUrl(returnUrl);
    }

    [AdminAccess]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Activate(
        Guid id,
        string? returnUrl,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
            return Challenge();

        var result = await _roomService.ActivateAsync(
            id,
            currentUserId,
            cancellationToken);

        TempData[result.Succeeded ? "SuccessMessage" : "ErrorMessage"] =
            result.Succeeded
                ? "Đã mở hoạt động phòng thành công."
                : result.ErrorMessage ?? "Không thể mở hoạt động phòng.";

        return RedirectToLocalReturnUrl(returnUrl);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReceptionSetMaintenance(
        Guid id,
        string? returnUrl,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
            return Challenge();

        var result = await _roomService.DeactivateAsync(
            id,
            currentUserId,
            isMaintenance: true,
            cancellationToken);

        TempData[result.Succeeded ? "SuccessMessage" : "ErrorMessage"] =
            result.Succeeded
                ? "Đã chuyển phòng sang trạng thái Bảo trì và dừng hoạt động."
                : result.ErrorMessage ?? "Không thể chuyển phòng sang Bảo trì.";

        return RedirectToLocalReturnUrl(returnUrl);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReceptionActivate(
        Guid id,
        string? returnUrl,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
            return Challenge();

        var result = await _roomService.ActivateAsync(
            id,
            currentUserId,
            cancellationToken);

        TempData[result.Succeeded ? "SuccessMessage" : "ErrorMessage"] =
            result.Succeeded
                ? "Đã chuyển phòng từ Bảo trì về Trống và mở hoạt động phòng."
                : result.ErrorMessage ?? "Không thể mở hoạt động phòng.";

        return RedirectToLocalReturnUrl(returnUrl);
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

        var result = await _roomService.DeleteAsync(
            id,
            currentUserId,
            cancellationToken);

        TempData[result.Succeeded ? "SuccessMessage" : "ErrorMessage"] =
            result.Succeeded
                ? "Đã xóa phòng thành công."
                : result.ErrorMessage ?? "Không thể xóa phòng.";

        return RedirectToLocalReturnUrl(returnUrl);
    }

    private async Task PopulateCreateLookupsAsync(
        RoomCreateViewModel model,
        CancellationToken cancellationToken)
    {
        var types = await _roomService.GetCreateModelAsync(cancellationToken);
        model.AvailableRoomTypes = types.AvailableRoomTypes;

        if (string.IsNullOrWhiteSpace(model.RoomCode))
            model.RoomCode = types.RoomCode;
    }

    private async Task PopulateEditLookupsAsync(
        RoomEditViewModel model,
        CancellationToken cancellationToken)
    {
        var current = await _roomService.GetEditModelAsync(
            model.Id,
            model.ReturnUrl,
            cancellationToken);

        model.AvailableRoomTypes = current?.AvailableRoomTypes
            ?? Array.Empty<RoomTypeOptionViewModel>();
    }

    private void AddFieldErrors(IReadOnlyDictionary<string, string> fieldErrors)
    {
        foreach (var item in fieldErrors)
            ModelState.AddModelError(item.Key, item.Value);
    }

    private IActionResult RedirectToLocalReturnUrl(string? returnUrl)
    {
        return !string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl)
            ? LocalRedirect(returnUrl)
            : RedirectToAction(nameof(Index));
    }

    private bool TryGetCurrentUserId(out Guid userId)
    {
        var rawUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(rawUserId, out userId);
    }
}
