namespace Hotel_Management_Website.Models.ViewModels.UI;

public sealed class CrudToolbarViewModel
{
    public string CreateText { get; init; } = "Thêm mới";
    public string CreateIcon { get; init; } = "bi-plus-lg";
    public string CreateButtonId { get; init; } = "btnCreate";
    public string SearchInputId { get; init; } = "tableSearch";
    public string SearchName { get; init; } = "search";
    public string SearchPlaceholder { get; init; } = "Tìm kiếm...";
    public string SearchValue { get; init; } = string.Empty;
    public bool ShowSearch { get; init; } = true;
    public bool ShowCreate { get; init; } = true;
}
