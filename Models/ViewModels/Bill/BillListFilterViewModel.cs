namespace Hotel_Management_Website.Models.ViewModels.Bill;

public sealed class BillListFilterViewModel
{
    public string Search { get; set; } = string.Empty;

    public BillSortOption Sort { get; set; } = BillSortOption.LastModifiedDesc;

    public byte? Status { get; set; }

    public int CurrentPage { get; set; } = 1;

    public int PageSize { get; set; } = 10;
}
