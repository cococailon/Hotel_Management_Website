using System.ComponentModel.DataAnnotations;

namespace Hotel_Management_Website.Models.ViewModels.User;

public sealed class UserEditViewModel
{
    public Guid Id { get; set; }

    [Display(Name = "Mã tài khoản")]
    [StringLength(50, ErrorMessage = "Mã tài khoản không được vượt quá {1} ký tự.")]
    [RegularExpression(@"^\S+$", ErrorMessage = "Mã tài khoản không được chứa dấu cách hoặc khoảng trắng.")]
    public string UserCode { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập tên đăng nhập.")]
    [StringLength(100, ErrorMessage = "Tên đăng nhập không được vượt quá 100 ký tự.")]
    [Display(Name = "Tên đăng nhập")]
    public string UserName { get; set; } = string.Empty;

    [DataType(DataType.Password)]
    [Display(Name = "Mật khẩu mới")]
    public string? Password { get; set; }

    [Range(0, 2, ErrorMessage = "Vai trò tài khoản không hợp lệ.")]
    [Display(Name = "Vai trò")]
    public byte Role { get; set; }

    [Display(Name = "Nhân viên")]
    public Guid? EmployeeId { get; set; }

    [Display(Name = "Trạng thái")]
    public bool IsActive { get; set; }

    [StringLength(255, ErrorMessage = "Ghi chú không được vượt quá 255 ký tự.")]
    [Display(Name = "Ghi chú")]
    public string? Note { get; set; }

    public bool CanEditAdmin { get; set; }

    public string? ReturnUrl { get; set; }

    public IReadOnlyList<UserEmployeeOptionViewModel> Employees { get; set; }
        = Array.Empty<UserEmployeeOptionViewModel>();
}
