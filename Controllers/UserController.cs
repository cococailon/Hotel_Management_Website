using System.Globalization;
using System.Security.Claims;
using Hotel_Management_Website.Filters;
using Hotel_Management_Website.IServices;
using Hotel_Management_Website.Models.ViewModels.User;
using Hotel_Management_Website.Security;
using Microsoft.AspNetCore.Mvc;

namespace Hotel_Management_Website.Controllers;

[AdminAccess]
public sealed class UserController : Controller
{
    private readonly IUserService _userService;
    private readonly ILogger<UserController> _logger;

    public UserController(
        IUserService userService,
        ILogger<UserController> logger)
    {
        _userService = userService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        UserIndexQueryViewModel model,
        CancellationToken cancellationToken)
    {
        var currentUserId = GetCurrentUserId();
        if (!currentUserId.HasValue)
            return RedirectToAction("Login", "Account");

        var currentRole = await _userService.GetUserRoleCodeAsync(
            currentUserId.Value,
            cancellationToken);

        if (currentRole is not 0 and not 4)
            return RedirectToRoleHome();

        try
        {
            if (string.Equals(model.ResetSection, "admin", StringComparison.OrdinalIgnoreCase))
                model.Admin = new UserListFilterViewModel();
            else if (string.Equals(model.ResetSection, "reception", StringComparison.OrdinalIgnoreCase))
                model.Reception = new UserListFilterViewModel();
            else if (string.Equals(model.ResetSection, "housekeeping", StringComparison.OrdinalIgnoreCase))
                model.Housekeeping = new UserListFilterViewModel();

            var result = await _userService.GetIndexAsync(
                currentUserId.Value,
                currentRole.Value,
                NormalizeFilter(model.Admin),
                NormalizeFilter(model.Reception),
                NormalizeFilter(model.Housekeeping),
                cancellationToken);

            ApplyPaginationUrls(result);

            return View(result);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Không thể tải danh sách tài khoản.");

            return View(new UserIndexViewModel
            {
                IsFullAdmin = currentRole.Value == 4,
                AdminUsers = CreateEmptySection(0, "Admin", "Tài khoản quản trị hệ thống.", "bi-person-gear"),
                ReceptionUsers = CreateEmptySection(1, "Lễ Tân", "Tài khoản dành cho bộ phận lễ tân.", "bi-person-vcard"),
                HousekeepingUsers = CreateEmptySection(2, "Buồng Phòng", "Tài khoản dành cho bộ phận buồng phòng.", "bi-person-check")
            });
        }
    }

    [HttpGet]
    public async Task<IActionResult> Create(
        byte role = 1,
        CancellationToken cancellationToken = default)
    {
        var currentUserId = GetCurrentUserId();
        if (!currentUserId.HasValue)
            return RedirectToAction("Login", "Account");

        var currentRole = await _userService.GetUserRoleCodeAsync(
            currentUserId.Value,
            cancellationToken);

        if (currentRole is not 0 and not 4)
            return RedirectToRoleHome();

        if (role == 0 && currentRole != 4)
            role = 1;

        if (role is not 0 and not 1 and not 2)
            role = 1;

        try
        {
            var model = await _userService.PrepareCreateAsync(
                currentRole.Value,
                role,
                cancellationToken);

            return View(model);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Không thể tải form tạo tài khoản.");
            TempData["ErrorMessage"] = "Không thể tải form tạo tài khoản. Vui lòng thử lại.";
            return RedirectToAction(nameof(Index));
        }
    }

    [HttpGet]
    public async Task<IActionResult> GenerateUserCode(
        CancellationToken cancellationToken = default)
    {
        var currentUserId = GetCurrentUserId();
        if (!currentUserId.HasValue)
            return Unauthorized();

        var currentRole = await _userService.GetUserRoleCodeAsync(
            currentUserId.Value,
            cancellationToken);

        if (currentRole is not 0 and not 4)
            return Forbid();

        var userCode = await _userService.GenerateNextUserCodeAsync(cancellationToken);
        return Json(new { userCode });
    }

    [HttpGet]
    public async Task<IActionResult> Details(
        Guid id,
        string? returnUrl,
        CancellationToken cancellationToken = default)
    {
        var currentUserId = GetCurrentUserId();
        if (!currentUserId.HasValue)
            return RedirectToAction("Login", "Account");

        var currentRole = await _userService.GetUserRoleCodeAsync(
            currentUserId.Value,
            cancellationToken);

        if (currentRole is not 0 and not 4)
            return RedirectToRoleHome();

        var model = await _userService.GetDetailsAsync(id, cancellationToken);
        if (model is null)
            return NotFound();

        ViewData["ReturnUrl"] = returnUrl;
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(
        Guid id,
        string? returnUrl,
        CancellationToken cancellationToken = default)
    {
        var currentUserId = GetCurrentUserId();
        if (!currentUserId.HasValue)
            return RedirectToAction("Login", "Account");

        var currentRole = await _userService.GetUserRoleCodeAsync(
            currentUserId.Value,
            cancellationToken);

        if (currentRole is not 0 and not 4)
            return RedirectToRoleHome();

        var model = await _userService.GetEditModelAsync(
            id,
            currentUserId.Value,
            currentRole.Value,
            returnUrl,
            cancellationToken);

        if (model is null)
            return NotFound();

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        UserEditViewModel model,
        CancellationToken cancellationToken)
    {
        model ??= new UserEditViewModel();

        var currentUserId = GetCurrentUserId();
        if (!currentUserId.HasValue)
            return RedirectToAction("Login", "Account");

        var currentRole = await _userService.GetUserRoleCodeAsync(
            currentUserId.Value,
            cancellationToken);

        if (currentRole is not 0 and not 4)
            return RedirectToRoleHome();

        model.UserCode ??= string.Empty;
        model.UserName ??= string.Empty;
        model.Note = string.IsNullOrWhiteSpace(model.Note)
            ? null
            : model.Note.Trim();
        model.CanEditAdmin = currentRole.Value == 4;

        if (!ModelState.IsValid)
        {
            model.Employees = await _userService.GetEmployeesForEditAsync(
                model.Role,
                model.EmployeeId,
                cancellationToken);

            return View(model);
        }

        var result = await _userService.UpdateAsync(
            model,
            currentUserId.Value,
            currentRole.Value,
            cancellationToken);

        if (!result.Succeeded)
        {
            AddBusinessValidationError(result.ErrorMessage);
            model.Employees = await _userService.GetEmployeesForEditAsync(
                model.Role,
                model.EmployeeId,
                cancellationToken);

            return View(model);
        }

        TempData["SuccessMessage"] =
            "Đã cập nhật tài khoản thành công.";

        if (!string.IsNullOrWhiteSpace(model.ReturnUrl) &&
            Url.IsLocalUrl(model.ReturnUrl))
        {
            return Redirect(model.ReturnUrl);
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        UserCreateViewModel model,
        CancellationToken cancellationToken)
    {
        var currentUserId = GetCurrentUserId();
        if (!currentUserId.HasValue)
            return RedirectToAction("Login", "Account");

        var currentRole = await _userService.GetUserRoleCodeAsync(
            currentUserId.Value,
            cancellationToken);

        if (currentRole is not 0 and not 4)
            return RedirectToRoleHome();

        model.UserCode ??= string.Empty;
        model.UserName ??= string.Empty;
        model.CanCreateAdmin = currentRole.Value == 4;

        if (!ModelState.IsValid)
        {
            await ReloadEmployeeOptionsAsync(model, cancellationToken);
            return View(model);
        }

        var result = await _userService.CreateAsync(
            model,
            currentUserId.Value,
            currentRole.Value,
            cancellationToken);

        if (!result.Succeeded)
        {
            AddBusinessValidationError(result.ErrorMessage);

            await ReloadEmployeeOptionsAsync(model, cancellationToken);
            return View(model);
        }

        var employeeLabel = string.Empty;
        if (model.EmployeeId.HasValue)
        {
            employeeLabel = await _userService.GetEmployeeDisplayNameAsync(
                model.EmployeeId.Value,
                cancellationToken) ?? string.Empty;
        }

        var createdUserCode = result.UserCode ?? model.UserCode;

        TempData["SuccessMessage"] = string.IsNullOrWhiteSpace(employeeLabel)
            ? $"Đã tạo tài khoản {createdUserCode}."
            : $"Đã tạo tài khoản {createdUserCode} cho nhân viên {employeeLabel}.";

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Lock(
        Guid id,
        string? returnUrl,
        CancellationToken cancellationToken = default)
    {
        var currentUserId = GetCurrentUserId();
        if (!currentUserId.HasValue)
            return RedirectToAction("Login", "Account");

        var currentRole = await _userService.GetUserRoleCodeAsync(
            currentUserId.Value,
            cancellationToken);

        if (currentRole is not 0 and not 4)
            return RedirectToRoleHome();

        var result = await _userService.LockAsync(
            id,
            currentUserId.Value,
            currentRole.Value,
            cancellationToken);

        TempData[result.Succeeded ? "SuccessMessage" : "ErrorMessage"] =
            result.Succeeded
                ? "Đã khóa tài khoản thành công."
                : result.ErrorMessage ?? "Không thể khóa tài khoản.";

        return RedirectToUserReturnUrl(returnUrl);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reopen(
        Guid id,
        string? returnUrl,
        CancellationToken cancellationToken = default)
    {
        var currentUserId = GetCurrentUserId();
        if (!currentUserId.HasValue)
            return RedirectToAction("Login", "Account");

        var currentRole = await _userService.GetUserRoleCodeAsync(
            currentUserId.Value,
            cancellationToken);

        if (currentRole is not 0 and not 4)
            return RedirectToRoleHome();

        var result = await _userService.ReopenAsync(
            id,
            currentUserId.Value,
            currentRole.Value,
            cancellationToken);

        TempData[result.Succeeded ? "SuccessMessage" : "ErrorMessage"] =
            result.Succeeded
                ? "Đã mở lại tài khoản thành công."
                : result.ErrorMessage ?? "Không thể mở lại tài khoản.";

        return RedirectToUserReturnUrl(returnUrl);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(
        Guid id,
        string? returnUrl,
        CancellationToken cancellationToken = default)
    {
        var currentUserId = GetCurrentUserId();
        if (!currentUserId.HasValue)
            return RedirectToAction("Login", "Account");

        var currentRole = await _userService.GetUserRoleCodeAsync(
            currentUserId.Value,
            cancellationToken);

        if (currentRole is not 0 and not 4)
            return RedirectToRoleHome();

        var result = await _userService.DeleteAsync(
            id,
            currentUserId.Value,
            currentRole.Value,
            cancellationToken);

        TempData[result.Succeeded ? "SuccessMessage" : "ErrorMessage"] =
            result.Succeeded
                ? "Đã xóa tài khoản thành công."
                : result.ErrorMessage ?? "Không thể xóa tài khoản.";

        return RedirectToUserReturnUrl(returnUrl);
    }

    private IActionResult RedirectToUserReturnUrl(string? returnUrl)
    {
        if (!string.IsNullOrWhiteSpace(returnUrl) &&
            Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return RedirectToAction(nameof(Index));
    }

    private void ApplyPaginationUrls(UserIndexViewModel model)
    {
        var baseUrl = Url.Action(nameof(Index), "User") ?? "/User/Index";

        var queryParameters = BuildPaginationParameters(
            model.AdminUsers,
            model.ReceptionUsers,
            model.HousekeepingUsers);

        model.AdminUsers.Pagination = CreatePagination(
            model.AdminUsers.Pagination,
            baseUrl,
            queryParameters);

        model.ReceptionUsers.Pagination = CreatePagination(
            model.ReceptionUsers.Pagination,
            baseUrl,
            queryParameters);

        model.HousekeepingUsers.Pagination = CreatePagination(
            model.HousekeepingUsers.Pagination,
            baseUrl,
            queryParameters);
    }

    private static Hotel_Management_Website.Models.ViewModels.UI.PaginationViewModel CreatePagination(
        Hotel_Management_Website.Models.ViewModels.UI.PaginationViewModel pagination,
        string baseUrl,
        IReadOnlyDictionary<string, string?> queryParameters)
    {
        return new Hotel_Management_Website.Models.ViewModels.UI.PaginationViewModel
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
        UserListSectionViewModel admin,
        UserListSectionViewModel reception,
        UserListSectionViewModel housekeeping)
    {
        return new Dictionary<string, string?>
        {
            ["Admin.Search"] = admin.Filter.Search,
            ["Admin.Sort"] = ((byte)admin.Filter.Sort).ToString(CultureInfo.InvariantCulture),
            ["Admin.IsActive"] = admin.Filter.IsActive?.ToString(CultureInfo.InvariantCulture),
            ["Admin.CurrentPage"] = admin.Filter.CurrentPage.ToString(CultureInfo.InvariantCulture),
            ["Admin.PageSize"] = admin.Filter.PageSize.ToString(CultureInfo.InvariantCulture),

            ["Reception.Search"] = reception.Filter.Search,
            ["Reception.Sort"] = ((byte)reception.Filter.Sort).ToString(CultureInfo.InvariantCulture),
            ["Reception.IsActive"] = reception.Filter.IsActive?.ToString(CultureInfo.InvariantCulture),
            ["Reception.CurrentPage"] = reception.Filter.CurrentPage.ToString(CultureInfo.InvariantCulture),
            ["Reception.PageSize"] = reception.Filter.PageSize.ToString(CultureInfo.InvariantCulture),

            ["Housekeeping.Search"] = housekeeping.Filter.Search,
            ["Housekeeping.Sort"] = ((byte)housekeeping.Filter.Sort).ToString(CultureInfo.InvariantCulture),
            ["Housekeeping.IsActive"] = housekeeping.Filter.IsActive?.ToString(CultureInfo.InvariantCulture),
            ["Housekeeping.CurrentPage"] = housekeeping.Filter.CurrentPage.ToString(CultureInfo.InvariantCulture),
            ["Housekeeping.PageSize"] = housekeeping.Filter.PageSize.ToString(CultureInfo.InvariantCulture)
        };
    }

    private async Task ReloadEmployeeOptionsAsync(
        UserCreateViewModel model,
        CancellationToken cancellationToken)
    {
        // Create dùng đúng cùng nguồn dữ liệu và cùng cách dựng danh sách
        // nhân viên như Edit User. currentEmployeeId = null vì Create
        // chưa có nhân viên hiện tại cần ưu tiên giữ lại.
        model.Employees = await _userService.GetEmployeesForEditAsync(
            model.Role,
            currentEmployeeId: null,
            cancellationToken);
    }

    private void AddBusinessValidationError(string? errorMessage)
    {
        var message = string.IsNullOrWhiteSpace(errorMessage)
            ? "Không thể lưu tài khoản. Vui lòng kiểm tra lại dữ liệu."
            : errorMessage;

        var key = message.StartsWith("Mã tài khoản", StringComparison.OrdinalIgnoreCase)
            ? nameof(UserCreateViewModel.UserCode)
            : message.StartsWith("Tên đăng nhập", StringComparison.OrdinalIgnoreCase) ||
              message.StartsWith("Vui lòng nhập tên đăng nhập", StringComparison.OrdinalIgnoreCase)
                ? nameof(UserCreateViewModel.UserName)
                : message.StartsWith("Nhân viên", StringComparison.OrdinalIgnoreCase)
                    ? nameof(UserCreateViewModel.EmployeeId)
                    : string.Empty;

        ModelState.AddModelError(key, message);
    }

    private Guid? GetCurrentUserId()
    {
        var text = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(text, out var id) && id != Guid.Empty
            ? id
            : null;
    }

    private IActionResult RedirectToRoleHome()
    {
        return Redirect(
            User.IsInRole(AppRoles.Reception)
                ? "/Reception/Index"
                : "/Housekeeping/Index");
    }

    private static UserListFilterViewModel NormalizeFilter(UserListFilterViewModel? filter)
    {
        return filter ?? new UserListFilterViewModel();
    }

    private static UserListSectionViewModel CreateEmptySection(
        byte role,
        string title,
        string description,
        string icon)
    {
        return new UserListSectionViewModel
        {
            Role = role,
            Title = title,
            Description = description,
            Icon = icon,
            Filter = new UserListFilterViewModel(),
            Users = Array.Empty<UserListItemViewModel>(),
            ShowCreate = false
        };
    }
}
