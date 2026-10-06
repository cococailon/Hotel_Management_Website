using Hotel_Management_Website.Models.ViewModels.RoomType;

namespace Hotel_Management_Website.IServices;

public interface IRoomTypeService
{
    Task<RoomTypeIndexViewModel> GetIndexAsync(
        RoomTypeListFilterViewModel filter,
        CancellationToken cancellationToken = default);

    Task<RoomTypeCreateViewModel> PrepareCreateAsync(
        CancellationToken cancellationToken = default);

    Task<RoomTypeEditViewModel?> GetEditModelAsync(
        Guid id,
        string? returnUrl,
        CancellationToken cancellationToken = default);

    Task<RoomTypeDetailsViewModel?> GetDetailsAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<string> GetNextRoomTypeCodeAsync(CancellationToken cancellationToken = default);

    Task<(bool Succeeded, string? ErrorMessage, IReadOnlyDictionary<string, string> FieldErrors)> CreateAsync(
        RoomTypeCreateViewModel model,
        Guid currentUserId,
        CancellationToken cancellationToken = default);

    Task<(bool Succeeded, string? ErrorMessage, IReadOnlyDictionary<string, string> FieldErrors)> UpdateAsync(
        RoomTypeEditViewModel model,
        Guid currentUserId,
        CancellationToken cancellationToken = default);

    Task<(bool Succeeded, string? ErrorMessage)> DeleteAsync(
        Guid id,
        Guid currentUserId,
        CancellationToken cancellationToken = default);

    Task<(bool Succeeded, string? ErrorMessage)> DeactivateAsync(
        Guid id,
        Guid currentUserId,
        CancellationToken cancellationToken = default);

    Task<(bool Succeeded, string? ErrorMessage, int ReopenedRoomCount)> ActivateAsync(
        Guid id,
        bool reopenRooms,
        Guid currentUserId,
        CancellationToken cancellationToken = default);

    Task PopulateCreateLookupsAsync(
        RoomTypeCreateViewModel model,
        CancellationToken cancellationToken = default);

    Task PopulateEditLookupsAsync(
        RoomTypeEditViewModel model,
        bool loadCurrentRelationsWhenEmpty = true,
        CancellationToken cancellationToken = default);
}
