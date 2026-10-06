using Hotel_Management_Website.Models.ViewModels.UI;

namespace Hotel_Management_Website.Models.ViewModels.Service;

public sealed class ServiceIndexViewModel
{
    public ServiceListFilterViewModel Filter { get; init; } = new();
    public IReadOnlyList<ServiceListItemViewModel> Services { get; init; } = Array.Empty<ServiceListItemViewModel>();
    public int TotalCount { get; init; }
    public PaginationViewModel Pagination { get; set; } = new();
    public string? LoadError { get; init; }
}
