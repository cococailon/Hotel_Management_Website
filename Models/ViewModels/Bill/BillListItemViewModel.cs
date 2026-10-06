namespace Hotel_Management_Website.Models.ViewModels.Bill;

public sealed class BillListItemViewModel
{
    public Guid Id { get; init; }

    public string BillCode { get; init; } = string.Empty;

    public string BookingCode { get; init; } = string.Empty;

    public decimal? TotalAmount { get; init; }

    public decimal DepositAmount { get; init; }

    public decimal FinalAmount { get; init; }

    public DateTime? PaidAt { get; init; }

    public byte Status { get; init; }

    public string StatusName { get; init; } = string.Empty;

    public string StatusBadgeClass { get; init; } = "text-bg-secondary";
}
