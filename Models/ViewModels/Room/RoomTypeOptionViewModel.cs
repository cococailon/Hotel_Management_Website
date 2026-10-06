namespace Hotel_Management_Website.Models.ViewModels.Room;

public sealed class RoomTypeOptionViewModel
{
    public Guid Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public bool IsActive { get; init; }
    public bool IsSelectable { get; init; }
}
