using System.ComponentModel.DataAnnotations;

namespace Hotel_Management_Website.Models.ViewModels.User;

public sealed class UserCreateViewModel
{
    [Required(ErrorMessage = "Vui lòng nhập mã tài khoản.")]
    [StringLength(50, ErrorMessage = "Mã tài khoản không được vượt quá {1} ký tự.")]
    [RegularExpression("^[A-Za-z0-9]+$", ErrorMessage = "Mã tài khoản chỉ được chứa chữ cái và chữ số, không chứa dấu cách hoặc ký tự đặc biệt.")]
    [Display(Name = "Mã tài khoản")]
    public string UserCode { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập tên đăng nhập.")]
    [MinLength(3, ErrorMessage = "Tên đăng nhập phải có ít nhất 3 ký tự.")]
    [StringLength(100, ErrorMessage = "Tên đăng nhập không được vượt quá 100 ký tự.")]
    [RegularExpression(@"^\S+$", ErrorMessage = "Tên đăng nhập không được chứa dấu cách hoặc khoảng trắng.")]
    [Display(Name = "Tên đăng nhập")]
    public string UserName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập mật khẩu.")]
    [MinLength(5, ErrorMessage = "Mật khẩu phải có ít nhất 5 ký tự.")]
    [Display(Name = "Mật khẩu")]
    public string Password { get; set; } = string.Empty;

    [Range(0, 2, ErrorMessage = "Vai trò tài khoản không hợp lệ.")]
    [Display(Name = "Vai trò")]
    public byte Role { get; set; }

    [Display(Name = "Nhân viên")]
    public Guid? EmployeeId { get; set; }

    [Display(Name = "Trạng thái")]
    public bool IsActive { get; set; } = true;

    [StringLength(255, ErrorMessage = "Ghi chú không được vượt quá 255 ký tự.")]
    [Display(Name = "Ghi chú")]
    public string? Note { get; set; }

    public bool CanCreateAdmin { get; set; }

    public IReadOnlyList<UserEmployeeOptionViewModel> Employees { get; set; }
        = Array.Empty<UserEmployeeOptionViewModel>();
}
