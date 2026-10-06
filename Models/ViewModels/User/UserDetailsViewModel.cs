namespace Hotel_Management_Website.Models.ViewModels.User;

public sealed class UserDetailsViewModel
{
    public Guid Id { get; init; }

    public string UserCode { get; init; } = string.Empty;

    public string UserName { get; init; } = string.Empty;

    public byte Role { get; init; }

    public string RoleName { get; init; } = string.Empty;

    public bool IsActive { get; init; }

    public string StatusName { get; init; } = string.Empty;

    public string StatusBadgeClass { get; init; } = "text-bg-secondary";

    public string? EmployeeCode { get; init; }

    public string? EmployeeName { get; init; }

    public string? Note { get; init; }

    public string CreatedAt { get; init; } = string.Empty;

    public string LastModifiedAt { get; init; } = string.Empty;
}
