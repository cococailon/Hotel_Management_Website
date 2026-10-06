using System.ComponentModel.DataAnnotations;

namespace Hotel_Management_Website.Models.ViewModels.RoomType;

public sealed class RoomTypeCreateViewModel : IValidatableObject
{
    [Display(Name = "Mã loại phòng")]
    [StringLength(50, ErrorMessage = "Mã loại phòng không được vượt quá {1} ký tự.")]
    [RegularExpression(@"^\S+$", ErrorMessage = "Mã loại phòng không được chứa dấu cách hoặc khoảng trắng.")]
    public string RoomTypeCode { get; set; } = string.Empty;

    [Display(Name = "Tên loại phòng")]
    [Required(ErrorMessage = "Vui lòng nhập tên loại phòng.")]
    [StringLength(100, ErrorMessage = "Tên loại phòng không được vượt quá {1} ký tự.")]
    public string RoomTypeName { get; set; } = string.Empty;

    [Display(Name = "Tổng người lớn")]
    [Range(1, 100, ErrorMessage = "Tổng người lớn phải từ 1 đến 100.")]
    public int MaxAdults { get; set; } = 1;

    [Display(Name = "Tổng trẻ em")]
    [Range(0, 100, ErrorMessage = "Tổng trẻ em phải từ 0 đến 100.")]
    public int MaxChildren { get; set; }

    [Display(Name = "Giá một đêm")]
    [Range(typeof(decimal), "0.01", "9999999999999999.99", ErrorMessage = "Giá một đêm phải lớn hơn 0 và không vượt quá giới hạn dữ liệu.")]
    public decimal PricePerNight { get; set; }

    [Display(Name = "Cọc")]
    [Range(typeof(decimal), "0", "100", ErrorMessage = "Phần trăm cọc phải từ 0% đến 100%.")]
    public decimal DepositPercent { get; set; }

    [Display(Name = "Diện tích")]
    [Range(typeof(decimal), "0.01", "99999.99", ErrorMessage = "Diện tích phải lớn hơn 0.")]
    public decimal Area { get; set; }

    [Display(Name = "Mô tả")]
    [StringLength(255, ErrorMessage = "Mô tả không được vượt quá {1} ký tự.")]
    public string? Description { get; set; }

    [Display(Name = "Trạng thái")]
    public bool IsActive { get; set; } = true;

    [Display(Name = "Ghi chú")]
    [StringLength(255, ErrorMessage = "Ghi chú không được vượt quá {1} ký tự.")]
    public string? Note { get; set; }

    public List<RoomTypeAssociationInputViewModel> Amenities { get; set; } = new();
    public List<RoomTypeAssociationInputViewModel> Services { get; set; } = new();

    public IReadOnlyList<RoomTypeMasterOptionViewModel> AvailableAmenities { get; set; } = Array.Empty<RoomTypeMasterOptionViewModel>();
    public IReadOnlyList<RoomTypeMasterOptionViewModel> AvailableServices { get; set; } = Array.Empty<RoomTypeMasterOptionViewModel>();

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var duplicateAmenities = Amenities
            .Where(x => x.ItemId.HasValue && x.ItemId.Value != Guid.Empty)
            .GroupBy(x => x.ItemId!.Value)
            .Any(g => g.Count() > 1);

        if (duplicateAmenities)
        {
            yield return new ValidationResult(
                "Không được thêm trùng tiện nghi cho loại phòng.",
                new[] { nameof(Amenities) });
        }

        var duplicateServices = Services
            .Where(x => x.ItemId.HasValue && x.ItemId.Value != Guid.Empty)
            .GroupBy(x => x.ItemId!.Value)
            .Any(g => g.Count() > 1);

        if (duplicateServices)
        {
            yield return new ValidationResult(
                "Không được thêm trùng dịch vụ cho loại phòng.",
                new[] { nameof(Services) });
        }
    }
}
