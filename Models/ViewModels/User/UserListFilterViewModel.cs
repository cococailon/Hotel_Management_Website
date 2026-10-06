namespace Hotel_Management_Website.Models.ViewModels.User;

public sealed class UserListFilterViewModel
{
    public string Search { get; set; } = string.Empty;

    public UserSortOption Sort { get; set; } = UserSortOption.LastModifiedDesc;

    public byte? IsActive { get; set; }

    public int CurrentPage { get; set; } = 1;

    public int PageSize { get; set; } = 10;
}
