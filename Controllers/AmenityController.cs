using System.Globalization;
using System.Security.Claims;
using Hotel_Management_Website.Filters;
using Hotel_Management_Website.IServices;
using Hotel_Management_Website.Models.ViewModels.Amenity;
using Microsoft.AspNetCore.Mvc;

namespace Hotel_Management_Website.Controllers;

[ReceptionAccess]
public sealed class AmenityController : Controller
{
    private readonly IAmenityService _amenityService;
    private readonly ILogger<AmenityController> _logger;

    public AmenityController(
        IAmenityService amenityService,
        ILogger<AmenityController> logger)
    {
        _amenityService = amenityService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        AmenityIndexQueryViewModel query,
        CancellationToken cancellationToken)
    {
        query ??= new AmenityIndexQueryViewModel();
        query.Filter ??= new AmenityListFilterViewModel();

        if (string.Equals(query.Reset, "amenity", StringComparison.OrdinalIgnoreCase))
        {
            query.Filter = new AmenityListFilterViewModel();
        }

        try
        {
            var model = await _amenityService.GetIndexAsync(
                query.Filter,
                cancellationToken);

            model.Pagination = new Models.ViewModels.UI.PaginationViewModel
            {
                CurrentPage = model.Pagination.CurrentPage,
                PageSize = model.Pagination.PageSize,
                TotalItems = model.Pagination.TotalItems,
                PageParam = model.Pagination.PageParam,
                PageSizeParam = model.Pagination.PageSizeParam,
                BaseUrl = Url.Action(nameof(Index), "Amenity") ?? "/Amenity/Index",
                ItemLabel = "tiện nghi",
                QueryParameters = new Dictionary<string, string?>
                {
                    ["Filter.Search"] = model.Filter.Search,
                    ["Filter.Sort"] = ((byte)model.Filter.Sort).ToString(CultureInfo.InvariantCulture),
                    ["Filter.Status"] = model.Filter.Status.HasValue
                        ? (model.Filter.Status.Value ? "true" : "false")
                        : null,
                    ["Filter.CurrentPage"] = model.Filter.CurrentPage.ToString(CultureInfo.InvariantCulture),
                    ["Filter.PageSize"] = model.Filter.PageSize.ToString(CultureInfo.InvariantCulture)
                }
            };

            return View(model);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Không thể tải danh sách tiện nghi.");
            return View(new AmenityIndexViewModel
            {
                Filter = query.Filter,
                Amenities = Array.Empty<AmenityListItemViewModel>(),
                TotalCount = 0,
                LoadError = "Không thể tải danh sách tiện nghi. Vui lòng thử lại."
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
            var model = await _amenityService.GetDetailsAsync(id, cancellationToken);
            return model is null ? NotFound() : View(model);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi tải chi tiết tiện nghi {AmenityId}.", id);
            TempData["ErrorMessage"] = "Không thể tải chi tiết tiện nghi. Vui lòng thử lại.";
            return RedirectToAction(nameof(Index));
        }
    }

    [AdminAccess]
    [HttpGet]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        try
        {
            return View(await _amenityService.PrepareCreateAsync(cancellationToken));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Không thể mở trang tạo tiện nghi.");
            TempData["ErrorMessage"] = "Không thể mở trang tạo tiện nghi. Vui lòng thử lại.";
            return RedirectToAction(nameof(Index));
        }
    }

    [AdminAccess]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        AmenityCreateViewModel model,
        CancellationToken cancellationToken)
    {
        model ??= new AmenityCreateViewModel();
        model.AmenityCode = (model.AmenityCode ?? string.Empty).Trim();

        if (!ModelState.IsValid)
            return View(model);

        if (!TryGetCurrentUserId(out var currentUserId))
            return Challenge();

        try
        {
            var result = await _amenityService.CreateAsync(
                model,
                currentUserId,
                cancellationToken);

            AddFieldErrors(result.FieldErrors);

            if (!result.Succeeded)
            {
                ModelState.AddModelError(
                    string.Empty,
                    result.ErrorMessage ?? "Không thể tạo tiện nghi.");
                return View(model);
            }

            TempData["SuccessMessage"] =
                $"Đã tạo tiện nghi {model.AmenityCode} thành công.";

            return RedirectToAction(nameof(Index));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi tạo tiện nghi {AmenityCode}.", model.AmenityCode);
            ModelState.AddModelError(
                string.Empty,
                "Không thể tạo tiện nghi. Vui lòng thử lại.");
            return View(model);
        }
    }

    [AdminAccess]
    [HttpGet]
    public async Task<IActionResult> GetNextAmenityCode(CancellationToken cancellationToken)
    {
        try
        {
            var code = await _amenityService.GetNextAmenityCodeAsync(cancellationToken);
            return Json(new { code });
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Không thể tự sinh mã tiện nghi.");
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                message = "Không thể tự sinh mã tiện nghi. Vui lòng thử lại."
            });
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

        try
        {
            var model = await _amenityService.GetEditModelAsync(
                id,
                returnUrl,
                cancellationToken);

            return model is null ? NotFound() : View(model);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Không thể mở trang sửa tiện nghi {AmenityId}.", id);
            TempData["ErrorMessage"] = "Không thể mở trang sửa tiện nghi. Vui lòng thử lại.";
            return RedirectToAction(nameof(Index));
        }
    }

    [AdminAccess]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        AmenityEditViewModel model,
        CancellationToken cancellationToken)
    {
        model ??= new AmenityEditViewModel();

        if (model.Id != Guid.Empty)
        {
            var current = await _amenityService.GetEditModelAsync(
                model.Id,
                model.ReturnUrl,
                cancellationToken);

            if (current is not null && current.HasActiveBookings)
            {
                model.AmenityCode = current.AmenityCode;
                model.AmenityName = current.AmenityName;
                model.IsActive = current.IsActive;
                model.HasActiveBookings = true;

                ModelState.Remove(nameof(AmenityEditViewModel.AmenityCode));
                ModelState.Remove(nameof(AmenityEditViewModel.AmenityName));
                ModelState.Remove(nameof(AmenityEditViewModel.IsActive));
            }
        }

        if (!ModelState.IsValid)
            return View(model);

        if (!TryGetCurrentUserId(out var currentUserId))
            return Challenge();

        try
        {
            var result = await _amenityService.UpdateAsync(
                model,
                currentUserId,
                cancellationToken);

            AddFieldErrors(result.FieldErrors);

            if (!result.Succeeded)
            {
                ModelState.AddModelError(
                    string.Empty,
                    result.ErrorMessage ?? "Không thể lưu thay đổi tiện nghi.");
                return View(model);
            }

            TempData["SuccessMessage"] =
                $"Đã cập nhật tiện nghi {model.AmenityCode} thành công.";

            return RedirectToLocalReturnUrl(model.ReturnUrl);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi sửa tiện nghi {AmenityId}.", model.Id);
            ModelState.AddModelError(
                string.Empty,
                "Không thể lưu thay đổi tiện nghi. Vui lòng thử lại.");
            return View(model);
        }
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

        var result = await _amenityService.DeactivateAsync(
            id,
            currentUserId,
            cancellationToken);

        TempData[result.Succeeded ? "SuccessMessage" : "ErrorMessage"] =
            result.Succeeded
                ? "Đã dừng hoạt động tiện nghi và các tiện nghi tương ứng trong loại phòng đã được dừng hoạt động."
                : result.ErrorMessage ?? "Không thể dừng hoạt động tiện nghi.";

        return RedirectToLocalReturnUrl(returnUrl);
    }

    [AdminAccess]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Activate(
        Guid id,
        bool reactivateAssociations,
        string? returnUrl,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var currentUserId))
            return Challenge();

        var result = await _amenityService.ActivateAsync(
            id,
            reactivateAssociations,
            currentUserId,
            cancellationToken);

        TempData[result.Succeeded ? "SuccessMessage" : "ErrorMessage"] =
            result.Succeeded
                ? (result.ReopenedAssociationCount > 0
                    ? $"Đã mở hoạt động tiện nghi và mở lại {result.ReopenedAssociationCount} tiện nghi trong loại phòng."
                    : "Đã mở hoạt động tiện nghi.")
                : result.ErrorMessage ?? "Không thể mở hoạt động tiện nghi.";

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

        var result = await _amenityService.DeleteAsync(
            id,
            currentUserId,
            cancellationToken);

        TempData[result.Succeeded ? "SuccessMessage" : "ErrorMessage"] =
            result.Succeeded
                ? "Đã xóa tiện nghi thành công."
                : result.ErrorMessage ?? "Không thể xóa tiện nghi.";

        return RedirectToLocalReturnUrl(returnUrl);
    }

    private void AddFieldErrors(
        IReadOnlyDictionary<string, string> fieldErrors)
    {
        foreach (var item in fieldErrors)
            ModelState.AddModelError(item.Key, item.Value);
    }

    private bool TryGetCurrentUserId(out Guid userId)
    {
        userId = Guid.Empty;
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out userId) && userId != Guid.Empty;
    }

    private IActionResult RedirectToLocalReturnUrl(string? returnUrl)
    {
        return !string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl)
            ? Redirect(returnUrl)
            : RedirectToAction(nameof(Index));
    }
}
