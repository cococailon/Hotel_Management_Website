using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Hotel_Management_Website.Models.ViewModels.Booking;

// ViewModel dùng chung cho một Razor View.
// Controller vẫn dùng BookingCreateStep1ViewModel/BookingCreateStep2ViewModel
// riêng cho hai POST để mỗi form chỉ bind đúng dữ liệu của bước đó.
public sealed class BookingCreateViewModel
{
    public string? DraftId { get; set; }

    public DateTime? Step2StartedAt { get; set; }

    public Guid BookingId { get; set; }

    public int Step { get; set; } = 1;

    [DataType(DataType.Date)]
    public DateTime CheckInDate { get; set; } = DateTime.Today;

    [DataType(DataType.Date)]
    public DateTime CheckOutDate { get; set; } = DateTime.Today.AddDays(1);

    public TimeOnly DefaultCheckInTime { get; set; }
    public TimeOnly DefaultCheckOutTime { get; set; }

    public byte BookingType { get; set; } = 0;

    public string BookingTypeName => BookingType == 1 ? "Trực tiếp tại quầy" : "Đặt trước";

    public string BookingCode { get; set; } = string.Empty;
    public string? BookingNote { get; set; }
    [BindNever]
    public Guid? GuestId { get; set; }
    public string GuestCode { get; set; } = string.Empty;
    public string GuestName { get; set; } = string.Empty;
    public DateTime? GuestDateOfBirth { get; set; }
    public byte? GuestSex { get; set; }
    public string? GuestAddress { get; set; }
    public string? GuestPhoneNumber { get; set; }
    public string? GuestEmail { get; set; }
    public string? GuestIdentityNumber { get; set; }
    public byte? GuestIdentityType { get; set; }
    public string? GuestNote { get; set; }

    public decimal Nights { get; set; }
    public decimal EstimatedRoomAmount { get; set; }
    public decimal DepositAmount { get; set; }
    public int TotalAdults { get; set; }
    public int TotalChildren { get; set; }
    public byte? PaymentMethod { get; set; }
    public DateTime? PaymentDate { get; set; }
    public string? ReferenceCode { get; set; }
    public bool DepositConfirmed { get; set; }

    public string? AvailabilityMessage { get; set; }
    public bool AvailabilitySucceeded { get; set; }

    public IReadOnlyList<BookingRoomSelectionViewModel> RoomSelections { get; set; }
        = Array.Empty<BookingRoomSelectionViewModel>();

    public IReadOnlyList<GuestOptionViewModel> Guests { get; set; }
        = Array.Empty<GuestOptionViewModel>();

    public IReadOnlyList<RoomTypeCreateOptionViewModel> RoomTypes { get; set; }
        = Array.Empty<RoomTypeCreateOptionViewModel>();
}

public sealed class BookingRoomSelectionViewModel
{
    // Step 1 chỉ cho phép gửi RoomTypeId và Quantity.
    // Các thuộc tính còn lại do server dựng lại từ DB.
    public Guid RoomTypeId { get; set; }

    [BindNever]
    public string RoomTypeCode { get; set; } = string.Empty;

    [BindNever]
    public string RoomTypeName { get; set; } = string.Empty;

    [BindNever]
    public int MaxAdults { get; set; }

    [BindNever]
    public int MaxChildren { get; set; }

    [BindNever]
    public decimal PricePerNight { get; set; }

    [BindNever]
    public decimal DepositPercent { get; set; }

    [BindNever]
    public int AvailableRoomCount { get; set; }

    public int Quantity { get; set; }
}

public sealed class RoomTypeCreateOptionViewModel
{
    public Guid Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public int MaxAdults { get; init; }
    public int MaxChildren { get; init; }
    public decimal PricePerNight { get; init; }
    public decimal DepositPercent { get; init; }
    public int AvailableRoomCount { get; set; }
}

public sealed class GuestOptionViewModel
{
    public Guid Id { get; init; }
    public string GuestCode { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public DateTime? DateOfBirth { get; init; }
    public byte? Sex { get; init; }
    public string? Address { get; init; }
    public string? PhoneNumber { get; init; }
    public string? Email { get; init; }
    public string? IdentityNumber { get; init; }
    public byte? IdentityType { get; init; }
    public string? Note { get; init; }
}

public sealed class BookingCreateValidationResult
{
    public bool Succeeded { get; init; }
    public string? ErrorMessage { get; init; }
    public IReadOnlyDictionary<string, string> FieldErrors { get; init; }
        = new Dictionary<string, string>();
    public BookingCreateViewModel Model { get; init; } = new();
}
