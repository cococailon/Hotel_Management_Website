namespace Hotel_Management_Website.Models.ViewModels.Employee;

public sealed class EmployeeListFilterViewModel
{
    public string Search { get; set; } = string.Empty;

    public EmployeeSortOption Sort { get; set; } = EmployeeSortOption.LastModifiedDesc;

    public byte? Sex { get; set; }

    public byte? Status { get; set; }

    public int CurrentPage { get; set; } = 1;

    public int PageSize { get; set; } = 10;
}
