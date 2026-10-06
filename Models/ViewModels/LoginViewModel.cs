using System.ComponentModel.DataAnnotations;

namespace Hotel_Management_Website.Models.ViewModels;

public class LoginViewModel
{
    [Required(ErrorMessage = "Vui lòng nhập tên đăng nhập.")]
    [StringLength(
        100,
        ErrorMessage = "Tên đăng nhập không được vượt quá 100 ký tự.")]
    [Display(Name = "Tên đăng nhập")]
    public string UserName { get; set; } = string.Empty;


    [Required(ErrorMessage = "Vui lòng nhập mật khẩu.")]
    [DataType(DataType.Password)]
    [Display(Name = "Mật khẩu")]
    public string Password { get; set; } = string.Empty;


    public string? ReturnUrl { get; set; }
}