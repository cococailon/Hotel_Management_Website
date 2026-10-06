namespace Hotel_Management_Website.Models.ViewModels.Service;

public sealed class ServiceListFilterViewModel
{
    public string Search { get; set; } = string.Empty;

    public ServiceSortOption Sort { get; set; } = ServiceSortOption.LastModifiedDesc;

    public bool? Status { get; set; }

    public ServicePriceFilterOption PriceFilter { get; set; } = ServicePriceFilterOption.All;

    public int CurrentPage { get; set; } = 1;

    public int PageSize { get; set; } = 10;
}
