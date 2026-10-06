namespace Hotel_Management_Website.Models.ViewModels.RoomType;

public sealed class RoomTypeDetailsViewModel
{
    public Guid Id { get; init; }

    public string RoomTypeCode { get; init; } = string.Empty;

    public string RoomTypeName { get; init; } = string.Empty;

    public int MaxAdults { get; init; }

    public int MaxChildren { get; init; }

    public decimal PricePerNight { get; init; }

    public decimal DepositPercent { get; init; }

    public decimal Area { get; init; }

    public string? Description { get; init; }

    public bool IsActive { get; init; }

    public string? Note { get; init; }

    public DateTime CreatedAt { get; init; }

    public DateTime LastModifiedAt { get; init; }

    public string StatusName =>
        IsActive ? "Đang hoạt động" : "Dừng hoạt động";

    public string StatusBadgeClass =>
        IsActive ? "text-bg-success" : "text-bg-secondary";
}
