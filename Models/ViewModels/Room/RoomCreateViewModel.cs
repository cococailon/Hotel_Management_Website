using System.ComponentModel.DataAnnotations;

namespace Hotel_Management_Website.Models.ViewModels.Room;

public sealed class RoomCreateViewModel
{
    [Display(Name = "Mã phòng")]
    [RegularExpression(@"^\S+$", ErrorMessage = "Mã phòng không được chứa dấu cách hoặc khoảng trắng.")]
    [StringLength(50, ErrorMessage = "Mã phòng không được vượt quá {1} ký tự.")]
    public string RoomCode { get; set; } = string.Empty;

    [Display(Name = "Tên phòng")]
    [Required(ErrorMessage = "Vui lòng nhập tên phòng.")]
    [StringLength(100, ErrorMessage = "Tên phòng không được vượt quá {1} ký tự.")]
    public string RoomName { get; set; } = string.Empty;

    [Display(Name = "Tầng")]
    [Range(1, 1000, ErrorMessage = "Tầng phải từ 1 đến 1000.")]
    public int Floor { get; set; } = 1;

    [Display(Name = "Hàng")]
    [Range(1, 1000, ErrorMessage = "Hàng phải từ 1 đến 1000.")]
    public int Row { get; set; } = 1;

    [Display(Name = "Thứ tự")]
    [Range(1, 1000, ErrorMessage = "Thứ tự phải từ 1 đến 1000.")]
    public int LeftToRightOrder { get; set; } = 1;

    [Display(Name = "Loại phòng")]
    [Required(ErrorMessage = "Vui lòng chọn loại phòng.")]
    public Guid? RoomTypeId { get; set; }

    [Display(Name = "Trạng thái")]
    public bool IsActive { get; set; } = true;

    [Display(Name = "Mô tả")]
    [StringLength(255, ErrorMessage = "Mô tả không được vượt quá {1} ký tự.")]
    public string? Description { get; set; }

    [Display(Name = "Ghi chú")]
    [StringLength(255, ErrorMessage = "Ghi chú không được vượt quá {1} ký tự.")]
    public string? Note { get; set; }

    public IReadOnlyList<RoomTypeOptionViewModel> AvailableRoomTypes { get; set; } = Array.Empty<RoomTypeOptionViewModel>();
}
