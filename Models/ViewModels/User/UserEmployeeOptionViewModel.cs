namespace Hotel_Management_Website.Models.ViewModels.User;

public sealed class UserEmployeeOptionViewModel
{
    public Guid Id { get; init; }

    public string EmployeeCode { get; init; } = string.Empty;

    public string EmployeeName { get; init; } = string.Empty;

    public byte Position { get; init; }

    public bool IsActive { get; init; }

    public bool IsAssigned { get; init; }
}
