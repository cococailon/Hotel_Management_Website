namespace Hotel_Management_Website.Models.ViewModels.Service;

public sealed class ServiceDetailsViewModel
{
    public Guid Id { get; init; }
    public string ServiceCode { get; init; } = string.Empty;
    public string ServiceName { get; init; } = string.Empty;
    public decimal? UnitPrice { get; init; }
    public string? PriceUnitType { get; init; }
    public bool IsActive { get; init; }
    public string? Description { get; init; }
    public string? Note { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime LastModifiedAt { get; init; }
    public bool HasActiveBookings { get; init; }
}
