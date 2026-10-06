namespace Hotel_Management_Website.Models.ViewModels.Amenity;

public sealed class AmenityIndexQueryViewModel
{
    public AmenityListFilterViewModel Filter { get; set; } = new();

    public string? Reset { get; set; }
}
