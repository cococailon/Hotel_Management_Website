namespace Hotel_Management_Website.Models.ViewModels.Employee;

public sealed class EmployeeIndexQueryViewModel
{
    public EmployeeListFilterViewModel Reception { get; set; } = new();

    public EmployeeListFilterViewModel Housekeeping { get; set; } = new();

    /// <summary>
    /// Cho phép nút "Đặt lại" chỉ reset đúng một khu vực mà người dùng đang thao tác.
    /// </summary>
    public string? ResetSection { get; set; }
}
