namespace Hotel_Management_Website.Models.ViewModels.Booking;

public sealed class BookingDetailsViewModel
{
    public Guid Id { get; init; }

    public string BookingCode { get; init; } = string.Empty;

    public byte BookingType { get; init; }

    public string BookingTypeName { get; init; } = string.Empty;

    public string BookingTypeBadgeClass { get; init; } = "text-bg-secondary";

    public BookingGuestDetailsViewModel Guest { get; init; } = new();

    public DateTime ExpectedCheckInAt { get; init; }

    public DateTime ExpectedCheckOutAt { get; init; }

    public DateTime? ActualCheckInAt { get; init; }

    public DateTime? ActualCheckOutAt { get; init; }

    public int TotalAdults { get; init; }

    public int TotalChildren { get; init; }

    public decimal DepositAmount { get; init; }

    public byte? PaymentMethod { get; init; }

    public string PaymentMethodName { get; init; } = string.Empty;

    public DateTime? PaymentDate { get; init; }

    public string? ReferenceCode { get; init; }

    public byte Status { get; init; }

    public string StatusName { get; init; } = string.Empty;

    public string StatusBadgeClass { get; init; } = "text-bg-secondary";

    public string? Note { get; init; }

    public DateTime CreatedAt { get; init; }

    public DateTime LastModifiedAt { get; init; }

    public IReadOnlyList<BookingRoomDetailsViewModel> BookingRooms { get; init; }
        = Array.Empty<BookingRoomDetailsViewModel>();

    public BillDetailsViewModel? Bill { get; init; }

    public bool HasBill => Bill is not null;
}

public sealed class BookingGuestDetailsViewModel
{
    public string GuestCode { get; init; } = string.Empty;

    public string GuestName { get; init; } = string.Empty;

    public int? Age { get; init; }

    public byte? Sex { get; init; }

    public string SexName { get; init; } = "Không xác định";

    public string? PhoneNumber { get; init; }

    public string? Email { get; init; }
}

public sealed class BookingRoomDetailsViewModel
{
    public Guid Id { get; init; }

    public string RoomTypeCode { get; init; } = string.Empty;

    public string RoomTypeName { get; init; } = string.Empty;

    public string RoomCode { get; init; } = string.Empty;

    public string RoomName { get; init; } = string.Empty;

    public int? Adults { get; init; }

    public int? Children { get; init; }

    public string? Note { get; init; }

    public DateTime CreatedAt { get; init; }

    public DateTime LastModifiedAt { get; init; }

    public IReadOnlyList<BookingRoomServiceDetailsViewModel> Services { get; init; }
        = Array.Empty<BookingRoomServiceDetailsViewModel>();

    public IReadOnlyList<AdditionalFeeRoomDetailsViewModel> AdditionalFees { get; init; }
        = Array.Empty<AdditionalFeeRoomDetailsViewModel>();
}

public sealed class BookingRoomServiceDetailsViewModel
{
    public Guid Id { get; init; }

    public Guid BookingRoomId { get; init; }

    public string ServiceCode { get; init; } = string.Empty;

    public string ServiceName { get; init; } = string.Empty;

    public decimal TotalPrice { get; init; }

    public DateTime UsedAt { get; init; }

    public string? Note { get; init; }
}

public sealed class AdditionalFeeRoomDetailsViewModel
{
    public Guid Id { get; init; }

    public Guid BookingRoomId { get; init; }

    public string FeeName { get; init; } = string.Empty;

    public byte FeeType { get; init; }

    public string FeeTypeName { get; init; } = string.Empty;

    public decimal TotalFee { get; init; }

    public string? Description { get; init; }
}

public sealed class BillDetailsViewModel
{
    public Guid Id { get; init; }

    public string BillCode { get; init; } = string.Empty;

    public decimal? TotalAmount { get; init; }

    public decimal DepositAmount { get; init; }

    public decimal FinalAmount { get; init; }

    public byte Status { get; init; }

    public string StatusName { get; init; } = string.Empty;

    public string StatusBadgeClass { get; init; } = "text-bg-secondary";

    public string? Note { get; init; }

    public DateTime CreatedAt { get; init; }

    public DateTime LastModifiedAt { get; init; }

    public DateTime? PaidAt { get; init; }

    public IReadOnlyList<BillDetailItemViewModel> Details { get; init; }
        = Array.Empty<BillDetailItemViewModel>();
}

public sealed class BillDetailItemViewModel
{
    public Guid Id { get; init; }

    public Guid BookingRoomId { get; init; }

    public string RoomTypeCode { get; init; } = string.Empty;

    public string RoomTypeName { get; init; } = string.Empty;

    public string RoomCode { get; init; } = string.Empty;

    public string RoomName { get; init; } = string.Empty;

    public decimal? TotalPrice { get; init; }

    public string? Note { get; init; }

    public DateTime CreatedAt { get; init; }

    public DateTime LastModifiedAt { get; init; }
}
