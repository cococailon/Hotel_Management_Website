namespace Hotel_Management_Website.Models.ViewModels.Room;

public sealed class RoomPositionSuggestionViewModel
{
    public int Floor { get; init; }
    public int? Row { get; init; }
    public int TotalRows { get; init; }
    public IReadOnlyList<RoomPositionRowSuggestionViewModel> Rows { get; init; } = Array.Empty<RoomPositionRowSuggestionViewModel>();
}

public sealed class RoomPositionRowSuggestionViewModel
{
    public int Row { get; init; }
    public int RoomCount { get; init; }
    public int MaxLeftToRightOrder { get; init; }
}
