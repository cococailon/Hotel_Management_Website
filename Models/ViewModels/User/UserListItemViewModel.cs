namespace Hotel_Management_Website.Models.ViewModels.User;

public sealed class UserListItemViewModel
{
    public Guid Id { get; init; }

    public string UserCode { get; init; } = string.Empty;

    public string UserName { get; init; } = string.Empty;

    public bool IsActive { get; init; }

    public string? EmployeeCode { get; init; }

    public DateTime LastModifiedAt { get; init; }
}
