namespace Hotel_Management_Website.Models.ViewModels.Guest;

public sealed class GuestListItemViewModel
{
    public Guid Id { get; init; }
    public string GuestCode { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public DateTime? DateOfBirth { get; init; }
    public byte? Sex { get; init; }
    public string? PhoneNumber { get; init; }
    public string? Email { get; init; }
    public DateTime LastModifiedAt { get; init; }

    public bool CanEditDetails { get; set; }
    public bool RequireFullDetails { get; set; }
    public bool CanEditNoteOnly { get; set; }
    public bool CanDelete { get; set; }

    public string SexName => Sex switch
    {
        0 => "Nam",
        1 => "Nữ",
        2 => "Khác",
        _ => "Chưa cập nhật"
    };
}
