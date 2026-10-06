using System.Security.Claims;

namespace Hotel_Management_Website.Security;

public static class AppRoles
{
    public const string Admin = "Admin";
    public const string Reception = "Reception";
    public const string Housekeeping = "Housekeeping";
    public const string RoleCodeClaim = "RoleCode";

    public const byte AdminRole = 0;
    public const byte ReceptionRole = 1;
    public const byte HousekeepingRole = 2;
    public const byte FullAdminRole = 4;

    public static bool IsAdmin(byte role) =>
        role == AdminRole || role == FullAdminRole;

    public static bool IsFullAdmin(byte role) =>
        role == FullAdminRole;


    public static bool TryGetRoleName(byte role, out string roleName)
    {
        switch (role)
        {
            case 0:
            case 4:
                roleName = Admin;
                return true;

            case 1:
                roleName = Reception;
                return true;

            case 2:
                roleName = Housekeeping;
                return true;

            default:
                roleName = string.Empty;
                return false;
        }
    }


    public static bool TryGetHomeUrl(string? roleName, out string url)
    {
        switch (roleName)
        {
            case Admin:
                url = "/Admin/Index";
                return true;

            case Reception:
                url = "/Reception/Index";
                return true;

            case Housekeeping:
                url = "/Housekeeping/Index";
                return true;

            default:
                url = "/Account/Login";
                return false;
        }
    }


    public static string GetHomeUrl(ClaimsPrincipal principal)
    {
        var role = principal.FindFirstValue(ClaimTypes.Role);

        return TryGetHomeUrl(role, out var url)
            ? url
            : "/Account/Login";
    }
}