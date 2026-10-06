using Hotel_Management_Website.Models.ViewModels.Room;

namespace Hotel_Management_Website.IServices;

public interface IRoomService
{
    Task<RoomIndexViewModel> GetIndexAsync(
        RoomListFilterViewModel filter,
        CancellationToken cancellationToken = default);

    Task<RoomCreateViewModel> GetCreateModelAsync(
        CancellationToken cancellationToken = default);

    Task<RoomEditViewModel?> GetEditModelAsync(
        Guid id,
        string? returnUrl = null,
        CancellationToken cancellationToken = default);

    Task<RoomDetailsViewModel?> GetDetailsAsync(
        Guid id,
        string? returnUrl = null,
        CancellationToken cancellationToken = default);

    Task<Hotel_Management_Website.Models.ViewModels.Housekeeping.HousekeepingRoomStatusViewModel> GetHousekeepingRoomStatusAsync(
        RoomListFilterViewModel filter,
        CancellationToken cancellationToken = default);

    Task<(bool Succeeded, string? ErrorMessage)> StartCleaningAsync(
        Guid id,
        Guid currentUserId,
        CancellationToken cancellationToken = default);

    Task<(bool Succeeded, string? ErrorMessage)> CompleteCleaningAsync(
        Guid id,
        Guid currentUserId,
        CancellationToken cancellationToken = default);

    Task<string> GetNextRoomCodeAsync(
        CancellationToken cancellationToken = default);

    Task<RoomPositionSuggestionViewModel> GetPositionSuggestionsAsync(
        int floor,
        int? row,
        CancellationToken cancellationToken = default);

    Task<(bool Succeeded, string? ErrorMessage, IReadOnlyDictionary<string, string> FieldErrors)> CreateAsync(
        RoomCreateViewModel model,
        Guid currentUserId,
        CancellationToken cancellationToken = default);

    Task<(bool Succeeded, string? ErrorMessage, IReadOnlyDictionary<string, string> FieldErrors)> UpdateAsync(
        RoomEditViewModel model,
        Guid currentUserId,
        CancellationToken cancellationToken = default);

    Task<(bool Succeeded, string? ErrorMessage)> DeactivateAsync(
        Guid id,
        Guid currentUserId,
        bool isMaintenance,
        CancellationToken cancellationToken = default);

    Task<(bool Succeeded, string? ErrorMessage)> ActivateAsync(
        Guid id,
        Guid currentUserId,
        CancellationToken cancellationToken = default);

    Task<(bool Succeeded, string? ErrorMessage)> DeleteAsync(
        Guid id,
        Guid currentUserId,
        CancellationToken cancellationToken = default);
}
