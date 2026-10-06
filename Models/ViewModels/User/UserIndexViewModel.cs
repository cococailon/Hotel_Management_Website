namespace Hotel_Management_Website.Models.ViewModels.User;

public sealed class UserIndexViewModel
{
    public bool IsFullAdmin { get; init; }

    public UserListSectionViewModel AdminUsers { get; init; } = new()
    {
        Role = 0,
        Title = "Admin",
        Description = "Tài khoản quản trị hệ thống.",
        Icon = "bi-person-gear"
    };

    public UserListSectionViewModel ReceptionUsers { get; init; } = new()
    {
        Role = 1,
        Title = "Lễ Tân",
        Description = "Tài khoản dành cho bộ phận lễ tân.",
        Icon = "bi-person-vcard"
    };

    public UserListSectionViewModel HousekeepingUsers { get; init; } = new()
    {
        Role = 2,
        Title = "Buồng Phòng",
        Description = "Tài khoản dành cho bộ phận buồng phòng.",
        Icon = "bi-person-check"
    };
}
