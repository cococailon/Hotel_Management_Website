namespace Hotel_Management_Website.Models.ViewModels.RoomType;

public sealed class RoomTypeListItemViewModel
{
    public Guid Id { get; init; }
    public string RoomTypeCode { get; init; } = string.Empty;
    public string RoomTypeName { get; init; } = string.Empty;
    public int MaxAdults { get; init; }
    public int MaxChildren { get; init; }
    public decimal PricePerNight { get; init; }
    public decimal DepositPercent { get; init; }
    public decimal Area { get; init; }
    public bool IsActive { get; init; }
    public DateTime LastModifiedAt { get; init; }
    public bool HasActiveBookings { get; init; }
    public bool HasUndeletedRooms { get; init; }

    public bool CanDelete => !IsActive && !HasActiveBookings && !HasUndeletedRooms;
    public bool CanDeactivate => IsActive && !HasActiveBookings;
}
