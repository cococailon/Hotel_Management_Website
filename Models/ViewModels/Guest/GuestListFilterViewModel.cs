namespace Hotel_Management_Website.Models.ViewModels.Guest;

public sealed class GuestListFilterViewModel
{
    public string Search { get; set; } = string.Empty;

    public GuestSortOption Sort { get; set; } = GuestSortOption.LastModifiedDesc;

    public byte? Sex { get; set; }

    public int CurrentPage { get; set; } = 1;

    public int PageSize { get; set; } = 10;
}
