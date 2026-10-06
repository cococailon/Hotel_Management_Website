using Hotel_Management_Website.Models.ViewModels.Service;

namespace Hotel_Management_Website.IServices;

public interface IServiceManagementService
{
    Task<ServiceIndexViewModel> GetIndexAsync(ServiceListFilterViewModel filter, CancellationToken cancellationToken = default);
    Task<string> GetNextServiceCodeAsync(CancellationToken cancellationToken = default);
    Task<ServiceDetailsViewModel?> GetDetailsAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ServiceCreateViewModel> PrepareCreateAsync(CancellationToken cancellationToken = default);
    Task<(bool Succeeded, string? ErrorMessage, IReadOnlyDictionary<string, string> FieldErrors)> CreateAsync(ServiceCreateViewModel model, Guid currentUserId, CancellationToken cancellationToken = default);
    Task<ServiceEditViewModel?> GetEditModelAsync(Guid id, string? returnUrl, CancellationToken cancellationToken = default);
    Task<(bool Succeeded, string? ErrorMessage, IReadOnlyDictionary<string, string> FieldErrors)> UpdateAsync(ServiceEditViewModel model, Guid currentUserId, CancellationToken cancellationToken = default);
    Task<(bool Succeeded, string? ErrorMessage)> DeactivateAsync(Guid id, Guid currentUserId, CancellationToken cancellationToken = default);
    Task<(bool Succeeded, string? ErrorMessage, int ReopenedAssociationCount)> ActivateAsync(Guid id, bool reactivateAssociations, Guid currentUserId, CancellationToken cancellationToken = default);
    Task<(bool Succeeded, string? ErrorMessage)> DeleteAsync(Guid id, Guid currentUserId, CancellationToken cancellationToken = default);
    Task<bool> HasActiveBookingsAsync(Guid id, CancellationToken cancellationToken = default);
}
