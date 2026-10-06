namespace Hotel_Management_Website.Models.ViewModels.Employee;

public sealed class EmployeeIndexViewModel
{
    public EmployeeListSectionViewModel Reception { get; init; } = new();

    public EmployeeListSectionViewModel Housekeeping { get; init; } = new();

    public string? LoadError { get; init; }
}
