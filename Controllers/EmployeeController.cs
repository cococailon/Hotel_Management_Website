using Hotel_Management_Website.Filters;
using Hotel_Management_Website.IServices;
using Hotel_Management_Website.Models.ViewModels.Employee;
using Hotel_Management_Website.Models.ViewModels.UI;
using Microsoft.AspNetCore.Mvc;
using System.Globalization;
using System.Security.Claims;

namespace Hotel_Management_Website.Controllers;

[AdminAccess]
public sealed class EmployeeController : Controller
{
    private readonly IEmployeeService _employeeService;
    private readonly ILogger<EmployeeController> _logger;

    public EmployeeController(
        IEmployeeService employeeService,
        ILogger<EmployeeController> logger)
    {
        _employeeService = employeeService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        EmployeeIndexQueryViewModel query,
        CancellationToken cancellationToken)
    {
        query ??= new EmployeeIndexQueryViewModel();

        // Mặc định: LastModifiedAt mới nhất + không lọc.
        query.Reception ??= new EmployeeListFilterViewModel();
        query.Housekeeping ??= new EmployeeListFilterViewModel();

        if (string.Equals(query.ResetSection, "reception", StringComparison.OrdinalIgnoreCase))
        {
            query.Reception = new EmployeeListFilterViewModel();
        }
        else if (string.Equals(query.ResetSection, "housekeeping", StringComparison.OrdinalIgnoreCase))
        {
            query.Housekeeping = new EmployeeListFilterViewModel();
        }

        try
        {
            var model = await _employeeService.GetIndexAsync(
                query.Reception,
                query.Housekeeping,
                cancellationToken);

            ApplyPaginationUrls(model);

            return View(model);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi tải danh sách nhân viên.");

            return View(new EmployeeIndexViewModel
            {
                Reception = CreateFallbackSection(0, query.Reception),
                Housekeeping = CreateFallbackSection(1, query.Housekeeping),
                LoadError = "Không thể tải danh sách nhân viên. Vui lòng thử lại."
            });
        }
    }

    private void ApplyPaginationUrls(EmployeeIndexViewModel model)
    {
        var baseUrl = Url.Action(nameof(Index), "Employee") ?? "/Employee/Index";

        var queryParameters = BuildPaginationParameters(
            model.Reception,
            model.Housekeeping);

        model.Reception.Pagination = CreatePagination(
            model.Reception.Pagination,
            baseUrl,
            queryParameters);

        model.Housekeeping.Pagination = CreatePagination(
            model.Housekeeping.Pagination,
            baseUrl,
            queryParameters);
    }

    private static PaginationViewModel CreatePagination(
        PaginationViewModel pagination,
        string baseUrl,
        IReadOnlyDictionary<string, string?> queryParameters)
    {
        return new PaginationViewModel
        {
            CurrentPage = pagination.CurrentPage,
            PageSize = pagination.PageSize,
            TotalItems = pagination.TotalItems,
            PageParam = pagination.PageParam,
            PageSizeParam = pagination.PageSizeParam,
            BaseUrl = baseUrl,
            QueryParameters = queryParameters
        };
    }

    private static IReadOnlyDictionary<string, string?> BuildPaginationParameters(
        EmployeeListSectionViewModel reception,
        EmployeeListSectionViewModel housekeeping)
    {
        return new Dictionary<string, string?>
        {
            ["Reception.Search"] = reception.Filter.Search,
            ["Reception.Sort"] = ((byte)reception.Filter.Sort).ToString(CultureInfo.InvariantCulture),
            ["Reception.Sex"] = reception.Filter.Sex?.ToString(CultureInfo.InvariantCulture),
            ["Reception.Status"] = reception.Filter.Status?.ToString(CultureInfo.InvariantCulture),
            ["Reception.CurrentPage"] = reception.Filter.CurrentPage.ToString(CultureInfo.InvariantCulture),
            ["Reception.PageSize"] = reception.Filter.PageSize.ToString(CultureInfo.InvariantCulture),

            ["Housekeeping.Search"] = housekeeping.Filter.Search,
            ["Housekeeping.Sort"] = ((byte)housekeeping.Filter.Sort).ToString(CultureInfo.InvariantCulture),
            ["Housekeeping.Sex"] = housekeeping.Filter.Sex?.ToString(CultureInfo.InvariantCulture),
            ["Housekeeping.Status"] = housekeeping.Filter.Status?.ToString(CultureInfo.InvariantCulture),
            ["Housekeeping.CurrentPage"] = housekeeping.Filter.CurrentPage.ToString(CultureInfo.InvariantCulture),
            ["Housekeeping.PageSize"] = housekeeping.Filter.PageSize.ToString(CultureInfo.InvariantCulture)
        };
    }

    private static EmployeeListSectionViewModel CreateFallbackSection(
        byte position,
        EmployeeListFilterViewModel filter)
    {
        return new EmployeeListSectionViewModel
        {
            Position = position,
            Title = position == 0 ? "Lễ Tân" : "Buồng Phòng",
            Description = position == 0
                ? "Nhân viên thuộc bộ phận lễ tân."
                : "Nhân viên thuộc bộ phận buồng phòng.",
            Filter = filter,
            Employees = Array.Empty<EmployeeListItemViewModel>()
        };
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
            var employee = await _employeeService.GetDetailsAsync(
                id,
                cancellationToken);

            if (employee is null)
            {
                return NotFound();
            }

            return View(employee);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Lỗi khi tải chi tiết nhân viên {EmployeeId}.",
                id);

            TempData["ErrorMessage"] =
                "Không thể tải chi tiết nhân viên. Vui lòng thử lại.";

            return RedirectToAction(nameof(Index));
        }
    }

    [HttpGet]
    public async Task<IActionResult> Create(
    byte? position,
    CancellationToken cancellationToken)
    {
        var normalizedPosition = position is 1
            ? (byte)1
            : (byte)0;

        try
        {
            var model = await _employeeService.GetCreateModelAsync(
                normalizedPosition,
                cancellationToken);

            return View(model);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Lỗi khi chuẩn bị trang tạo nhân viên.");

            TempData["ErrorMessage"] =
                "Không thể tải trang tạo nhân viên. Vui lòng thử lại.";

            return RedirectToAction(nameof(Index));
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        EmployeeCreateViewModel model,
        CancellationToken cancellationToken)
    {
        model ??= new EmployeeCreateViewModel();

        // Chuẩn hóa mã trước khi kiểm tra ModelState.
        model.EmployeeCode =
            (model.EmployeeCode ?? string.Empty)
                .Trim()
                .ToUpperInvariant();

        // Nếu người dùng xóa mã tự sinh, hệ thống tự sinh lại.
        if (string.IsNullOrWhiteSpace(model.EmployeeCode))
        {
            model.EmployeeCode = await _employeeService.GetNextEmployeeCodeAsync(
                cancellationToken);

            // Bỏ lỗi Required cũ do model binder tạo ra cho EmployeeCode.
            ModelState.Remove(nameof(EmployeeCreateViewModel.EmployeeCode));
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Challenge();
        }

        try
        {
            var result = await _employeeService.CreateAsync(
                model,
                currentUserId,
                cancellationToken);

            if (!result.Succeeded)
            {
                ModelState.AddModelError(
                    nameof(EmployeeCreateViewModel.EmployeeCode),
                    result.ErrorMessage ?? "Không thể tạo nhân viên.");

                return View(model);
            }

            TempData["SuccessMessage"] =
                $"Đã tạo nhân viên {model.EmployeeCode} thành công.";

            return RedirectToAction(nameof(Index));
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Lỗi khi tạo nhân viên với mã {EmployeeCode}.",
                model.EmployeeCode);

            ModelState.AddModelError(
                string.Empty,
                "Không thể tạo nhân viên. Vui lòng thử lại.");

            return View(model);
        }
    }

    private bool TryGetCurrentUserId(out Guid userId)
    {
        userId = Guid.Empty;

        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);

        return Guid.TryParse(value, out userId)
               && userId != Guid.Empty;
    }

    [HttpGet]
    public async Task<IActionResult> Edit(
    Guid id,
    string? returnUrl,
    CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
        {
            return NotFound();
        }

        var model = await _employeeService.GetEditModelAsync(
            id,
            returnUrl,
            cancellationToken);

        if (model is null)
        {
            return NotFound();
        }

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        EmployeeEditViewModel model,
        CancellationToken cancellationToken)
    {
        model ??= new EmployeeEditViewModel();

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Challenge();
        }

        var result = await _employeeService.UpdateAsync(
            model,
            currentUserId,
            cancellationToken);

        if (!result.Succeeded)
        {
            ModelState.AddModelError(
                string.Empty,
                result.ErrorMessage ?? "Không thể lưu thay đổi nhân viên.");

            return View(model);
        }

        TempData["SuccessMessage"] =
            $"Đã cập nhật nhân viên {model.EmployeeCode} thành công.";

        if (!string.IsNullOrWhiteSpace(model.ReturnUrl) &&
            Url.IsLocalUrl(model.ReturnUrl))
        {
            return Redirect(model.ReturnUrl);
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Leave(
    Guid id,
    string? returnUrl,
    CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
        {
            TempData["ErrorMessage"] = "Nhân viên không hợp lệ.";
            return RedirectToAction(nameof(Index));
        }

        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Challenge();
        }

        var result = await _employeeService.LeaveAsync(
            id,
            currentUserId,
            cancellationToken);

        if (!result.Succeeded)
        {
            TempData["ErrorMessage"] =
                result.ErrorMessage ?? "Không thể cho nhân viên nghỉ việc.";
        }
        else
        {
            TempData["SuccessMessage"] = result.DeactivatedUserCount > 0
                ? $"Đã cho nhân viên nghỉ việc và dừng hoạt động {result.DeactivatedUserCount} tài khoản liên kết."
                : "Đã cho nhân viên nghỉ việc.";
        }

        if (!string.IsNullOrWhiteSpace(returnUrl) &&
            Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReturnToWork(
    Guid id,
    string? returnUrl,
    CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
        {
            TempData["ErrorMessage"] = "Nhân viên không hợp lệ.";
            return RedirectToAction(nameof(Index));
        }

        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Challenge();
        }

        var result = await _employeeService.ReturnToWorkAsync(
            id,
            currentUserId,
            cancellationToken);

        if (!result.Succeeded)
        {
            TempData["ErrorMessage"] =
                result.ErrorMessage ?? "Không thể cho nhân viên trở lại làm việc.";
        }
        else
        {
            TempData["SuccessMessage"] = result.ActivatedUserCount > 0
                ? $"Đã cho nhân viên trở lại làm việc và kích hoạt {result.ActivatedUserCount} tài khoản liên kết."
                : "Đã cho nhân viên trở lại làm việc.";
        }

        if (!string.IsNullOrWhiteSpace(returnUrl) &&
            Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(
    Guid id,
    string? returnUrl,
    CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
        {
            TempData["ErrorMessage"] = "Nhân viên không hợp lệ.";
            return RedirectToAction(nameof(Index));
        }

        if (!TryGetCurrentUserId(out var currentUserId))
        {
            return Challenge();
        }

        var result = await _employeeService.DeleteAsync(
            id,
            currentUserId,
            cancellationToken);

        if (!result.Succeeded)
        {
            TempData["ErrorMessage"] =
                result.ErrorMessage ?? "Không thể xóa nhân viên.";
        }
        else
        {
            TempData["SuccessMessage"] = result.DeactivatedUserCount > 0
                ? $"Đã xóa hồ sơ nhân viên và dừng hoạt động {result.DeactivatedUserCount} tài khoản liên kết."
                : "Đã xóa hồ sơ nhân viên.";
        }

        if (!string.IsNullOrWhiteSpace(returnUrl) &&
            Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return RedirectToAction(nameof(Index));
    }
}
