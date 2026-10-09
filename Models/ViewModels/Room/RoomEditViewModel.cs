using System.ComponentModel.DataAnnotations;

namespace Hotel_Management_Website.Models.ViewModels.Room;

public sealed class RoomEditViewModel
{
    public Guid Id { get; set; }

    [Display(Name = "Mã phòng")]
    [Required(ErrorMessage = "Vui lòng nhập mã phòng.")]
    [StringLength(50, ErrorMessage = "Mã phòng không được vượt quá {1} ký tự.")]
    [RegularExpression(@"^[A-Za-z0-9]+$", ErrorMessage = "Mã phòng chỉ được bao gồm chữ hoa, chữ thường và số; không được chứa dấu cách hoặc ký tự đặc biệt.")]
    public string RoomCode { get; set; } = string.Empty;

    [Display(Name = "Tên phòng")]
    [Required(ErrorMessage = "Vui lòng nhập tên phòng.")]
    [StringLength(100, ErrorMessage = "Tên phòng không được vượt quá {1} ký tự.")]
    public string RoomName { get; set; } = string.Empty;

    [Display(Name = "Tầng")]
    [Range(1, 1000, ErrorMessage = "Tầng phải từ 1 đến 1000.")]
    public int Floor { get; set; }

    [Display(Name = "Hàng")]
    [Range(1, 1000, ErrorMessage = "Hàng phải từ 1 đến 1000.")]
    public int Row { get; set; }

    [Display(Name = "Thứ tự")]
    [Range(1, 1000, ErrorMessage = "Thứ tự phải từ 1 đến 1000.")]
    public int LeftToRightOrder { get; set; }

    [Display(Name = "Loại phòng")]
    [Required(ErrorMessage = "Vui lòng chọn loại phòng.")]
    public Guid? RoomTypeId { get; set; }

    [Display(Name = "Tình trạng")]
    public byte Status { get; set; }

    [Display(Name = "Trạng thái")]
    public bool IsActive { get; set; }

    [Display(Name = "Mô tả")]
    [StringLength(255, ErrorMessage = "Mô tả không được vượt quá {1} ký tự.")]
    public string? Description { get; set; }

    [Display(Name = "Ghi chú")]
    [StringLength(255, ErrorMessage = "Ghi chú không được vượt quá {1} ký tự.")]
    public string? Note { get; set; }

    public string? ReturnUrl { get; set; }
    public IReadOnlyList<RoomTypeOptionViewModel> AvailableRoomTypes { get; set; } = Array.Empty<RoomTypeOptionViewModel>();
}
