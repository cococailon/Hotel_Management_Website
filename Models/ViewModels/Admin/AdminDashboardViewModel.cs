namespace Hotel_Management_Website.Models.ViewModels.Admin;

public sealed class AdminDashboardViewModel
{
    public decimal RevenueToday { get; init; }

    public decimal RevenueThisMonth { get; init; }

    public int OccupiedRooms { get; init; }

    public int AvailableRooms { get; init; }

    public int RoomsNeedCleaning { get; init; }

    public int TotalBookings { get; init; }

    public DateTime GeneratedAt { get; init; }

    public string? LoadError { get; init; }
}
