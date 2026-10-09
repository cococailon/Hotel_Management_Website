using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Hotel_Management_Website.Models.ViewModels.Booking;

public sealed class BookingEditViewModel
{
    public Guid BookingId { get; set; }

    [BindNever]
    public string BookingCode { get; set; } = string.Empty;

    [BindNever]
    public byte BookingType { get; set; }

    [BindNever]
    public string BookingTypeName { get; set; } = string.Empty;

    [BindNever]
    public string BookingTypeBadgeClass { get; set; } = "text-bg-secondary";

    [BindNever]
    public byte Status { get; set; }

    [BindNever]
    public string StatusName { get; set; } = string.Empty;

    [BindNever]
    public string StatusBadgeClass { get; set; } = "text-bg-secondary";

    [BindNever]
    public DateTime ExpectedCheckInAt { get; set; }

    [BindNever]
    public DateTime ExpectedCheckOutAt { get; set; }

    [BindNever]
    public DateTime? ActualCheckInAt { get; set; }

    [BindNever]
    public DateTime? ActualCheckOutAt { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Tổng người lớn phải lớn hơn 0.")]
    public int TotalAdults { get; set; }

    [Range(0, int.MaxValue, ErrorMessage = "Tổng trẻ em phải từ 0 trở lên.")]
    public int TotalChildren { get; set; }

    [StringLength(255, ErrorMessage = "Ghi chú Booking không được vượt quá 255 ký tự.")]
    public string? Note { get; set; }

    [BindNever]
    public bool CanEditPeople { get; set; }

    [BindNever]
    public bool CanEditServices { get; set; }

    public BookingEditGuestViewModel Guest { get; set; } = new();

    [BindNever]
    public bool CanEditGuestDetails { get; set; }

    [BindNever]
    public bool RequireFullGuestDetails { get; set; }

    [BindNever]
    public IReadOnlyList<BookingEditServiceOptionViewModel> Services { get; set; }
        = Array.Empty<BookingEditServiceOptionViewModel>();

    public List<BookingEditRoomViewModel> Rooms { get; set; } = new();
}

public sealed class BookingEditGuestViewModel
{
    [RegularExpression("^[A-Za-z0-9]+$", ErrorMessage = "Mã khách hàng chỉ được bao gồm chữ hoa, chữ thường và số, không chứa dấu cách và ký tự đặc biệt.")]
    [StringLength(50, ErrorMessage = "Mã khách hàng không được vượt quá 50 ký tự.")]
    public string GuestCode { get; set; } = string.Empty;

    [StringLength(100, ErrorMessage = "Tên khách hàng không được vượt quá 100 ký tự.")]
    public string Name { get; set; } = string.Empty;

    [DataType(DataType.Date)]
    public DateTime? DateOfBirth { get; set; }

    [Range(0, 2, ErrorMessage = "Giới tính không hợp lệ.")]
    public byte? Sex { get; set; }

    [StringLength(255, ErrorMessage = "Địa chỉ không được vượt quá 255 ký tự.")]
    public string? Address { get; set; }

    [StringLength(30, ErrorMessage = "Số điện thoại nhập vào không được vượt quá 30 ký tự.")]
    public string? PhoneNumber { get; set; }

    [StringLength(254, ErrorMessage = "Email không được vượt quá 254 ký tự.")]
    [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
    public string? Email { get; set; }

    [StringLength(50, ErrorMessage = "Số giấy tờ không được vượt quá 50 ký tự.")]
    public string? IdentityNumber { get; set; }

    [Range(0, 2, ErrorMessage = "Loại giấy tờ không hợp lệ.")]
    public byte? IdentityType { get; set; }

    [StringLength(255, ErrorMessage = "Ghi chú khách hàng không được vượt quá 255 ký tự.")]
    public string? Note { get; set; }

    [BindNever]
    public string SexName { get; set; } = "Không xác định";

    [BindNever]
    public string IdentityTypeName { get; set; } = "Không xác định";
}

public sealed class BookingEditRoomViewModel
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

    [StringLength(255, ErrorMessage = "Ghi chú BookingRoom không được vượt quá 255 ký tự.")]
    public string? Note { get; set; }

    public List<BookingEditServiceItemViewModel> ServiceInputs { get; set; } = new();

    public List<BookingEditFeeItemViewModel> FeeInputs { get; set; } = new();
}

public sealed class BookingEditServiceOptionViewModel
{
    public Guid Id { get; init; }
    public string ServiceCode { get; init; } = string.Empty;
    public string ServiceName { get; init; } = string.Empty;
    public decimal UnitPrice { get; init; }
    public string? PriceUnitType { get; init; }
    public bool IsActive { get; init; }
}

public sealed class BookingEditServiceItemViewModel
{
    public Guid? Id { get; set; }

    public Guid ServiceId { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn dịch vụ.")]
    public DateTime? UsedAt { get; set; }

    [Range(typeof(decimal), "0", "9999999999999999", ErrorMessage = "Thành tiền dịch vụ không được âm.")]
    public decimal TotalPrice { get; set; }

    [StringLength(255, ErrorMessage = "Ghi chú dịch vụ không được vượt quá 255 ký tự.")]
    public string? Note { get; set; }

    public bool IsDeleted { get; set; }
}

public sealed class BookingEditFeeItemViewModel
{
    public Guid? Id { get; set; }

    [StringLength(100, ErrorMessage = "Tên phí phát sinh không được vượt quá 100 ký tự.")]
    public string FeeName { get; set; } = string.Empty;

    [Range(0, 2, ErrorMessage = "Loại phí phát sinh không hợp lệ.")]
    public byte FeeType { get; set; }

    [Range(typeof(decimal), "0.01", "9999999999999999", ErrorMessage = "Thành tiền phí phát sinh phải lớn hơn 0.")]
    public decimal TotalFee { get; set; }

    [StringLength(255, ErrorMessage = "Mô tả phí phát sinh không được vượt quá 255 ký tự.")]
    public string? Description { get; set; }

    public bool IsDeleted { get; set; }
}

public sealed class BookingEditResult
{
    public bool Succeeded { get; init; }
    public string? ErrorMessage { get; init; }
    public IReadOnlyDictionary<string, string> FieldErrors { get; init; }
        = new Dictionary<string, string>();
    public BookingEditViewModel Model { get; init; } = new();
}
