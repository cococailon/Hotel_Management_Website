namespace Hotel_Management_Website.Models.ViewModels.Room;

public sealed class RoomListFilterViewModel
{
    public string Search { get; set; } = string.Empty;
    public RoomSortOption Sort { get; set; } = RoomSortOption.LeftToRightAsc;
    public Guid? RoomTypeId { get; set; }
    public byte? Status { get; set; }
    public bool? IsActive { get; set; }
    public int CurrentPage { get; set; } = 1;
    public int PageSize { get; set; } = 5;
}
