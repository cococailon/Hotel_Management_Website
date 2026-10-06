namespace Hotel_Management_Website.Models.ViewModels.Bill;

public sealed class BillIndexQueryViewModel
{
    public BillListFilterViewModel Filter { get; set; } = new();

    public string? Reset { get; set; }
}
