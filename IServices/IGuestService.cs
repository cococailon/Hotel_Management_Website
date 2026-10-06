using Hotel_Management_Website.Models.ViewModels.Guest;

namespace Hotel_Management_Website.IServices;

public interface IGuestService
{
    Task<GuestIndexViewModel> GetIndexAsync(
        GuestListFilterViewModel filter,
        CancellationToken cancellationToken = default);

    Task<GuestDetailsViewModel?> GetDetailsAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<GuestEditViewModel?> GetEditModelAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<GuestActionResult> UpdateAsync(
        GuestEditViewModel model,
        Guid currentUserId,
        CancellationToken cancellationToken = default);

    Task<GuestActionResult> DeleteAsync(
        Guid id,
        Guid currentUserId,
        CancellationToken cancellationToken = default);
}
