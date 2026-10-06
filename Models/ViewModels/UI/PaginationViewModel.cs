namespace Hotel_Management_Website.Models.ViewModels.UI;

public sealed class PaginationViewModel
{
    public int CurrentPage { get; init; } = 1;

    public int PageSize { get; init; } = 10;

    public int TotalItems { get; init; }

    public string PageParam { get; init; } = "currentPage";

    public string PageSizeParam { get; init; } = "pageSize";

    public string BaseUrl { get; init; } = "";

    public string ItemLabel { get; init; } = "";

    public IReadOnlyDictionary<string, string?> QueryParameters { get; init; }
        = new Dictionary<string, string?>();

    public int TotalPages =>
        PageSize <= 0
            ? 0
            : (int)Math.Ceiling(TotalItems / (double)PageSize);

    public bool HasPreviousPage => CurrentPage > 1;

    public bool HasNextPage => CurrentPage < TotalPages;

    public int FirstItemIndex =>
        TotalItems == 0
            ? 0
            : ((CurrentPage - 1) * PageSize) + 1;

    public int LastItemIndex =>
        TotalItems == 0
            ? 0
            : Math.Min(CurrentPage * PageSize, TotalItems);
}
