using System.Globalization;
using System.Security.Claims;
using Hotel_Management_Website.Filters;
using Hotel_Management_Website.IServices;
using Hotel_Management_Website.Models.ViewModels.Service;
using Hotel_Management_Website.Models.ViewModels.UI;
using Microsoft.AspNetCore.Mvc;

namespace Hotel_Management_Website.Controllers;

[ReceptionAccess]
public sealed class ServicesController : Controller
{
    private readonly IServiceManagementService _service;
    private readonly ILogger<ServicesController> _logger;

    public ServicesController(IServiceManagementService service, ILogger<ServicesController> logger)
    {
        _service = service;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index(ServiceIndexQueryViewModel query, CancellationToken cancellationToken)
    {
        query ??= new ServiceIndexQueryViewModel();
        query.Filter ??= new ServiceListFilterViewModel();

        if (string.Equals(query.Reset, "service", StringComparison.OrdinalIgnoreCase))
            query.Filter = new ServiceListFilterViewModel();

        try
        {
            var model = await _service.GetIndexAsync(query.Filter, cancellationToken);
            model.Pagination = new PaginationViewModel
            {
                CurrentPage = model.Pagination.CurrentPage,
                PageSize = model.Pagination.PageSize,
                TotalItems = model.Pagination.TotalItems,
                PageParam = model.Pagination.PageParam,
                PageSizeParam = model.Pagination.PageSizeParam,
                BaseUrl = Url.Action(nameof(Index), "Services") ?? "/Services/Index",
                ItemLabel = "dịch vụ",
                QueryParameters = new Dictionary<string, string?>
                {
                    ["Filter.Search"] = model.Filter.Search,
                    ["Filter.Sort"] = ((byte)model.Filter.Sort).ToString(CultureInfo.InvariantCulture),
                    ["Filter.Status"] = model.Filter.Status.HasValue ? (model.Filter.Status.Value ? "true" : "false") : null,
                    ["Filter.PriceFilter"] = ((byte)model.Filter.PriceFilter).ToString(CultureInfo.InvariantCulture),
                    ["Filter.CurrentPage"] = model.Filter.CurrentPage.ToString(CultureInfo.InvariantCulture),
                    ["Filter.PageSize"] = model.Filter.PageSize.ToString(CultureInfo.InvariantCulture)
                }
            };
            return View(model);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Không thể tải danh sách dịch vụ.");
            return View(new ServiceIndexViewModel
            {
                Filter = query.Filter,
                Services = Array.Empty<ServiceListItemViewModel>(),
                LoadError = "Không thể tải danh sách dịch vụ. Vui lòng thử lại."
            });
        }
    }

    [HttpGet]
    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty) return NotFound();
        try
        {
            var model = await _service.GetDetailsAsync(id, cancellationToken);
            return model is null ? NotFound() : View(model);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi tải chi tiết dịch vụ {ServiceId}.", id);
            TempData["ErrorMessage"] = "Không thể tải chi tiết dịch vụ. Vui lòng thử lại.";
            return RedirectToAction(nameof(Index));
        }
    }

    [AdminAccess]
    [HttpGet]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        try { return View(await _service.PrepareCreateAsync(cancellationToken)); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Không thể mở trang tạo dịch vụ.");
            TempData["ErrorMessage"] = "Không thể mở trang tạo dịch vụ. Vui lòng thử lại.";
            return RedirectToAction(nameof(Index));
        }
    }

    [AdminAccess]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ServiceCreateViewModel model, CancellationToken cancellationToken)
    {
        model ??= new ServiceCreateViewModel();
        model.ServiceCode = (model.ServiceCode ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(model.ServiceCode))
        {
            model.ServiceCode = await _service.GetNextServiceCodeAsync(cancellationToken);
            ModelState.Remove(nameof(ServiceCreateViewModel.ServiceCode));
        }

        if (!ModelState.IsValid) return View(model);
        if (!TryGetCurrentUserId(out var currentUserId)) return Challenge();

        try
        {
            var result = await _service.CreateAsync(model, currentUserId, cancellationToken);
            AddFieldErrors(result.FieldErrors);
            if (!result.Succeeded)
            {
                ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Không thể tạo dịch vụ.");
                return View(model);
            }
            TempData["SuccessMessage"] = $"Đã tạo dịch vụ {model.ServiceCode} thành công.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi tạo dịch vụ {ServiceCode}.", model.ServiceCode);
            ModelState.AddModelError(string.Empty, "Không thể tạo dịch vụ. Vui lòng thử lại.");
            return View(model);
        }
    }

    [AdminAccess]
    [HttpGet]
    public async Task<IActionResult> Edit(Guid id, string? returnUrl, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty) return NotFound();
        try
        {
            var model = await _service.GetEditModelAsync(id, returnUrl, cancellationToken);
            return model is null ? NotFound() : View(model);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Không thể mở trang sửa dịch vụ {ServiceId}.", id);
            TempData["ErrorMessage"] = "Không thể mở trang sửa dịch vụ. Vui lòng thử lại.";
            return RedirectToAction(nameof(Index));
        }
    }

    [AdminAccess]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(ServiceEditViewModel model, CancellationToken cancellationToken)
    {
        model ??= new ServiceEditViewModel();

        if (model.Id != Guid.Empty)
        {
            var current = await _service.GetEditModelAsync(model.Id, model.ReturnUrl, cancellationToken);
            if (current is not null && current.HasActiveBookings)
            {
                model.ServiceCode = current.ServiceCode;
                model.ServiceName = current.ServiceName;
                model.UnitPrice = current.UnitPrice;
                model.PriceUnitType = current.PriceUnitType;
                model.IsActive = current.IsActive;
                model.HasActiveBookings = true;
                ModelState.Remove(nameof(ServiceEditViewModel.ServiceCode));
                ModelState.Remove(nameof(ServiceEditViewModel.ServiceName));
                ModelState.Remove(nameof(ServiceEditViewModel.UnitPrice));
                ModelState.Remove(nameof(ServiceEditViewModel.PriceUnitType));
                ModelState.Remove(nameof(ServiceEditViewModel.IsActive));
            }
        }

        if (!ModelState.IsValid) return View(model);
        if (!TryGetCurrentUserId(out var currentUserId)) return Challenge();

        try
        {
            var result = await _service.UpdateAsync(model, currentUserId, cancellationToken);
            AddFieldErrors(result.FieldErrors);
            if (!result.Succeeded)
            {
                ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Không thể lưu thay đổi dịch vụ.");
                return View(model);
            }
            TempData["SuccessMessage"] = $"Đã cập nhật dịch vụ {model.ServiceCode} thành công.";
            return RedirectToLocalReturnUrl(model.ReturnUrl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi sửa dịch vụ {ServiceId}.", model.Id);
            ModelState.AddModelError(string.Empty, "Không thể lưu thay đổi dịch vụ. Vui lòng thử lại.");
            return View(model);
        }
    }

    [AdminAccess]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Deactivate(Guid id, string? returnUrl, CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var currentUserId)) return Challenge();
        var result = await _service.DeactivateAsync(id, currentUserId, cancellationToken);
        TempData[result.Succeeded ? "SuccessMessage" : "ErrorMessage"] = result.Succeeded
            ? "Đã dừng hoạt động dịch vụ và cập nhật các liên kết tương ứng."
            : result.ErrorMessage ?? "Không thể dừng hoạt động dịch vụ.";
        return RedirectToLocalReturnUrl(returnUrl);
    }

    [AdminAccess]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Activate(Guid id, bool reactivateAssociations, string? returnUrl, CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var currentUserId)) return Challenge();
        var result = await _service.ActivateAsync(id, reactivateAssociations, currentUserId, cancellationToken);
        TempData[result.Succeeded ? "SuccessMessage" : "ErrorMessage"] = result.Succeeded
            ? result.ReopenedAssociationCount > 0
                ? $"Đã mở hoạt động dịch vụ và mở lại {result.ReopenedAssociationCount} liên kết loại phòng."
                : "Đã mở hoạt động dịch vụ."
            : result.ErrorMessage ?? "Không thể mở hoạt động dịch vụ.";
        return RedirectToLocalReturnUrl(returnUrl);
    }

    [AdminAccess]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id, string? returnUrl, CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var currentUserId)) return Challenge();
        var result = await _service.DeleteAsync(id, currentUserId, cancellationToken);
        TempData[result.Succeeded ? "SuccessMessage" : "ErrorMessage"] = result.Succeeded
            ? "Đã xóa dịch vụ."
            : result.ErrorMessage ?? "Không thể xóa dịch vụ.";
        return RedirectToLocalReturnUrl(returnUrl);
    }

    private void AddFieldErrors(IReadOnlyDictionary<string, string> fieldErrors)
    {
        foreach (var error in fieldErrors)
            ModelState.AddModelError(error.Key, error.Value);
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
