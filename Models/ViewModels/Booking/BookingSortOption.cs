namespace Hotel_Management_Website.Models.ViewModels.Booking;

public enum BookingSortOption : byte
{
    BookingCodeAsc = 0,
    BookingCodeDesc = 1,
    CreatedAtDesc = 2,
    CreatedAtAsc = 3,
    LastModifiedDesc = 4,
    LastModifiedAsc = 5,
    ExpectedCheckInDesc = 6,
    ExpectedCheckInAsc = 7,
    ExpectedCheckOutDesc = 8,
    ExpectedCheckOutAsc = 9,
    TotalAdultsAsc = 10,
    TotalAdultsDesc = 11
}
