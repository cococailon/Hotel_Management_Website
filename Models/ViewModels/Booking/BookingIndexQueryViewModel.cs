namespace Hotel_Management_Website.Models.ViewModels.Booking;

public sealed class BookingIndexQueryViewModel
{
    public BookingListFilterViewModel Filter { get; set; } = new();

    public string? Reset { get; set; }
}
