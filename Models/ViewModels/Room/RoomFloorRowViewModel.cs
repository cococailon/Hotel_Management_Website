namespace Hotel_Management_Website.Models.ViewModels.Room;

public sealed class RoomFloorRowViewModel
{
    public int Floor { get; init; }
    public int Row { get; init; }
    public IReadOnlyList<RoomListItemViewModel> Rooms { get; init; } = Array.Empty<RoomListItemViewModel>();
}
