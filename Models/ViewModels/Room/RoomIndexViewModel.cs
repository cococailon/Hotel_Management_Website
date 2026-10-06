namespace Hotel_Management_Website.Models.ViewModels.Room;

public sealed class RoomIndexViewModel
{
    public RoomListFilterViewModel Filter { get; init; } = new();
    public IReadOnlyList<RoomFloorRowViewModel> Rows { get; init; } = Array.Empty<RoomFloorRowViewModel>();
    public IReadOnlyList<RoomTypeOptionViewModel> RoomTypes { get; init; } = Array.Empty<RoomTypeOptionViewModel>();
    public int TotalRooms { get; init; }
    public int TotalPages { get; init; } = 1;
    public string? LoadError { get; init; }
}
