using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Hotel_Management_Website.Models.ViewModels.Booking;

public sealed class BookingPaymentRecoveryViewModel
{
    public Guid BookingId { get; set; }

    [BindNever]
    public string BookingCode { get; set; } = string.Empty;

    [Required(ErrorMessage = "Mã khách hàng không được để trống.")]
    [RegularExpression("^[A-Za-z0-9]+$", ErrorMessage = "Mã khách hàng chỉ được bao gồm chữ và số, không chứa dấu cách và ký tự đặc biệt.")]
    [StringLength(50, ErrorMessage = "Mã khách hàng không được vượt quá 50 ký tự.")]
    public string GuestCode { get; set; } = string.Empty;

    [Required(ErrorMessage = "Tên khách hàng là bắt buộc.")]
    [StringLength(100, ErrorMessage = "Tên khách hàng không được vượt quá 100 ký tự.")]
    public string GuestName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Ngày sinh là bắt buộc.")]
    [DataType(DataType.Date)]
    public DateTime? GuestDateOfBirth { get; set; }

    [Required(ErrorMessage = "Giới tính là bắt buộc.")]
    [Range(0, 2, ErrorMessage = "Giới tính không hợp lệ.")]
    public byte? GuestSex { get; set; }

    [Required(ErrorMessage = "Địa chỉ là bắt buộc.")]
    [StringLength(255, ErrorMessage = "Địa chỉ không được vượt quá 255 ký tự.")]
    public string GuestAddress { get; set; } = string.Empty;

    [Required(ErrorMessage = "Số điện thoại là bắt buộc.")]
    [StringLength(30, ErrorMessage = "Số điện thoại nhập vào không được vượt quá 30 ký tự.")]
    public string GuestPhoneNumber { get; set; } = string.Empty;

    [StringLength(254, ErrorMessage = "Email không được vượt quá 254 ký tự.")]
    [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
    public string? GuestEmail { get; set; }

    [Required(ErrorMessage = "Số giấy tờ là bắt buộc.")]
    [StringLength(50, ErrorMessage = "Số giấy tờ không được vượt quá 50 ký tự.")]
    public string GuestIdentityNumber { get; set; } = string.Empty;

    [Required(ErrorMessage = "Loại giấy tờ là bắt buộc.")]
    [Range(0, 2, ErrorMessage = "Loại giấy tờ không hợp lệ.")]
    public byte? GuestIdentityType { get; set; }

    [StringLength(255, ErrorMessage = "Ghi chú khách hàng không được vượt quá 255 ký tự.")]
    public string? GuestNote { get; set; }

    [StringLength(255, ErrorMessage = "Ghi chú Booking không được vượt quá 255 ký tự.")]
    public string? BookingNote { get; set; }

    [BindNever]
    public Guid BillId { get; set; }

    [BindNever]
    public string BillCode { get; set; } = string.Empty;

    [BindNever]
    public decimal TotalAmount { get; set; }

    [BindNever]
    public decimal DepositAmount { get; set; }

    [BindNever]
    public decimal FinalAmount { get; set; }

    [StringLength(255, ErrorMessage = "Ghi chú hóa đơn không được vượt quá 255 ký tự.")]
    public string? BillNote { get; set; }

    public bool PaymentReceived { get; set; }

    public byte? PaymentMethod { get; set; }

    [StringLength(100, ErrorMessage = "Mã giao dịch không được vượt quá 100 ký tự.")]
    public string? ReferenceCode { get; set; }

    public List<BookingPaymentRecoveryRoomViewModel> Rooms { get; set; } = new();
}

public sealed class BookingPaymentRecoveryRoomViewModel
{
    public Guid BookingRoomId { get; set; }

    [BindNever]
    public string RoomTypeCode { get; set; } = string.Empty;

    [BindNever]
    public string RoomTypeName { get; set; } = string.Empty;

    [BindNever]
    public string RoomCode { get; set; } = string.Empty;

    [BindNever]
    public string RoomName { get; set; } = string.Empty;

    [StringLength(255, ErrorMessage = "Ghi chú phòng không được vượt quá 255 ký tự.")]
    public string? Note { get; set; }

    public List<BookingPaymentRecoveryServiceViewModel> Services { get; set; } = new();
    public List<BookingPaymentRecoveryFeeViewModel> Fees { get; set; } = new();
}

public sealed class BookingPaymentRecoveryServiceViewModel
{
    public Guid Id { get; set; }

    [BindNever]
    public string ServiceCode { get; set; } = string.Empty;

    [BindNever]
    public string ServiceName { get; set; } = string.Empty;

    [BindNever]
    public DateTime UsedAt { get; set; }

    [BindNever]
    public decimal TotalPrice { get; set; }

    [StringLength(255, ErrorMessage = "Ghi chú dịch vụ không được vượt quá 255 ký tự.")]
    public string? Note { get; set; }
}

public sealed class BookingPaymentRecoveryFeeViewModel
{
    public Guid Id { get; set; }

    [BindNever]
    public string FeeName { get; set; } = string.Empty;

    [BindNever]
    public byte FeeType { get; set; }

    [BindNever]
    public decimal TotalFee { get; set; }

    [StringLength(255, ErrorMessage = "Ghi chú phí phát sinh không được vượt quá 255 ký tự.")]
    public string? Description { get; set; }
}
