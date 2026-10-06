using System.ComponentModel.DataAnnotations;

namespace Hotel_Management_Website.Models.ViewModels.HotelPolicy;

public sealed class HotelPolicyViewModel : IValidatableObject
{
    public Guid HotelSettingId { get; set; }
    public Guid PolicyId { get; set; }

    [Display(Name = "Giờ check in")]
    [Required(ErrorMessage = "Vui lòng nhập giờ check in.")]
    public TimeOnly CheckInTime { get; set; }

    [Display(Name = "Giờ check out")]
    [Required(ErrorMessage = "Vui lòng nhập giờ check out.")]
    public TimeOnly CheckOutTime { get; set; }

    [Display(Name = "Ghi chú HotelSettings")]
    [StringLength(255, ErrorMessage = "Ghi chú không được vượt quá 255 ký tự.")]
    public string? HotelSettingsNote { get; set; }

    [Display(Name = "Nội dung quy định")]
    public string? PolicyContent { get; set; }

    [Display(Name = "Ghi chú quy định")]
    [StringLength(255, ErrorMessage = "Ghi chú quy định không được vượt quá 255 ký tự.")]
    public string? PolicyNote { get; set; }

    public DateTime? PolicyLastModifiedAt { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var elapsedHours = ((CheckOutTime - CheckInTime).TotalHours + 24d) % 24d;

        if (Math.Abs(elapsedHours - 22d) > 0.0001d)
        {
            yield return new ValidationResult(
                "Giờ check in và giờ check out phải cách nhau đúng 22 tiếng.",
                new[] { nameof(CheckInTime), nameof(CheckOutTime) });
        }
    }
}
