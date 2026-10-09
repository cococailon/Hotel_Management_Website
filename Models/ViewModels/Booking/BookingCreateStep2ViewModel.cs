using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Hotel_Management_Website.Models.ViewModels.Booking;

public sealed class BookingCreateStep2ViewModel
{
    public string DraftId { get; set; } = string.Empty;

    // Server-owned timestamp marking when Step 2 was opened.
    public DateTime? Step2StartedAt { get; set; }

    public Guid BookingId { get; set; }

    // Read-only summary inherited from the validated Step 1 draft.
    public DateTime CheckInDate { get; set; }

    public DateTime CheckOutDate { get; set; }

    public TimeOnly DefaultCheckInTime { get; set; }

    public TimeOnly DefaultCheckOutTime { get; set; }

    public int TotalAdults { get; set; }

    public int TotalChildren { get; set; }

    public decimal Nights { get; set; }

    public decimal EstimatedRoomAmount { get; set; }

    public decimal DepositAmount { get; set; }

    public string? AvailabilityMessage { get; set; }

    public bool AvailabilitySucceeded { get; set; }

    public IReadOnlyList<BookingRoomSelectionViewModel> RoomSelections { get; set; }
        = Array.Empty<BookingRoomSelectionViewModel>();

    // Booking information entered at Step 2.
    [Required(ErrorMessage = "Vui lòng chọn loại đặt phòng.")]
    [Range(0, 1, ErrorMessage = "Loại đặt phòng không hợp lệ.")]
    public byte BookingType { get; set; }

    [Required(ErrorMessage = "Mã đặt phòng không được để trống.")]
    [RegularExpression("^[A-Za-z0-9]+$", ErrorMessage = "Mã đặt phòng chỉ được bao gồm chữ hoa, chữ thường và số, không chứa dấu cách và ký tự đặc biệt.")]
    [StringLength(50, ErrorMessage = "Mã đặt phòng không được vượt quá 50 ký tự.")]
    public string BookingCode { get; set; } = string.Empty;

    public string? BookingNote { get; set; }

    // Guest information entered/selected at Step 2.
    [BindNever]
    public Guid? GuestId { get; set; }

    [Required(ErrorMessage = "Mã khách hàng không được để trống.")]
    [RegularExpression("^[A-Za-z0-9]+$", ErrorMessage = "Mã khách hàng chỉ được bao gồm chữ hoa, chữ thường và số, không chứa dấu cách và ký tự đặc biệt.")]
    [StringLength(50, ErrorMessage = "Mã khách hàng không được vượt quá 50 ký tự.")]
    public string GuestCode { get; set; } = string.Empty;

    public string GuestName { get; set; } = string.Empty;

    [DataType(DataType.Date)]
    public DateTime? GuestDateOfBirth { get; set; }

    public byte? GuestSex { get; set; }

    public string? GuestAddress { get; set; }

    public string? GuestPhoneNumber { get; set; }

    public string? GuestEmail { get; set; }

    public string? GuestIdentityNumber { get; set; }

    public byte? GuestIdentityType { get; set; }

    public string? GuestNote { get; set; }

    // Deposit payment information entered at Step 2.
    public byte? PaymentMethod { get; set; }

    [DataType(DataType.DateTime)]
    public DateTime? PaymentDate { get; set; }

    public string? ReferenceCode { get; set; }

    public bool DepositConfirmed { get; set; }

    public IReadOnlyList<GuestOptionViewModel> Guests { get; set; }
        = Array.Empty<GuestOptionViewModel>();
}

public sealed class BookingCreateStep2Result
{
    public bool Succeeded { get; init; }

    public string? ErrorMessage { get; init; }

    public IReadOnlyDictionary<string, string> FieldErrors { get; init; }
        = new Dictionary<string, string>();

    public BookingCreateStep2ViewModel Model { get; init; } = new();
}
