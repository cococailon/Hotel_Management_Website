using System.ComponentModel.DataAnnotations;

namespace Hotel_Management_Website.Models.ViewModels.Employee;

public sealed class EmployeeCreateViewModel : IValidatableObject
{
    [Display(Name = "Mã nhân viên")]
    [Required(ErrorMessage = "Vui lòng nhập mã nhân viên.")]
    [StringLength(50, ErrorMessage = "Mã nhân viên không được vượt quá {1} ký tự.")]
    [RegularExpression(
        @"^[A-Za-z0-9]+$",
        ErrorMessage = "Mã nhân viên chỉ được chứa chữ cái và chữ số, không chứa dấu cách hoặc ký tự đặc biệt.")]
    public string EmployeeCode { get; set; } = string.Empty;

    [Display(Name = "Tên nhân viên")]
    [Required(ErrorMessage = "Vui lòng nhập tên nhân viên.")]
    [StringLength(100, ErrorMessage = "Tên nhân viên không được vượt quá {1} ký tự.")]
    public string EmployeeName { get; set; } = string.Empty;

    [Display(Name = "Ngày sinh")]
    [Required(ErrorMessage = "Vui lòng chọn ngày sinh.")]
    public DateOnly? DateOfBirth { get; set; }

    [Display(Name = "Giới tính")]
    [Required(ErrorMessage = "Vui lòng chọn giới tính.")]
    [Range(0, 2, ErrorMessage = "Giới tính không hợp lệ.")]
    public byte? Sex { get; set; }

    [Display(Name = "Chức vụ")]
    [Required(ErrorMessage = "Vui lòng chọn chức vụ.")]
    [Range(0, 1, ErrorMessage = "Chức vụ không hợp lệ.")]
    public byte? Position { get; set; }

    [Display(Name = "Ngày vào làm")]
    [Required(ErrorMessage = "Vui lòng chọn ngày vào làm.")]
    public DateOnly? StartDate { get; set; }

    [Display(Name = "Ngày nghỉ việc")]
    public DateOnly? EndDate { get; set; }

    [Display(Name = "Trạng thái")]
    [Required(ErrorMessage = "Vui lòng chọn trạng thái.")]
    [Range(0, 1, ErrorMessage = "Trạng thái không hợp lệ.")]
    public byte? Status { get; set; } = 1;

    [Display(Name = "Ghi chú")]
    [StringLength(255, ErrorMessage = "Ghi chú không được vượt quá {1} ký tự.")]
    public string? Note { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);

        if (string.IsNullOrWhiteSpace(EmployeeName))
        {
            yield return new ValidationResult(
                "Vui lòng nhập tên nhân viên.",
                new[] { nameof(EmployeeName) });
        }

        if (DateOfBirth.HasValue && DateOfBirth.Value >= today)
        {
            yield return new ValidationResult(
                "Ngày sinh phải bé hơn ngày hiện tại.",
                new[] { nameof(DateOfBirth) });
        }

        if (DateOfBirth.HasValue && StartDate.HasValue && DateOfBirth.Value >= StartDate.Value)
        {
            yield return new ValidationResult(
                "Ngày vào làm phải sau ngày sinh.",
                new[] { nameof(StartDate) });
        }

        if (Status == 0 && !EndDate.HasValue)
        {
            yield return new ValidationResult(
                "Nhân viên đã nghỉ việc phải có ngày nghỉ việc.",
                new[] { nameof(EndDate) });
        }

        if (Status == 1 && EndDate.HasValue)
        {
            yield return new ValidationResult(
                "Nhân viên đang làm không được có ngày nghỉ việc.",
                new[] { nameof(EndDate), nameof(Status) });
        }

        if (StartDate.HasValue && EndDate.HasValue && EndDate.Value < StartDate.Value)
        {
            yield return new ValidationResult(
                "Ngày nghỉ việc không được trước ngày vào làm.",
                new[] { nameof(EndDate) });
        }
    }
}
