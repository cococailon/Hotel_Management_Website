using Hotel_Management_Website.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Hotel_Management_Website.Filters;

[AttributeUsage(
    AttributeTargets.Class | AttributeTargets.Method,
    AllowMultiple = false,
    Inherited = true)]
public sealed class AdminAccessAttribute : Attribute, IAsyncAuthorizationFilter
{
    public Task OnAuthorizationAsync(
        AuthorizationFilterContext context)
    {
        var user = context.HttpContext.User;

        // =========================================================
        // 1. CHƯA ĐĂNG NHẬP
        // =========================================================

        if (user.Identity?.IsAuthenticated != true)
        {
            var returnUrl =
                context.HttpContext.Request.PathBase +
                context.HttpContext.Request.Path +
                context.HttpContext.Request.QueryString;

            context.Result = new RedirectToActionResult(
                "Login",
                "Account",
                new
                {
                    ReturnUrl = returnUrl
                });

            return Task.CompletedTask;
        }


        // =========================================================
        // 2. ĐÃ ĐĂNG NHẬP NHƯNG KHÔNG PHẢI ADMIN
        // =========================================================

        if (user.IsInRole(AppRoles.Admin))
        {
            return Task.CompletedTask;
        }


        // =========================================================
        // 3. LỄ TÂN
        // =========================================================

        if (user.IsInRole(AppRoles.Reception))
        {
            context.Result = new RedirectToActionResult(
                "Index",
                "Reception",
                null);

            return Task.CompletedTask;
        }


        // =========================================================
        // 4. BUỒNG PHÒNG
        // =========================================================

        if (user.IsInRole(AppRoles.Housekeeping))
        {
            context.Result = new RedirectToActionResult(
                "Index",
                "Housekeeping",
                null);

            return Task.CompletedTask;
        }


        // =========================================================
        // 5. ROLE KHÔNG HỢP LỆ
        // =========================================================

        context.Result = new RedirectToActionResult(
            "Login",
            "Account",
            null);

        return Task.CompletedTask;
    }
}