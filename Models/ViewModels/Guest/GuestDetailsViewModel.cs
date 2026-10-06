namespace Hotel_Management_Website.Models.ViewModels.Guest;

public sealed class GuestDetailsViewModel
{
    public Guid Id { get; init; }
    public string GuestCode { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public DateTime? DateOfBirth { get; init; }
    public byte? Sex { get; init; }
    public string SexName => Sex switch
    {
        0 => "Nam",
        1 => "Nữ",
        2 => "Khác",
        _ => "Chưa cập nhật"
    };
    public string? Address { get; init; }
    public string? PhoneNumber { get; init; }
    public string? Email { get; init; }
    public string? IdentityNumber { get; init; }
    public byte? IdentityType { get; init; }
    public string IdentityTypeName => IdentityType switch
    {
        0 => "CCCD",
        1 => "Giấy phép lái xe",
        2 => "Hộ chiếu",
        _ => "Chưa cập nhật"
    };
    public string? Note { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime LastModifiedAt { get; init; }
}
