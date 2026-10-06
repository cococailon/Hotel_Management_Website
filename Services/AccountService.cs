using Hotel_Management_Website.IServices;
using Hotel_Management_Website.Models;
using Hotel_Management_Website.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Hotel_Management_Website.Services;
public class AccountService : IAccountService
{
    private readonly AppDbContext _context;
    private readonly IPasswordHasher<User> _passwordHasher;

    public AccountService(
        AppDbContext context,
        IPasswordHasher<User> passwordHasher)
    {
        _context = context;
        _passwordHasher = passwordHasher;
    }


    // =============================================================
    // GET USER FOR LOGIN
    // =============================================================

    public async Task<User?> GetUserForLoginAsync(
        string userName)
    {
        if (string.IsNullOrWhiteSpace(userName))
        {
            return null;
        }


        userName = userName.Trim();


        return await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(x =>
                x.UserName == userName &&
                !x.IsDeleted);
    }


    // =============================================================
    // GET EMPLOYEE
    // =============================================================

    public async Task<Employee?> GetEmployeeAsync(
        Guid employeeId)
    {
        return await _context.Employees
            .AsNoTracking()
            .FirstOrDefaultAsync(x =>
                x.Id == employeeId &&
                !x.IsDeleted);
    }


    // =============================================================
    // VERIFY PASSWORD
    // =============================================================

    public bool VerifyPassword(
        User user,
        string password)
    {
        if (user == null ||
            string.IsNullOrWhiteSpace(password) ||
            string.IsNullOrWhiteSpace(user.Password))
        {
            return false;
        }


        var result =
            _passwordHasher.VerifyHashedPassword(
                user,
                user.Password,
                password);


        return result == PasswordVerificationResult.Success ||
               result == PasswordVerificationResult.SuccessRehashNeeded;
    }


    // =============================================================
    // VALIDATE ACCOUNT
    // =============================================================

    public bool ValidateAccount(
        User user,
        Employee? employee,
        out string roleName)
    {
        return AccountRules.Validate(
            user,
            employee,
            out roleName);
    }


    // =============================================================
    // ROLE HOME
    // =============================================================

    public string GetRoleHomeUrl(
        string roleName)
    {
        return AppRoles.TryGetHomeUrl(
            roleName,
            out var url)
            ? url
            : "/Account/Login";
    }
}