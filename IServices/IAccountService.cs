using Hotel_Management_Website.Models;

namespace Hotel_Management_Website.IServices;

public interface IAccountService
{
    Task<User?> GetUserForLoginAsync(
        string userName);

    Task<Employee?> GetEmployeeAsync(
        Guid employeeId);

    bool VerifyPassword(
        User user,
        string password);

    bool ValidateAccount(
        User user,
        Employee? employee,
        out string roleName);

    string GetRoleHomeUrl(
        string roleName);
}