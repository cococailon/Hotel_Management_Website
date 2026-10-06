using System.Security.Claims;
using Hotel_Management_Website.Filters;
using Hotel_Management_Website.IServices;
using Hotel_Management_Website.Models.ViewModels.HotelPolicy;
using Microsoft.AspNetCore.Mvc;

namespace Hotel_Management_Website.Controllers;

[ReceptionAccess]
public sealed class HotelPolicyController : Controller
{
    private readonly IHotelPolicyService _service;
    private readonly ILogger<HotelPolicyController> _logger;

    public HotelPolicyController(
        IHotelPolicyService service,
        ILogger<HotelPolicyController> logger)
    {
        _service = service;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        try
        {
            var model = await _service.GetAsync(cancellationToken);
            return View(model);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Không thể tải quy định khách sạn.");
            TempData["ErrorMessage"] = "Không thể tải quy định khách sạn. Vui lòng thử lại.";
            return RedirectToAction("Index", "Reception");
        }
    }

    [AdminAccess]
    [HttpGet]
    public async Task<IActionResult> Edit(CancellationToken cancellationToken)
    {
        try
        {
            var model = await _service.GetAsync(cancellationToken);
            return View(model);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Không thể mở trang chỉnh sửa quy định khách sạn.");
            TempData["ErrorMessage"] = "Không thể mở trang chỉnh sửa quy định khách sạn. Vui lòng thử lại.";
            return RedirectToAction(nameof(Index));
        }
    }

    [AdminAccess]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        HotelPolicyViewModel model,
        CancellationToken cancellationToken)
    {
        model ??= new HotelPolicyViewModel();

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        if (!TryGetCurrentUserId(out var currentUserId))
            return Challenge();

        try
        {
            var result = await _service.UpdateAsync(
                model,
                currentUserId,
                cancellationToken);

            if (!result.Succeeded)
            {
                ModelState.AddModelError(string.Empty,
                    result.ErrorMessage ?? "Không thể lưu quy định khách sạn.");
                return View(model);
            }

            TempData["SuccessMessage"] = "Đã cập nhật quy định khách sạn thành công.";
            return RedirectToAction(nameof(Index));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi lưu quy định khách sạn.");
            ModelState.AddModelError(string.Empty,
                "Không thể lưu quy định khách sạn. Vui lòng thử lại.");
            return View(model);
        }
    }

    private bool TryGetCurrentUserId(out Guid userId)
    {
        userId = Guid.Empty;
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out userId) && userId != Guid.Empty;
    }
}
