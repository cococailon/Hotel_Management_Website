using Hotel_Management_Website.Models.ViewModels.UI;

namespace Hotel_Management_Website.Models.ViewModels.Booking;

public sealed class BookingIndexViewModel
{
    public BookingListFilterViewModel Filter { get; init; } = new();

    public IReadOnlyList<BookingListItemViewModel> Bookings { get; init; }
        = Array.Empty<BookingListItemViewModel>();

    public int TotalCount { get; init; }

    public PaginationViewModel Pagination { get; set; } = new();

    public string? LoadError { get; init; }
}
