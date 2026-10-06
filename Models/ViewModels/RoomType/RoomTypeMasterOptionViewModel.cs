namespace Hotel_Management_Website.Models.ViewModels.RoomType;

public sealed class RoomTypeMasterOptionViewModel
{
    public Guid Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public decimal? UnitPrice { get; init; }
    public string? PriceUnitType { get; init; }
}
