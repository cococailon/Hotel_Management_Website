namespace Hotel_Management_Website.Models.ViewModels.RoomType;

public sealed class RoomTypeAssociationDisplayViewModel
{
    public Guid? AssignmentId { get; init; }
    public Guid ItemId { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public decimal? UnitPrice { get; init; }
    public string? PriceUnitType { get; init; }
    public bool IsActive { get; init; }
    public bool MasterIsActive { get; init; }
    public bool MasterIsDeleted { get; init; }
}
