using Hotel_Management_Website.Models.ViewModels.User;

namespace Hotel_Management_Website.IServices;

public interface IUserService
{
    Task<byte?> GetUserRoleCodeAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<UserIndexViewModel> GetIndexAsync(
        Guid currentUserId,
        byte currentUserRole,
        UserListFilterViewModel adminFilter,
        UserListFilterViewModel receptionFilter,
        UserListFilterViewModel housekeepingFilter,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<UserEmployeeOptionViewModel>> GetEligibleEmployeesAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<UserEmployeeOptionViewModel>> GetEmployeesForEditAsync(
        byte role,
        Guid? currentEmployeeId,
        CancellationToken cancellationToken = default);

    Task<UserDetailsViewModel?> GetDetailsAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<string?> GetEmployeeDisplayNameAsync(
        Guid employeeId,
        CancellationToken cancellationToken = default);

    Task<UserCreateViewModel> PrepareCreateAsync(
        byte currentUserRole,
        byte requestedRole,
        CancellationToken cancellationToken = default);

    Task<UserEditViewModel?> GetEditModelAsync(
        Guid id,
        Guid currentUserId,
        byte currentUserRole,
        string? returnUrl,
        CancellationToken cancellationToken = default);

    Task<string> GenerateNextUserCodeAsync(
        CancellationToken cancellationToken = default);

    Task<(bool Succeeded, string? ErrorMessage, string? UserCode)> CreateAsync(
        UserCreateViewModel model,
        Guid currentUserId,
        byte currentUserRole,
        CancellationToken cancellationToken = default);

    Task<(bool Succeeded, string? ErrorMessage)> UpdateAsync(
        UserEditViewModel model,
        Guid currentUserId,
        byte currentUserRole,
        CancellationToken cancellationToken = default);

    Task<(bool Succeeded, string? ErrorMessage)> LockAsync(
        Guid id,
        Guid currentUserId,
        byte currentUserRole,
        CancellationToken cancellationToken = default);

    Task<(bool Succeeded, string? ErrorMessage)> ReopenAsync(
        Guid id,
        Guid currentUserId,
        byte currentUserRole,
        CancellationToken cancellationToken = default);

    Task<(bool Succeeded, string? ErrorMessage)> DeleteAsync(
        Guid id,
        Guid currentUserId,
        byte currentUserRole,
        CancellationToken cancellationToken = default);
}
