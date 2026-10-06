using Hotel_Management_Website.Models.ViewModels.UI;

namespace Hotel_Management_Website.Models.ViewModels.Bill;

public sealed class BillIndexViewModel
{
    public BillListFilterViewModel Filter { get; init; } = new();

    public IReadOnlyList<BillListItemViewModel> Bills { get; init; }
        = Array.Empty<BillListItemViewModel>();

    public int TotalCount { get; init; }

    public PaginationViewModel Pagination { get; set; } = new();

    public string? LoadError { get; init; }
}
