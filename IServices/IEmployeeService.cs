using Hotel_Management_Website.Models.ViewModels.Employee;

namespace Hotel_Management_Website.IServices;

public interface IEmployeeService
{
    Task<EmployeeIndexViewModel> GetIndexAsync(
        EmployeeListFilterViewModel receptionFilter,
        EmployeeListFilterViewModel housekeepingFilter,
        CancellationToken cancellationToken = default);

    Task<EmployeeListSectionViewModel> GetSectionAsync(
        byte position,
        EmployeeListFilterViewModel filter,
        CancellationToken cancellationToken = default);

    Task<EmployeeDetailsViewModel?> GetDetailsAsync(
    Guid id,
    CancellationToken cancellationToken = default);

    Task<EmployeeCreateViewModel> GetCreateModelAsync(
    byte position,
    CancellationToken cancellationToken = default);

    Task<string> GetNextEmployeeCodeAsync(
        CancellationToken cancellationToken = default);

    Task<(bool Succeeded, string? ErrorMessage)> CreateAsync(
        EmployeeCreateViewModel model,
        Guid currentUserId,
        CancellationToken cancellationToken = default);

    Task<EmployeeEditViewModel?> GetEditModelAsync(
    Guid id,
    string? returnUrl = null,
    CancellationToken cancellationToken = default);

    Task<(bool Succeeded, string? ErrorMessage)> UpdateAsync(
        EmployeeEditViewModel model,
        Guid currentUserId,
        CancellationToken cancellationToken = default);

    Task<(bool Succeeded, string? ErrorMessage, int DeactivatedUserCount)> LeaveAsync(
    Guid id,
    Guid currentUserId,
    CancellationToken cancellationToken = default);

    Task<(bool Succeeded, string? ErrorMessage, int ActivatedUserCount)> ReturnToWorkAsync(
        Guid id,
        Guid currentUserId,
        bool activateLinkedUser,
        CancellationToken cancellationToken = default);

    Task<(bool Succeeded, string? ErrorMessage, int DeactivatedUserCount)> DeleteAsync(
    Guid id,
    Guid currentUserId,
    CancellationToken cancellationToken = default);
}
