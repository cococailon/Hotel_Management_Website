using System.ComponentModel.DataAnnotations;

namespace Hotel_Management_Website.Models.ViewModels.Guest;

public sealed class GuestEditViewModel
{
    public Guid Id { get; set; }

    [RegularExpression("^[A-Za-z0-9]+$", ErrorMessage = "Mã khách hàng chỉ được bao gồm chữ hoa, chữ thường và số, không chứa dấu cách và ký tự đặc biệt.")]
    [StringLength(50, ErrorMessage = "Mã khách hàng không được vượt quá 50 ký tự.")]
    public string GuestCode { get; set; } = string.Empty;

    [StringLength(100, ErrorMessage = "Tên khách hàng không được vượt quá 100 ký tự.")]
    public string Name { get; set; } = string.Empty;

    [DataType(DataType.Date)]
    public DateTime? DateOfBirth { get; set; }

    public byte? Sex { get; set; }

    public string? Address { get; set; }

    [StringLength(30, ErrorMessage = "Số điện thoại nhập vào không được vượt quá 30 ký tự.")]
    public string? PhoneNumber { get; set; }

    [StringLength(254, ErrorMessage = "Email không được vượt quá 254 ký tự.")]
    [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
    public string? Email { get; set; }

    [StringLength(50, ErrorMessage = "Số giấy tờ không được vượt quá 50 ký tự.")]
    public string? IdentityNumber { get; set; }

    public byte? IdentityType { get; set; }

    [StringLength(255, ErrorMessage = "Ghi chú khách hàng không được vượt quá 255 ký tự.")]
    public string? Note { get; set; }

    public bool CanEditDetails { get; set; }
    public bool RequireFullDetails { get; set; }
    public bool CanEditNoteOnly { get; set; }
    public string BookingRuleDescription { get; set; } = string.Empty;
}
