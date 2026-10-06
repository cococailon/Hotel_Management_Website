namespace Hotel_Management_Website.Models.ViewModels.Bill;

public sealed class BillDetailsViewModel
{
    public Guid Id { get; init; }
    public string BillCode { get; init; } = string.Empty;
    public Guid BookingId { get; init; }
    public string BookingCode { get; init; } = string.Empty;
    public decimal? TotalAmount { get; init; }
    public decimal DepositAmount { get; init; }
    public decimal FinalAmount { get; init; }
    public byte PaymentMethod { get; init; }
    public string? ReferenceCode { get; init; }
    public byte? DepositPaymentMethod { get; init; }
    public string? DepositReferenceCode { get; init; }
    public DateTime? DepositPaymentDate { get; init; }
    public DateTime? PaidAt { get; init; }
    public byte Status { get; init; }
    public string StatusName { get; init; } = string.Empty;
    public string StatusBadgeClass { get; init; } = "text-bg-secondary";
    public string? Note { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime LastModifiedAt { get; init; }
    public DateTime? BookingExpectedCheckInAt { get; init; }
    public DateTime? BookingExpectedCheckOutAt { get; init; }
    public DateTime? BookingActualCheckInAt { get; init; }
    public DateTime? BookingActualCheckOutAt { get; init; }
    public byte? BookingStatus { get; init; }
    public byte? BookingType { get; init; }
    public string? BookingTypeName { get; init; }
    public string GuestCode { get; init; } = string.Empty;
    public string GuestName { get; init; } = string.Empty;
    public DateTime? GuestDateOfBirth { get; init; }
    public byte? GuestSex { get; init; }
    public string? GuestSexName { get; init; }
    public string? GuestAddress { get; init; }
    public string? GuestPhoneNumber { get; init; }
    public string? GuestEmail { get; init; }
    public string? GuestIdentityNumber { get; init; }
    public byte? GuestIdentityType { get; init; }
    public string? GuestIdentityTypeName { get; init; }
    public string? GuestNote { get; init; }
    public string? BookingStatusName { get; init; }
    public IReadOnlyList<BillDetailItemViewModel> Details { get; init; } = Array.Empty<BillDetailItemViewModel>();
}

public sealed class BillDetailItemViewModel
{
    public Guid Id { get; init; }
    public Guid BookingRoomId { get; init; }
    public string RoomTypeCode { get; init; } = string.Empty;
    public string RoomTypeName { get; init; } = string.Empty;
    public string RoomCode { get; init; } = string.Empty;
    public string RoomName { get; init; } = string.Empty;
    public int? Adults { get; init; }
    public int? Children { get; init; }
    public decimal TotalPrice { get; init; }
    public string? Note { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime LastModifiedAt { get; init; }
    public IReadOnlyList<BillServiceItemViewModel> Services { get; init; } = Array.Empty<BillServiceItemViewModel>();
    public IReadOnlyList<BillAdditionalFeeItemViewModel> AdditionalFees { get; init; } = Array.Empty<BillAdditionalFeeItemViewModel>();
}

public sealed class BillServiceItemViewModel
{
    public Guid Id { get; init; }
    public Guid BookingRoomId { get; init; }
    public string ServiceCode { get; init; } = string.Empty;
    public string ServiceName { get; init; } = string.Empty;
    public decimal TotalPrice { get; init; }
    public DateTime UsedAt { get; init; }
    public string? Note { get; init; }
}

public sealed class BillAdditionalFeeItemViewModel
{
    public Guid Id { get; init; }
    public Guid BookingRoomId { get; init; }
    public string FeeName { get; init; } = string.Empty;
    public byte FeeType { get; init; }
    public string FeeTypeName { get; init; } = string.Empty;
    public decimal TotalFee { get; init; }
    public string? Description { get; init; }
}
