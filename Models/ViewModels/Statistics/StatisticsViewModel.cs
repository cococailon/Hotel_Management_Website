namespace Hotel_Management_Website.Models.ViewModels.Statistics;

public sealed class StatisticsViewModel
{
    public DateTime StartDate { get; init; }
    public DateTime EndDate { get; init; }
    public DateTime GeneratedAt { get; init; }
    public string? LoadError { get; init; }

    public int TotalBookings { get; init; }
    public int PreBookingCount { get; init; }
    public int DirectBookingCount { get; init; }
    public int SuccessfulBookingCount { get; init; }
    public int CancelledOrFailedBookingCount { get; init; }
    public int CheckInFailureCount { get; init; }
    public int NoShowCount { get; init; }
    public int CheckedOutCount { get; init; }
    public int ActualCheckInCount { get; init; }
    public int ActualCheckOutCount { get; init; }
    public int TotalAdults { get; init; }
    public int TotalChildren { get; init; }
    public double AverageLeadTimeDays { get; init; }
    public double AverageStayNights { get; init; }

    public decimal RoomRevenue { get; init; }
    public decimal ServiceRevenue { get; init; }
    public decimal AdditionalFeeRevenue { get; init; }
    public decimal BilledRevenue { get; init; }
    public decimal DepositCollected { get; init; }
    public decimal RemainingCollected { get; init; }
    public decimal AverageBillAmount { get; init; }
    public int SuccessfulBillCount { get; init; }
    public int FailedBillCount { get; init; }
    public decimal DepositCash { get; init; }
    public decimal DepositTransfer { get; init; }
    public decimal RemainingCash { get; init; }
    public decimal RemainingTransfer { get; init; }

    public int TotalRooms { get; init; }
    public int ActiveRooms { get; init; }
    public int InactiveRooms { get; init; }
    public int OccupiedRooms { get; init; }
    public int AvailableRooms { get; init; }
    public int WaitingCleanRooms { get; init; }
    public int CleaningRooms { get; init; }
    public int MaintenanceRooms { get; init; }
    public double CurrentOccupancyRate { get; init; }
    public double PeriodOccupancyRate { get; init; }
    public double OccupiedRoomNights { get; init; }
    public double AvailableRoomNights { get; init; }

    public int ActiveRoomTypeCount { get; init; }
    public int InactiveRoomTypeCount { get; init; }
    public int ActiveServiceCount { get; init; }
    public int InactiveServiceCount { get; init; }
    public int ActiveAmenityCount { get; init; }
    public int InactiveAmenityCount { get; init; }
    public int WorkingEmployeeCount { get; init; }
    public int FormerEmployeeCount { get; init; }
    public int ActiveUserCount { get; init; }
    public int InactiveUserCount { get; init; }
    public int ActiveGuestCount { get; init; }
    public int NewGuestCount { get; init; }

    public IReadOnlyList<StatisticsRoomTypeItem> RoomTypes { get; init; } = Array.Empty<StatisticsRoomTypeItem>();
    public IReadOnlyList<StatisticsBookingStatusItem> BookingStatuses { get; init; } = Array.Empty<StatisticsBookingStatusItem>();
    public IReadOnlyList<StatisticsBookingTypeItem> BookingTypes { get; init; } = Array.Empty<StatisticsBookingTypeItem>();
    public IReadOnlyList<StatisticsSexItem> GuestSexes { get; init; } = Array.Empty<StatisticsSexItem>();
    public IReadOnlyList<StatisticsServiceItem> Services { get; init; } = Array.Empty<StatisticsServiceItem>();
    public IReadOnlyList<StatisticsFeeTypeItem> FeeTypes { get; init; } = Array.Empty<StatisticsFeeTypeItem>();
    public IReadOnlyList<StatisticsRoomRevenueItem> RoomRevenueByType { get; init; } = Array.Empty<StatisticsRoomRevenueItem>();
    public IReadOnlyList<StatisticsRevenueTrendItem> RevenueTrend { get; init; } = Array.Empty<StatisticsRevenueTrendItem>();
    public IReadOnlyList<StatisticsBookingTrendItem> BookingTrend { get; init; } = Array.Empty<StatisticsBookingTrendItem>();
    public IReadOnlyList<StatisticsGuestSpendingItem> TopGuests { get; init; } = Array.Empty<StatisticsGuestSpendingItem>();
    public IReadOnlyList<StatisticsStaffActivityItem> StaffActivities { get; init; } = Array.Empty<StatisticsStaffActivityItem>();
}

public sealed class StatisticsRoomTypeItem
{
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public int Total { get; init; }
    public int Active { get; init; }
    public int Occupied { get; init; }
    public int Available { get; init; }
    public int WaitingClean { get; init; }
    public int Cleaning { get; init; }
    public int Maintenance { get; init; }
    public double OccupancyRate { get; init; }
}

public sealed class StatisticsBookingStatusItem
{
    public byte Status { get; init; }
    public string Name { get; init; } = string.Empty;
    public int Count { get; init; }
    public double Percentage { get; init; }
}

public sealed class StatisticsBookingTypeItem
{
    public byte Type { get; init; }
    public string Name { get; init; } = string.Empty;
    public int Count { get; init; }
    public double Percentage { get; init; }
}

public sealed class StatisticsSexItem
{
    public byte? Sex { get; init; }
    public string Name { get; init; } = string.Empty;
    public int Count { get; init; }
    public double Percentage { get; init; }
}

public sealed class StatisticsServiceItem
{
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public int UsageCount { get; init; }
    public decimal Revenue { get; init; }
}

public sealed class StatisticsFeeTypeItem
{
    public byte Type { get; init; }
    public string Name { get; init; } = string.Empty;
    public int Count { get; init; }
    public decimal Revenue { get; init; }
}

public sealed class StatisticsRoomRevenueItem
{
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public decimal Revenue { get; init; }
}

public sealed class StatisticsRevenueTrendItem
{
    public DateTime Period { get; init; }
    public string Label { get; init; } = string.Empty;
    public decimal Revenue { get; init; }
}

public sealed class StatisticsBookingTrendItem
{
    public DateTime Period { get; init; }
    public string Label { get; init; } = string.Empty;
    public int Count { get; init; }
}

public sealed class StatisticsGuestSpendingItem
{
    public string GuestCode { get; init; } = string.Empty;
    public string GuestName { get; init; } = string.Empty;
    public decimal Spending { get; init; }
}

public sealed class StatisticsStaffActivityItem
{
    public string UserCode { get; init; } = string.Empty;
    public string UserName { get; init; } = string.Empty;
    public int BookingsCreated { get; init; }
    public int BillsCreated { get; init; }
}
