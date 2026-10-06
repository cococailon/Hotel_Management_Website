using Hotel_Management_Website.Models.ViewModels.UI;

namespace Hotel_Management_Website.Models.ViewModels.RoomType;

public sealed class RoomTypeIndexViewModel
{
    public RoomTypeListFilterViewModel Filter { get; init; } = new();
    public IReadOnlyList<RoomTypeListItemViewModel> RoomTypes { get; init; } = Array.Empty<RoomTypeListItemViewModel>();
    public int TotalCount { get; init; }
    public PaginationViewModel Pagination { get; set; } = new();
    public string? LoadError { get; init; }
}
