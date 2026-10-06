using Hotel_Management_Website.Models.ViewModels.Booking;

namespace Hotel_Management_Website.IServices;

public interface IBookingService
{
    Task<BookingIndexViewModel> GetIndexAsync(
        BookingListFilterViewModel filter,
        CancellationToken cancellationToken = default);

    Task<BookingDetailsViewModel?> GetDetailsAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<BookingEditViewModel?> GetEditModelAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<BookingEditResult> UpdateEditAsync(
        BookingEditViewModel model,
        Guid currentUserId,
        CancellationToken cancellationToken = default);

    Task<BookingCreateStep1ViewModel> GetCreateStep1ModelAsync(
        CancellationToken cancellationToken = default);

    Task<BookingCreateStep1ValidationResult> ValidateCreateStep1Async(
        BookingCreateStep1ViewModel model,
        CancellationToken cancellationToken = default);

    Task<BookingCreateStep1ViewModel> RefreshCreateStep1Async(
        BookingCreateStep1ViewModel model,
        CancellationToken cancellationToken = default);

    Task<BookingCreateStep2ViewModel> GetCreateStep2ModelAsync(
        BookingCreateStep1ViewModel step1,
        string draftId,
        CancellationToken cancellationToken = default);


    Task<BookingFlowViewModel?> GetFlowAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<BookingCheckInViewModel?> GetCheckInModelAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<BookingActionResult> ConfirmAsync(
        BookingConfirmViewModel model,
        Guid currentUserId,
        CancellationToken cancellationToken = default);

    Task<BookingActionResult> DeleteAsync(
        Guid bookingId,
        Guid currentUserId,
        CancellationToken cancellationToken = default);

    Task<BookingActionResult> CheckInAsync(
        BookingCheckInViewModel model,
        Guid currentUserId,
        CancellationToken cancellationToken = default);

    Task<BookingActionResult> CancelBeforeCheckInAsync(
        BookingCancelViewModel model,
        Guid currentUserId,
        CancellationToken cancellationToken = default);

    Task<BookingCheckOutViewModel?> GetCheckOutModelAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<BookingActionResult> CheckOutAsync(
        BookingCheckOutViewModel model,
        Guid currentUserId,
        CancellationToken cancellationToken = default);

    Task<BookingCreateStep2Result> CreateAsync(
        BookingCreateStep1ViewModel step1,
        BookingCreateStep2ViewModel step2,
        Guid currentUserId,
        CancellationToken cancellationToken = default);

    Task<BookingDirectCreateViewModel> GetDirectCreateModelAsync(
        CancellationToken cancellationToken = default);

    Task<BookingDirectCreateViewModel> RefreshDirectCreateModelAsync(
        BookingDirectCreateViewModel model,
        CancellationToken cancellationToken = default);

    Task<BookingDirectCreateResult> CreateDirectAsync(
        BookingDirectCreateViewModel model,
        Guid currentUserId,
        CancellationToken cancellationToken = default);
}

public sealed class BookingActionResult
{
    public bool Succeeded { get; init; }
    public string? ErrorMessage { get; init; }
    public IReadOnlyDictionary<string, string> FieldErrors { get; init; }
        = new Dictionary<string, string>();
}

