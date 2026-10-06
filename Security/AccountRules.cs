using Hotel_Management_Website.Models;

namespace Hotel_Management_Website.Security;

public static class AccountRules
{
    public static bool Validate(
        User user,
        Employee? employee,
        out string roleName)
    {
        roleName = string.Empty;

        // =========================================================
        // USER STATUS
        // =========================================================

        if (user.IsDeleted)
            return false;

        if (!user.IsActive)
            return false;


        // =========================================================
        // EMPLOYEE STATUS
        // =========================================================

        // Nếu User có gắn Employee thì Employee bắt buộc
        // phải tồn tại và đang làm việc.
        if (user.EmployeeId.HasValue)
        {
            if (employee == null)
                return false;

            if (employee.IsDeleted)
                return false;

            if (employee.Status != 1)
                return false;
        }


        // =========================================================
        // ROLE
        // =========================================================

        switch (user.Role)
        {
            // -----------------------------------------------------
            // ADMIN
            // -----------------------------------------------------
            case 0:
            case 4:
                roleName = AppRoles.Admin;
                return true;


            // -----------------------------------------------------
            // LỄ TÂN
            // -----------------------------------------------------
            case 1:

                if (employee == null)
                    return false;

                // Position 0 = Lễ Tân
                if (employee.Position != 0)
                    return false;

                roleName = AppRoles.Reception;
                return true;


            // -----------------------------------------------------
            // BUỒNG PHÒNG
            // -----------------------------------------------------
            case 2:

                if (employee == null)
                    return false;

                // Position 1 = Buồng Phòng
                if (employee.Position != 1)
                    return false;

                roleName = AppRoles.Housekeeping;
                return true;


            default:
                return false;
        }
    }
}