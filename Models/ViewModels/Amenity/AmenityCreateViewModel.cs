using System.ComponentModel.DataAnnotations;

namespace Hotel_Management_Website.Models.ViewModels.Amenity;

public sealed class AmenityCreateViewModel
{
    [Display(Name = "Mã tiện nghi")]
    [StringLength(50, ErrorMessage = "Mã tiện nghi không được vượt quá {1} ký tự.")]
    [RegularExpression(@"^\S*$", ErrorMessage = "Mã tiện nghi không được chứa dấu cách hoặc khoảng trắng.")]
    public string AmenityCode { get; set; } = string.Empty;

    [Display(Name = "Tên tiện nghi")]
    [Required(ErrorMessage = "Vui lòng nhập tên tiện nghi.")]
    [StringLength(100, ErrorMessage = "Tên tiện nghi không được vượt quá {1} ký tự.")]
    public string AmenityName { get; set; } = string.Empty;

    [Display(Name = "Mô tả")]
    [StringLength(255, ErrorMessage = "Mô tả không được vượt quá {1} ký tự.")]
    public string? Description { get; set; }

    [Display(Name = "Ghi chú")]
    [StringLength(255, ErrorMessage = "Ghi chú không được vượt quá {1} ký tự.")]
    public string? Note { get; set; }

    public bool IsActive { get; set; } = true;
}
