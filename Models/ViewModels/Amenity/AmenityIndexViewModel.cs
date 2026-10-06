using Hotel_Management_Website.Models.ViewModels.UI;

namespace Hotel_Management_Website.Models.ViewModels.Amenity;

public sealed class AmenityIndexViewModel
{
    public AmenityListFilterViewModel Filter { get; init; } = new();

    public IReadOnlyList<AmenityListItemViewModel> Amenities { get; init; } = Array.Empty<AmenityListItemViewModel>();

    public int TotalCount { get; init; }

    public PaginationViewModel Pagination { get; set; } = new();

    public string? LoadError { get; init; }
}
