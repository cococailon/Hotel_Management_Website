namespace Hotel_Management_Website.Models.ViewModels.RoomType;

public sealed class RoomTypeIndexQueryViewModel
{
    public RoomTypeListFilterViewModel Filter { get; set; } = new();

    public string? Reset { get; set; }
}
