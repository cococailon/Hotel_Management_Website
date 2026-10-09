using System.ComponentModel.DataAnnotations;

namespace Hotel_Management_Website.Models.ViewModels.Service;

public sealed class ServiceCreateViewModel
{
    [Display(Name = "Mã dịch vụ")]
    [Required(ErrorMessage = "Vui lòng nhập mã dịch vụ.")]
    [StringLength(50, ErrorMessage = "Mã dịch vụ không được vượt quá {1} ký tự.")]
    [RegularExpression(@"^[A-Za-z0-9]+$", ErrorMessage = "Mã dịch vụ chỉ được bao gồm chữ hoa, chữ thường và số; không chứa dấu cách hoặc ký tự đặc biệt.")]
    public string ServiceCode { get; set; } = string.Empty;

    [Display(Name = "Tên dịch vụ")]
    [Required(ErrorMessage = "Vui lòng nhập tên dịch vụ.")]
    [StringLength(100, ErrorMessage = "Tên dịch vụ không được vượt quá {1} ký tự.")]
    public string ServiceName { get; set; } = string.Empty;

    [Display(Name = "Đơn giá")]
    [Range(typeof(decimal), "0", "9999999999999999.99", ErrorMessage = "Đơn giá phải lớn hơn hoặc bằng 0.")]
    public decimal? UnitPrice { get; set; }

    [Display(Name = "Đơn vị")]
    [StringLength(50, ErrorMessage = "Đơn vị không được vượt quá {1} ký tự.")]
    [RegularExpression(@"^(/[^/\s]+)+$", ErrorMessage = "Đơn vị phải có dạng /x/x/x..., ví dụ /người/lần/kg.")]
    public string? PriceUnitType { get; set; }

    [Display(Name = "Trạng thái")]
    public bool IsActive { get; set; } = true;

    [Display(Name = "Mô tả")]
    [StringLength(255, ErrorMessage = "Mô tả không được vượt quá {1} ký tự.")]
    public string? Description { get; set; }

    [Display(Name = "Ghi chú")]
    [StringLength(255, ErrorMessage = "Ghi chú không được vượt quá {1} ký tự.")]
    public string? Note { get; set; }
}
