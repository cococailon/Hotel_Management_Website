namespace Hotel_Management_Website.Models.ViewModels.RoomType;

public sealed class RoomTypeListFilterViewModel
{
    public string Search { get; set; } = string.Empty;

    public RoomTypeSortOption Sort { get; set; } = RoomTypeSortOption.LastModifiedDesc;

    public bool? IsActive { get; set; }

    public int CurrentPage { get; set; } = 1;

    public int PageSize { get; set; } = 10;
}
