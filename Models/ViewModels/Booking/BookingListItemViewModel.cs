namespace Hotel_Management_Website.Models.ViewModels.Booking;

public sealed class BookingListItemViewModel
{
    public Guid Id { get; init; }

    public string BookingCode { get; init; } = string.Empty;

    public string GuestCode { get; init; } = string.Empty;

    public string GuestName { get; init; } = string.Empty;

    public byte BookingType { get; init; }

    public string BookingTypeName { get; init; } = string.Empty;

    public string BookingTypeBadgeClass { get; init; } = "text-bg-secondary";

    public DateTime ExpectedCheckInAt { get; init; }

    public DateTime ExpectedCheckOutAt { get; init; }

    public int TotalAdults { get; init; }

    public int TotalChildren { get; init; }

    public byte Status { get; init; }

    public string StatusName { get; init; } = string.Empty;

    public string StatusBadgeClass { get; init; } = "text-bg-secondary";
}
