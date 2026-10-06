namespace Hotel_Management_Website.Models.ViewModels.Guest;

public sealed class GuestIndexQueryViewModel
{
    public GuestListFilterViewModel Filter { get; set; } = new();

    public string? Reset { get; set; }
}
