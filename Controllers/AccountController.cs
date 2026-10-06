using Hotel_Management_Website.IServices;
using Hotel_Management_Website.Models.ViewModels;
using Hotel_Management_Website.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Hotel_Management_Website.Controllers;

[AllowAnonymous]
public class AccountController : Controller
{
    private readonly IAccountService _accountService;
    private readonly ILogger<AccountController> _logger;

    public AccountController(
        IAccountService accountService,
        ILogger<AccountController> logger)
    {
        _accountService = accountService;
        _logger = logger;
    }


    // =============================================================
    // GET: /Account/Login
    // =============================================================

    [HttpGet]
    [ResponseCache(
        Duration = 0,
        Location = ResponseCacheLocation.None,
        NoStore = true)]
    public IActionResult Login(
        string? returnUrl = null)
    {
        // Đã đăng nhập thì không quay lại Login
        if (User.Identity?.IsAuthenticated == true)
        {
            return Redirect(
                AppRoles.GetHomeUrl(User));
        }


        return View(
            new LoginViewModel
            {
                ReturnUrl = Url.IsLocalUrl(returnUrl)
                    ? returnUrl
                    : null
            });
    }


    // =============================================================
    // POST: /Account/Login
    // =============================================================

    [HttpPost]
    [ValidateAntiForgeryToken]
    [ResponseCache(
        Duration = 0,
        Location = ResponseCacheLocation.None,
        NoStore = true)]
    public async Task<IActionResult> Login(
        LoginViewModel model)
    {
        // ---------------------------------------------------------
        // Đã đăng nhập
        // ---------------------------------------------------------

        if (User.Identity?.IsAuthenticated == true)
        {
            return Redirect(
                AppRoles.GetHomeUrl(User));
        }


        // ---------------------------------------------------------
        // Model validation
        // ---------------------------------------------------------

        if (!ModelState.IsValid)
        {
            return View(model);
        }


        var userName =
            model.UserName.Trim();


        try
        {
            // =====================================================
            // USER
            // =====================================================

            var user =
                await _accountService
                    .GetUserForLoginAsync(userName);


            if (user == null)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "Tên đăng nhập hoặc mật khẩu không đúng.");

                return View(model);
            }


            // =====================================================
            // PASSWORD
            // =====================================================

            var passwordValid =
                _accountService.VerifyPassword(
                    user,
                    model.Password);


            if (!passwordValid)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "Tên đăng nhập hoặc mật khẩu không đúng.");

                return View(model);
            }


            // =====================================================
            // EMPLOYEE
            // =====================================================

            var employee = null as Models.Employee;

            if (user.EmployeeId.HasValue)
            {
                employee =
                    await _accountService
                        .GetEmployeeAsync(
                            user.EmployeeId.Value);
            }


            // =====================================================
            // ACCOUNT VALIDATION
            // =====================================================

            if (!_accountService.ValidateAccount(
                    user,
                    employee,
                    out var roleName))
            {
                ModelState.AddModelError(
                    string.Empty,
                    "Tên đăng nhập hoặc mật khẩu không đúng.");

                return View(model);
            }


            // =====================================================
            // CLAIMS
            // =====================================================

            var fullName =
                employee?.EmployeeName
                ?? user.UserName;


            var claims = new List<Claim>
            {
                new(
                    ClaimTypes.NameIdentifier,
                    user.Id.ToString()),

                new(
                    ClaimTypes.Name,
                    user.UserName),

                new(
                    ClaimTypes.Role,
                    roleName),

                new(
                    AppRoles.RoleCodeClaim,
                    user.Role.ToString()),

                new(
                    "FullName",
                    fullName)
            };


            var identity =
                new ClaimsIdentity(
                    claims,
                    CookieAuthenticationDefaults
                        .AuthenticationScheme);


            var principal =
                new ClaimsPrincipal(identity);


            // =====================================================
            // COOKIE
            // =====================================================

            var properties =
                new AuthenticationProperties
                {
                    IsPersistent = false,
                    AllowRefresh = true
                };


            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults
                    .AuthenticationScheme,
                principal,
                properties);


            // =====================================================
            // RETURN URL
            // =====================================================

            if (Url.IsLocalUrl(model.ReturnUrl))
            {
                return Redirect(
                    model.ReturnUrl!);
            }


            // =====================================================
            // ROLE HOME
            // =====================================================

            return Redirect(
                _accountService.GetRoleHomeUrl(
                    roleName));
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Lỗi đăng nhập cho UserName {UserName}",
                userName);

            ModelState.AddModelError(
                string.Empty,
                "Hệ thống không thể xử lý đăng nhập lúc này. Vui lòng thử lại.");

            return View(model);
        }
    }


    // =============================================================
    // POST: /Account/Logout
    // =============================================================

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(
            CookieAuthenticationDefaults
                .AuthenticationScheme);

        return RedirectToAction(
            nameof(Login));
    }
}