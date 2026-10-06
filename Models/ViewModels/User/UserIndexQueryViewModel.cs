namespace Hotel_Management_Website.Models.ViewModels.User;

public sealed class UserIndexQueryViewModel
{
    public UserListFilterViewModel Admin { get; set; } = new();

    public UserListFilterViewModel Reception { get; set; } = new();

    public UserListFilterViewModel Housekeeping { get; set; } = new();

    public string? ResetSection { get; set; }
}
