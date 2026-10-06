using Hotel_Management_Website.Models.ViewModels.Amenity;

namespace Hotel_Management_Website.IServices;

public interface IAmenityService
{
    Task<AmenityIndexViewModel> GetIndexAsync(
        AmenityListFilterViewModel filter,
        CancellationToken cancellationToken = default);

    Task<string> GetNextAmenityCodeAsync(
        CancellationToken cancellationToken = default);

    Task<AmenityDetailsViewModel?> GetDetailsAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<AmenityCreateViewModel> PrepareCreateAsync(
        CancellationToken cancellationToken = default);

    Task<(bool Succeeded, string? ErrorMessage, IReadOnlyDictionary<string, string> FieldErrors)> CreateAsync(
        AmenityCreateViewModel model,
        Guid currentUserId,
        CancellationToken cancellationToken = default);

    Task<AmenityEditViewModel?> GetEditModelAsync(
        Guid id,
        string? returnUrl,
        CancellationToken cancellationToken = default);

    Task<(bool Succeeded, string? ErrorMessage, IReadOnlyDictionary<string, string> FieldErrors)> UpdateAsync(
        AmenityEditViewModel model,
        Guid currentUserId,
        CancellationToken cancellationToken = default);

    Task<(bool Succeeded, string? ErrorMessage)> DeactivateAsync(
        Guid id,
        Guid currentUserId,
        CancellationToken cancellationToken = default);

    Task<(bool Succeeded, string? ErrorMessage, int ReopenedAssociationCount)> ActivateAsync(
        Guid id,
        bool reactivateAssociations,
        Guid currentUserId,
        CancellationToken cancellationToken = default);

    Task<(bool Succeeded, string? ErrorMessage)> DeleteAsync(
        Guid id,
        Guid currentUserId,
        CancellationToken cancellationToken = default);
}
