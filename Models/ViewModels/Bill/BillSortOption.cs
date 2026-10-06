namespace Hotel_Management_Website.Models.ViewModels.Bill;

public enum BillSortOption : byte
{
    BillCodeAsc = 0,
    BillCodeDesc = 1,
    TotalAmountAsc = 2,
    TotalAmountDesc = 3,
    DepositAmountAsc = 4,
    DepositAmountDesc = 5,
    PaidAtDesc = 6,
    PaidAtAsc = 7,
    LastModifiedDesc = 8,
    LastModifiedAsc = 9
}
