using Hotel_Management_Website.Models.ViewModels.UI;

namespace Hotel_Management_Website.Models.ViewModels.Guest;

public sealed class GuestIndexViewModel
{
    public GuestListFilterViewModel Filter { get; init; } = new();

    public IReadOnlyList<GuestListItemViewModel> Guests { get; init; }
        = Array.Empty<GuestListItemViewModel>();

    public int TotalCount { get; init; }

    public PaginationViewModel Pagination { get; set; } = new();

    public string? LoadError { get; init; }
}
