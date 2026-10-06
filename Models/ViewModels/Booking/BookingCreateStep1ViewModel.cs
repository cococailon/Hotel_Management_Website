using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Hotel_Management_Website.Models.ViewModels.Booking;

public sealed class BookingCreateStep1ViewModel
{
    public string? DraftId { get; set; }

    // Server-owned timestamp marking when Step 2 was opened.
    [BindNever]
    public DateTime? Step2StartedAt { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn ngày check in dự kiến.")]
    [DataType(DataType.Date)]
    public DateTime CheckInDate { get; set; } = DateTime.Today;

    [Required(ErrorMessage = "Vui lòng chọn ngày check out dự kiến.")]
    [DataType(DataType.Date)]
    public DateTime CheckOutDate { get; set; } = DateTime.Today.AddDays(1);

    // Server-owned values. They are never accepted from POST.
    [BindNever]
    public TimeOnly DefaultCheckInTime { get; set; }

    [BindNever]
    public TimeOnly DefaultCheckOutTime { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Tổng người lớn phải lớn hơn 0")]
    public int TotalAdults { get; set; }

    [Range(0, int.MaxValue, ErrorMessage = "Tổng trẻ em phải từ 0 trở lên")]
    public int TotalChildren { get; set; }

    [BindNever]
    public decimal Nights { get; set; }

    [BindNever]
    public decimal EstimatedRoomAmount { get; set; }

    [BindNever]
    public decimal DepositAmount { get; set; }

    [BindNever]
    public string? AvailabilityMessage { get; set; }

    [BindNever]
    public bool AvailabilitySucceeded { get; set; }

    // Concrete List is intentional: ASP.NET Core model binding can bind indexed
    // collection inputs reliably, while all server-owned room metadata is rebuilt
    // from RoomTypeId on the server.
    public List<BookingRoomSelectionViewModel> RoomSelections { get; set; }
        = new();
}

public sealed class BookingCreateStep1ValidationResult
{
    public bool Succeeded { get; init; }

    public string? ErrorMessage { get; init; }

    public IReadOnlyDictionary<string, string> FieldErrors { get; init; }
        = new Dictionary<string, string>();

    public BookingCreateStep1ViewModel Model { get; init; } = new();
}
