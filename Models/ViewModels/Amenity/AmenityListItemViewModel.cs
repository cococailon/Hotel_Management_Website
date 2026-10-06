namespace Hotel_Management_Website.Models.ViewModels.Amenity;

public sealed class AmenityListItemViewModel
{
    public Guid Id { get; init; }

    public string AmenityCode { get; init; } = string.Empty;

    public string AmenityName { get; init; } = string.Empty;

    public bool IsActive { get; init; }

    public DateTime LastModifiedAt { get; init; }

    public bool HasActiveBookings { get; init; }
}
