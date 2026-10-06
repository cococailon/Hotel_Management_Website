using Hotel_Management_Website.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Hotel_Management_Website.Filters;

[AttributeUsage(
    AttributeTargets.Class | AttributeTargets.Method,
    AllowMultiple = false,
    Inherited = true)]
public sealed class ReceptionOnlyAccessAttribute : Attribute, IAsyncAuthorizationFilter
{
    public Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var user = context.HttpContext.User;

        if (user.Identity?.IsAuthenticated != true)
        {
            var returnUrl =
                context.HttpContext.Request.PathBase +
                context.HttpContext.Request.Path +
                context.HttpContext.Request.QueryString;

            context.Result = new RedirectToActionResult(
                "Login",
                "Account",
                new { ReturnUrl = returnUrl });

            return Task.CompletedTask;
        }

        // Lễ Tân và Admin đều được sử dụng toàn bộ luồng nghiệp vụ Booking.
        if (user.IsInRole(AppRoles.Reception) || user.IsInRole(AppRoles.Admin))
        {
            return Task.CompletedTask;
        }

        if (user.IsInRole(AppRoles.Housekeeping))
        {
            context.Result = new RedirectToActionResult(
                "Index",
                "Housekeeping",
                null);

            return Task.CompletedTask;
        }

        context.Result = new RedirectToActionResult(
            "Login",
            "Account",
            null);

        return Task.CompletedTask;
    }
}
