using System.ComponentModel.DataAnnotations;

namespace Hotel_Management_Website.Models.ViewModels.Guest;

public sealed class GuestEditViewModel
{
    public Guid Id { get; set; }

    public string GuestCode { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    [DataType(DataType.Date)]
    public DateTime? DateOfBirth { get; set; }

    public byte? Sex { get; set; }

    public string? Address { get; set; }

    public string? PhoneNumber { get; set; }

    [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
    public string? Email { get; set; }

    public string? IdentityNumber { get; set; }

    public byte? IdentityType { get; set; }

    public string? Note { get; set; }

    public bool CanEditDetails { get; set; }
    public bool RequireFullDetails { get; set; }
    public bool CanEditNoteOnly { get; set; }
    public string BookingRuleDescription { get; set; } = string.Empty;
}
