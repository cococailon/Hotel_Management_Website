using System.Globalization;
using System.Security.Claims;
using Hotel_Management_Website.Filters;
using Hotel_Management_Website.IServices;
using Hotel_Management_Website.Models.ViewModels.Guest;
using Hotel_Management_Website.Models.ViewModels.UI;
using Hotel_Management_Website.Security;
using Microsoft.AspNetCore.Mvc;

namespace Hotel_Management_Website.Controllers;

[ReceptionAccess]
public sealed class GuestController : Controller
{
    private readonly IGuestService _guestService;
    private readonly ILogger<GuestController> _logger;

    public GuestController(
        IGuestService guestService,
        ILogger<GuestController> logger)
    {
        _guestService = guestService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        GuestIndexQueryViewModel query,
        CancellationToken cancellationToken)
    {
        query ??= new GuestIndexQueryViewModel();
        query.Filter ??= new GuestListFilterViewModel();

        if (string.Equals(query.Reset, "guest", StringComparison.OrdinalIgnoreCase))
        {
            query.Filter = new GuestListFilterViewModel();
        }

        try
        {
            var model = await _guestService.GetIndexAsync(query.Filter, cancellationToken);
            ApplyPaginationUrl(model);
            return View(model);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Không thể tải danh sách khách hàng đặt phòng.");

            return View(new GuestIndexViewModel
            {
                Filter = query.Filter,
                Guests = Array.Empty<GuestListItemViewModel>(),
                TotalCount = 0,
                Pagination = new PaginationViewModel
                {
                    CurrentPage = 1,
                    PageSize = 10,
                    TotalItems = 0,
                    PageParam = "Filter.CurrentPage",
                    PageSizeParam = "Filter.PageSize",
                    BaseUrl = "/Guest/Index",
                    ItemLabel = "khách hàng"
                },
                LoadError = "Không thể tải danh sách khách hàng. Vui lòng thử lại."
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
            var model = await _guestService.GetDetailsAsync(id, cancellationToken);
            return model is null ? NotFound() : View(model);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Không thể tải chi tiết khách hàng {GuestId}.", id);
            TempData["ErrorMessage"] = "Không thể tải chi tiết khách hàng. Vui lòng thử lại.";
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
            return NotFound();

        try
        {
            var model = await _guestService.GetEditModelAsync(id, cancellationToken);
            return model is null ? NotFound() : View(model);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Không thể mở chỉnh sửa khách hàng {GuestId}.", id);
            TempData["ErrorMessage"] = "Không thể mở chức năng chỉnh sửa khách hàng. Vui lòng thử lại.";
            return RedirectToAction(nameof(Index));
        }
    }

    [ReceptionOnlyAccess]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        GuestEditViewModel model,
        CancellationToken cancellationToken)
    {
        model ??= new GuestEditViewModel();

        if (!ModelState.IsValid)
        {
            var rebuilt = await _guestService.GetEditModelAsync(model.Id, cancellationToken);
            if (rebuilt is not null)
            {
                CopyPostedFields(rebuilt, model);
                ApplyModelStateErrorsToView();
                return View(rebuilt);
            }

            TempData["ErrorMessage"] = "Khách hàng không còn tồn tại hoặc đã bị thay đổi. Vui lòng mở lại danh sách.";
            return RedirectToAction(nameof(Index));
        }

        if (!TryGetCurrentUserId(out var currentUserId))
            return Challenge();

        var result = await _guestService.UpdateAsync(model, currentUserId, cancellationToken);

        if (!result.Succeeded)
        {
            ModelState.Clear();
            foreach (var error in result.FieldErrors)
                ModelState.AddModelError(error.Key, error.Value);

            if (!string.IsNullOrWhiteSpace(result.ErrorMessage)
                && result.FieldErrors.Count == 0)
            {
                ModelState.AddModelError(string.Empty, result.ErrorMessage);
            }
            else if (result.FieldErrors.Count > 0)
            {
                ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Vui lòng kiểm tra lại thông tin được đánh dấu.");
            }

            var rebuilt = await _guestService.GetEditModelAsync(model.Id, cancellationToken);
            if (rebuilt is not null)
            {
                CopyPostedFields(rebuilt, model);
                return View(rebuilt);
            }

            TempData["ErrorMessage"] = result.ErrorMessage ?? "Không thể cập nhật khách hàng.";
            return RedirectToAction(nameof(Index));
        }

        TempData["SuccessMessage"] = $"Đã cập nhật khách hàng {model.GuestCode} thành công.";
        return RedirectToAction(nameof(Details), new { id = model.Id });
    }

    [AdminAccess]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(
        Guid id,
        CancellationToken cancellationToken)
    {
        // Lớp bảo vệ thứ hai ngoài [AdminAccess]: kể cả khi endpoint/action
        // bị thay đổi filter trong tương lai, non-admin vẫn không thể xóa.
        if (!User.IsInRole(AppRoles.Admin))
            return Forbid();

        if (!TryGetCurrentUserId(out var currentUserId))
            return Challenge();

        if (id == Guid.Empty)
        {
            TempData["ErrorMessage"] = "Khách hàng không hợp lệ.";
            return RedirectToAction(nameof(Index));
        }

        var result = await _guestService.DeleteAsync(id, currentUserId, cancellationToken);
        TempData[result.Succeeded ? "SuccessMessage" : "ErrorMessage"] = result.Succeeded
            ? "Đã xóa khách hàng và toàn bộ lịch đặt phòng, phòng đặt, dịch vụ/phí phát sinh liên quan."
            : result.ErrorMessage ?? "Không thể xóa khách hàng.";

        return RedirectToAction(nameof(Index));
    }

    private void ApplyPaginationUrl(GuestIndexViewModel model)
    {
        var baseUrl = Url.Action(nameof(Index), "Guest") ?? "/Guest/Index";

        model.Pagination = new PaginationViewModel
        {
            CurrentPage = model.Pagination.CurrentPage,
            PageSize = model.Pagination.PageSize,
            TotalItems = model.Pagination.TotalItems,
            PageParam = model.Pagination.PageParam,
            PageSizeParam = model.Pagination.PageSizeParam,
            BaseUrl = baseUrl,
            ItemLabel = "khách hàng",
            QueryParameters = new Dictionary<string, string?>
            {
                ["Filter.Search"] = model.Filter.Search,
                ["Filter.Sort"] = ((byte)model.Filter.Sort).ToString(CultureInfo.InvariantCulture),
                ["Filter.Sex"] = model.Filter.Sex?.ToString(CultureInfo.InvariantCulture),
                ["Filter.CurrentPage"] = model.Filter.CurrentPage.ToString(CultureInfo.InvariantCulture),
                ["Filter.PageSize"] = model.Filter.PageSize.ToString(CultureInfo.InvariantCulture)
            }
        };
    }

    private static void CopyPostedFields(
        GuestEditViewModel target,
        GuestEditViewModel source)
    {
        target.GuestCode = source.GuestCode;
        target.Name = source.Name;
        target.DateOfBirth = source.DateOfBirth;
        target.Sex = source.Sex;
        target.Address = source.Address;
        target.PhoneNumber = source.PhoneNumber;
        target.Email = source.Email;
        target.IdentityNumber = source.IdentityNumber;
        target.IdentityType = source.IdentityType;
        target.Note = source.Note;
    }

    private void ApplyModelStateErrorsToView()
    {
        // ModelState đã được giữ nguyên, chỉ cần để ASP.NET Core render các lỗi.
    }

    private bool TryGetCurrentUserId(out Guid userId)
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out userId) && userId != Guid.Empty;
    }
}
