namespace Hotel_Management_Website.Models.ViewModels.Amenity;

public sealed class AmenityDetailsViewModel
{
    public Guid Id { get; init; }

    public string AmenityCode { get; init; } = string.Empty;

    public string AmenityName { get; init; } = string.Empty;

    public string? Description { get; init; }

    public bool IsActive { get; init; }

    public string? Note { get; init; }

    public DateTime CreatedAt { get; init; }

    public DateTime LastModifiedAt { get; init; }

    public bool HasActiveBookings { get; init; }
}
