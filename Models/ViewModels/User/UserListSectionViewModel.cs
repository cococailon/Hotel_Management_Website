using Hotel_Management_Website.Models.ViewModels.UI;

namespace Hotel_Management_Website.Models.ViewModels.User;

public sealed class UserListSectionViewModel
{
    public byte Role { get; init; }

    public string Title { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public string Icon { get; init; } = "bi-person-lock";

    public UserListFilterViewModel Filter { get; init; } = new();

    public IReadOnlyList<UserListItemViewModel> Users { get; init; }
        = Array.Empty<UserListItemViewModel>();

    /// <summary>
    /// Tổng số bản ghi sau search/filter nhưng trước phân trang.
    /// </summary>
    public int TotalCount { get; init; }

    public PaginationViewModel Pagination { get; set; } = new();

    public bool ShowCreate { get; init; }
}
