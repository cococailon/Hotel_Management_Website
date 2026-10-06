namespace Hotel_Management_Website.Models.ViewModels.Room;

public sealed class RoomDetailsViewModel
{
    public Guid Id { get; init; }
    public string RoomCode { get; init; } = string.Empty;
    public string RoomName { get; init; } = string.Empty;
    public string RoomTypeCode { get; init; } = string.Empty;
    public string RoomTypeName { get; init; } = string.Empty;
    public int Floor { get; init; }
    public int Row { get; init; }
    public int LeftToRightOrder { get; init; }
    public string? Description { get; init; }
    public byte Status { get; init; }
    public bool IsActive { get; init; }
    public string? Note { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime LastModifiedAt { get; init; }
    public string? ReturnUrl { get; init; }

    public string StatusName => Status switch
    {
        0 => "Trống",
        1 => "Đang sử dụng",
        2 => "Chờ dọn dẹp",
        3 => "Đang dọn dẹp",
        4 => "Bảo trì",
        _ => "Không xác định"
    };

    public string StatusBadgeClass => Status switch
    {
        0 => "text-bg-success",
        1 => "text-bg-primary",
        2 => "text-bg-warning",
        3 => "text-bg-info",
        4 => "text-bg-danger",
        _ => "text-bg-secondary"
    };

    public string ActiveStatusName => IsActive ? "Đang hoạt động" : "Dừng hoạt động";

    public string ActiveStatusBadgeClass =>
        IsActive ? "text-bg-success" : "text-bg-secondary";
}
