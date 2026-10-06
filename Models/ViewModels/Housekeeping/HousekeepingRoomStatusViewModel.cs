using Hotel_Management_Website.Models.ViewModels.Room;

namespace Hotel_Management_Website.Models.ViewModels.Housekeeping;

public sealed class HousekeepingRoomStatusViewModel
{
    public RoomListFilterViewModel Filter { get; init; } = new();
    public IReadOnlyList<RoomFloorRowViewModel> Rows { get; init; } = Array.Empty<RoomFloorRowViewModel>();
    public IReadOnlyList<RoomTypeOptionViewModel> RoomTypes { get; init; } = Array.Empty<RoomTypeOptionViewModel>();
    public int TotalRooms { get; init; }
    public string? LoadError { get; init; }
}
