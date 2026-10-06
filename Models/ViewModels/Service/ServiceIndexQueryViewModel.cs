namespace Hotel_Management_Website.Models.ViewModels.Service;

public sealed class ServiceIndexQueryViewModel
{
    public ServiceListFilterViewModel Filter { get; set; } = new();

    public string? Reset { get; set; }
}
