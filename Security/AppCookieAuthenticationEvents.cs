using Hotel_Management_Website.IServices;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.WebUtilities;
using System.Security.Claims;

namespace Hotel_Management_Website.Security;

public sealed class AppCookieAuthenticationEvents
    : CookieAuthenticationEvents
{
    private readonly IAccountService _accountService;

    public AppCookieAuthenticationEvents(
        IAccountService accountService)
    {
        _accountService = accountService;
    }


    // =============================================================
    // VALIDATE PRINCIPAL
    // =============================================================

    public override async Task ValidatePrincipal(
        CookieValidatePrincipalContext context)
    {
        try
        {
            var userIdText =
                context.Principal?
                    .FindFirstValue(
                        ClaimTypes.NameIdentifier);


            // -----------------------------------------------------
            // INVALID USER ID
            // -----------------------------------------------------

            if (!Guid.TryParse(
                    userIdText,
                    out var userId))
            {
                await RejectPrincipalAsync(context);
                return;
            }


            // -----------------------------------------------------
            // LOAD USER
            // -----------------------------------------------------

            var user =
                await _accountService
                    .GetUserForLoginAsync(
                        context.Principal?
                            .FindFirstValue(
                                ClaimTypes.Name)
                        ?? string.Empty);


            // -----------------------------------------------------
            // UserName trong cookie chưa đủ an toàn để định danh
            // hoàn toàn, nên kiểm tra lại Id.
            //
            // Vì AccountService hiện tại tìm theo UserName,
            // ta vẫn tiếp tục verify Id ở đây.
            // -----------------------------------------------------

            if (user == null ||
                user.Id != userId)
            {
                await RejectPrincipalAsync(context);
                return;
            }


            // -----------------------------------------------------
            // EMPLOYEE
            // -----------------------------------------------------

            Models.Employee? employee = null;

            if (user.EmployeeId.HasValue)
            {
                employee =
                    await _accountService
                        .GetEmployeeAsync(
                            user.EmployeeId.Value);
            }


            // -----------------------------------------------------
            // ACCOUNT VALIDATION
            // -----------------------------------------------------

            if (!_accountService.ValidateAccount(
                    user,
                    employee,
                    out var currentRole))
            {
                await RejectPrincipalAsync(context);
                return;
            }


            // -----------------------------------------------------
            // ROLE
            // -----------------------------------------------------

            var cookieRole =
                context.Principal?
                    .FindFirstValue(
                        ClaimTypes.Role);

            if (!string.Equals(
                    cookieRole,
                    currentRole,
                    StringComparison.Ordinal))
            {
                await RejectPrincipalAsync(context);
                return;
            }

            var cookieRoleCode =
                context.Principal?
                    .FindFirstValue(AppRoles.RoleCodeClaim);

            if (!string.IsNullOrWhiteSpace(cookieRoleCode) &&
                (!byte.TryParse(cookieRoleCode, out var cookieRoleValue) ||
                 cookieRoleValue != user.Role))
            {
                await RejectPrincipalAsync(context);
                return;
            }
        }
        catch
        {
            // Fail closed.
            await RejectPrincipalAsync(context);
        }
    }


    // =============================================================
    // NOT AUTHENTICATED
    // =============================================================

    public override Task RedirectToLogin(
        RedirectContext<CookieAuthenticationOptions> context)
    {
        var returnUrl =
            context.Request.PathBase +
            context.Request.Path +
            context.Request.QueryString;


        var loginUrl =
            QueryHelpers.AddQueryString(
                "/Account/Login",
                "returnUrl",
                returnUrl);


        context.Response.Redirect(
            loginUrl);


        return Task.CompletedTask;
    }


    // =============================================================
    // ACCESS DENIED
    // =============================================================

    public override async Task RedirectToAccessDenied(
        RedirectContext<CookieAuthenticationOptions> context)
    {
        var role =
            context.HttpContext.User
                .FindFirstValue(
                    ClaimTypes.Role);


        if (AppRoles.TryGetHomeUrl(
                role,
                out var homeUrl))
        {
            context.Response.Redirect(homeUrl);
            return;
        }


        await context.HttpContext.SignOutAsync(
            CookieAuthenticationDefaults
                .AuthenticationScheme);


        context.Response.Redirect(
            "/Account/Login");
    }


    // =============================================================
    // REJECT
    // =============================================================

    private static async Task RejectPrincipalAsync(
        CookieValidatePrincipalContext context)
    {
        context.RejectPrincipal();

        await context.HttpContext.SignOutAsync(
            CookieAuthenticationDefaults
                .AuthenticationScheme);
    }
}