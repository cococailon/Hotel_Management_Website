namespace Hotel_Management_Website.Models.ViewModels.Amenity;

public sealed class AmenityListFilterViewModel
{
    public string Search { get; set; } = string.Empty;

    public AmenitySortOption Sort { get; set; } = AmenitySortOption.LastModifiedDesc;

    /// <summary>
    /// null = tất cả, true = đang hoạt động, false = dừng hoạt động.
    /// </summary>
    public bool? Status { get; set; }

    public int CurrentPage { get; set; } = 1;

    public int PageSize { get; set; } = 10;
}
