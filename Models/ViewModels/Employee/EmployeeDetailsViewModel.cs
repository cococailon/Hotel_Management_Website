namespace Hotel_Management_Website.Models.ViewModels.Employee;

public sealed class EmployeeDetailsViewModel
{
    // Dùng nội bộ cho route /Employee/Details/{id}; không hiển thị trên UI.
    public Guid Id { get; init; }

    public string EmployeeCode { get; init; } = string.Empty;

    public string EmployeeName { get; init; } = string.Empty;

    public string DateOfBirth { get; init; } = string.Empty;

    public int? Age { get; init; }

    public byte Sex { get; init; }

    public string SexName { get; init; } = string.Empty;

    public byte Position { get; init; }

    public string PositionName { get; init; } = string.Empty;

    public string StartDate { get; init; } = string.Empty;

    public string? EndDate { get; init; }

    public byte Status { get; init; }

    public string StatusName { get; init; } = string.Empty;

    public string StatusBadgeClass { get; init; } = "text-bg-secondary";

    public string? Note { get; init; }

    public bool IsDeleted { get; init; }

    public string DataStatusName { get; init; } = string.Empty;

    public string DataStatusBadgeClass { get; init; } = "text-bg-secondary";

    public string LastModifiedAt { get; init; } = string.Empty;
    public string CreatedAt { get; init; } = string.Empty;
}
