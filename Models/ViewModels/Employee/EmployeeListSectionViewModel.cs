using Hotel_Management_Website.Models.ViewModels.UI;

namespace Hotel_Management_Website.Models.ViewModels.Employee;

public sealed class EmployeeListSectionViewModel
{
    public byte Position { get; init; }

    public string Title { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public EmployeeListFilterViewModel Filter { get; init; } = new();

    public IReadOnlyList<EmployeeListItemViewModel> Employees { get; init; }
        = Array.Empty<EmployeeListItemViewModel>();

    /// <summary>
    /// Tổng số bản ghi sau khi áp dụng search/filter nhưng trước khi phân trang.
    /// </summary>
    public int TotalCount { get; init; }

    public PaginationViewModel Pagination { get; set; } = new();
}
