namespace Hotel_Management_Website.Models.ViewModels.Employee;

public sealed class EmployeeListItemViewModel
{
    public Guid Id { get; init; }

    public string EmployeeCode { get; init; } = string.Empty;

    public string EmployeeName { get; init; } = string.Empty;

    public string DateOfBirth { get; init; } = string.Empty;

    public byte Sex { get; init; }

    public string SexName { get; init; } = string.Empty;

    public byte Position { get; init; }

    public string PositionName { get; init; } = string.Empty;

    public string StartDate { get; init; } = string.Empty;

    public string? EndDate { get; init; }

    public byte Status { get; init; }

    public string StatusName { get; init; } = string.Empty;

    public string StatusBadgeClass { get; init; } = "text-bg-secondary";

    public string LastModifiedAt { get; init; } = string.Empty;
}
