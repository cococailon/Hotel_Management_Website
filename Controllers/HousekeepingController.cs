using System.Security.Claims;
using Hotel_Management_Website.IServices;
using Hotel_Management_Website.Models.ViewModels.Housekeeping;
using Hotel_Management_Website.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hotel_Management_Website.Controllers;

[ResponseCache(
    Duration = 0,
    Location = ResponseCacheLocation.None,
    NoStore = true)]
public sealed class HousekeepingController : Controller
{
    private readonly IRoomService _roomService;
    private readonly ILogger<HousekeepingController> _logger;

    public HousekeepingController(
        IRoomService roomService,
        ILogger<HousekeepingController> logger)
    {
        _roomService = roomService;
        _logger = logger;
    }

    [Authorize(Roles = AppRoles.Housekeeping)]
    [HttpGet]
    public IActionResult Index()
    {
        return View();
    }

    [Authorize(Roles = AppRoles.Housekeeping + "," + AppRoles.Admin)]
    [HttpGet]
    public async Task<IActionResult> RoomStatus(
        Models.ViewModels.Room.RoomListFilterViewModel filter,
        string? reset,
        CancellationToken cancellationToken)
    {
        filter ??= new Models.ViewModels.Room.RoomListFilterViewModel();

        if (string.Equals(reset, "housekeeping", StringComparison.OrdinalIgnoreCase))
        {
            filter = new Models.ViewModels.Room.RoomListFilterViewModel();
        }

        try
        {
            var model = await _roomService.GetHousekeepingRoomStatusAsync(filter, cancellationToken);
            return View(model);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Không thể tải quản lý tình trạng phòng.");
            return View(new HousekeepingRoomStatusViewModel
            {
                Rows = Array.Empty<Models.ViewModels.Room.RoomFloorRowViewModel>(),
                TotalRooms = 0,
                LoadError = "Không thể tải tình trạng phòng. Vui lòng thử lại."
            });
        }
    }

    [Authorize(Roles = AppRoles.Housekeeping + "," + AppRoles.Admin)]
    [HttpGet]
    public async Task<IActionResult> Details(
        Guid id,
        CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
            return NotFound();

        try
        {
            var returnUrl = Url.Action(nameof(RoomStatus));
            var model = await _roomService.GetDetailsAsync(id, returnUrl, cancellationToken);

            return model is null
                ? NotFound()
                : View("~/Views/Room/Details.cshtml", model);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi tải chi tiết phòng {RoomId} cho Buồng Phòng.", id);
            TempData["ErrorMessage"] = "Không thể tải chi tiết phòng. Vui lòng thử lại.";
            return RedirectToAction(nameof(RoomStatus));
        }
    }

    [Authorize(Roles = AppRoles.Housekeeping + "," + AppRoles.Admin)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> StartCleaning(
        Guid id,
        string? returnUrl,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
            return Challenge();

        var result = await _roomService.StartCleaningAsync(
            id,
            currentUserId,
            cancellationToken);

        TempData[result.Succeeded ? "SuccessMessage" : "ErrorMessage"] =
            result.Succeeded
                ? "Đã chuyển phòng sang trạng thái Đang dọn dẹp."
                : result.ErrorMessage ?? "Không thể bắt đầu dọn dẹp phòng.";

        return RedirectToLocalReturnUrl(returnUrl);
    }

    [Authorize(Roles = AppRoles.Housekeeping + "," + AppRoles.Admin)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CompleteCleaning(
        Guid id,
        string? returnUrl,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
            return Challenge();

        var result = await _roomService.CompleteCleaningAsync(
            id,
            currentUserId,
            cancellationToken);

        TempData[result.Succeeded ? "SuccessMessage" : "ErrorMessage"] =
            result.Succeeded
                ? "Đã chuyển phòng về trạng thái Trống."
                : result.ErrorMessage ?? "Không thể hoàn tất dọn dẹp phòng.";

        return RedirectToLocalReturnUrl(returnUrl);
    }

    [Authorize(Roles = AppRoles.Housekeeping + "," + AppRoles.Admin)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetMaintenance(
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

    private IActionResult RedirectToLocalReturnUrl(string? returnUrl)
    {
        return !string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl)
            ? LocalRedirect(returnUrl)
            : RedirectToAction(nameof(RoomStatus));
    }

    private bool TryGetCurrentUserId(out Guid userId)
    {
        var rawUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(rawUserId, out userId);
    }
}
