using Hotel_Management_Website.IServices;
using Hotel_Management_Website.Models;
using Hotel_Management_Website.Models.ViewModels.Statistics;
using Microsoft.EntityFrameworkCore;

namespace Hotel_Management_Website.Services;

public sealed class StatisticsService : IStatisticsService
{
    private static readonly byte[] FailureStatuses = { 4, 5, 7, 8, 9 };

    private readonly AppDbContext _context;

    public StatisticsService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<StatisticsViewModel> GetAsync(
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default)
    {
        var start = startDate.Date;
        var end = endDate.Date;

        if (end < start)
        {
            (start, end) = (end, start);
        }

        var periodEndExclusive = end.AddDays(1);
        var now = DateTime.Now;

        // =========================================================
        // CURRENT ROOM INVENTORY
        // =========================================================
        var currentRooms = await _context.Rooms
            .AsNoTracking()
            .Where(x => !x.IsDeleted)
            .Select(x => new
            {
                x.Id,
                x.RoomTypeId,
                x.Status,
                x.IsActive
            })
            .ToListAsync(cancellationToken);

        var totalRooms = currentRooms.Count;
        var activeRooms = currentRooms.Count(x => x.IsActive);
        var inactiveRooms = totalRooms - activeRooms;
        var occupiedRooms = currentRooms.Count(x => x.IsActive && x.Status == 1);
        var availableRooms = currentRooms.Count(x => x.IsActive && x.Status == 0);
        var waitingCleanRooms = currentRooms.Count(x => x.IsActive && x.Status == 2);
        var cleaningRooms = currentRooms.Count(x => x.IsActive && x.Status == 3);
        var maintenanceRooms = currentRooms.Count(x => x.Status == 4);
        var currentOccupancyRate = activeRooms == 0
            ? 0
            : occupiedRooms * 100d / activeRooms;

        // =========================================================
        // BOOKINGS IN SELECTED PERIOD (BY CREATED DATE)
        // =========================================================
        var periodBookings = await _context.Bookings
            .AsNoTracking()
            .Where(x =>
                !x.IsDeleted &&
                x.CreatedAt >= start &&
                x.CreatedAt < periodEndExclusive)
            .Select(x => new
            {
                x.Id,
                x.GuestId,
                x.BookingType,
                x.Status,
                x.TotalAdults,
                x.TotalChildren,
                x.ExpectedCheckInAt,
                x.ExpectedCheckOutAt,
                x.ActualCheckInAt,
                x.ActualCheckOutAt,
                x.CreatedAt,
                x.CreatedBy
            })
            .ToListAsync(cancellationToken);

        var totalBookings = periodBookings.Count;
        var preBookingCount = periodBookings.Count(x => x.BookingType == 0);
        var directBookingCount = periodBookings.Count(x => x.BookingType == 1);
        var successfulBookingCount = periodBookings.Count(x => x.Status is 1 or 2 or 3);
        var cancelledOrFailedBookingCount = periodBookings.Count(x => FailureStatuses.Contains(x.Status));
        var checkInFailureCount = periodBookings.Count(x => x.Status == 5);
        var noShowCount = periodBookings.Count(x => x.Status == 8);
        var checkedOutCount = periodBookings.Count(x => x.Status == 3);
        var actualCheckInCount = await _context.Bookings
            .AsNoTracking()
            .CountAsync(x =>
                !x.IsDeleted &&
                x.ActualCheckInAt.HasValue &&
                x.ActualCheckInAt.Value >= start &&
                x.ActualCheckInAt.Value < periodEndExclusive,
                cancellationToken);
        var actualCheckOutCount = await _context.Bookings
            .AsNoTracking()
            .CountAsync(x =>
                !x.IsDeleted &&
                x.ActualCheckOutAt.HasValue &&
                x.ActualCheckOutAt.Value >= start &&
                x.ActualCheckOutAt.Value < periodEndExclusive,
                cancellationToken);
        var totalAdults = periodBookings.Sum(x => x.TotalAdults);
        var totalChildren = periodBookings.Sum(x => x.TotalChildren);

        var leadTimes = periodBookings
            .Select(x => (x.ExpectedCheckInAt - x.CreatedAt).TotalDays)
            .Where(x => x >= 0)
            .ToList();

        var stayLengths = periodBookings
            .Where(x => !FailureStatuses.Contains(x.Status))
            .Select(x =>
            {
                var checkIn = x.ActualCheckInAt ?? x.ExpectedCheckInAt;
                var checkOut = x.ActualCheckOutAt ?? x.ExpectedCheckOutAt;
                return Math.Max(0d, (checkOut.Date - checkIn.Date).TotalDays);
            })
            .Where(x => x >= 0)
            .ToList();

        // =========================================================
        // BOOKING ROOM-NIGHTS OVERLAPPING SELECTED PERIOD
        // Only rooms actually assigned to BookingRoom are counted.
        // Cancelled/failed/no-show bookings are excluded.
        // =========================================================
        var activeBookingRoomRows = await _context.BookingRooms
            .AsNoTracking()
            .Where(x =>
                !x.IsDeleted &&
                !x.Booking.IsDeleted &&
                x.RoomId.HasValue &&
                !FailureStatuses.Contains(x.Booking.Status) &&
                (x.Booking.ActualCheckOutAt ?? x.Booking.ExpectedCheckOutAt) > start &&
                (x.Booking.ActualCheckInAt ?? x.Booking.ExpectedCheckInAt) < periodEndExclusive)
            .Select(x => new
            {
                x.RoomId,
                x.Booking.ExpectedCheckInAt,
                x.Booking.ExpectedCheckOutAt,
                x.Booking.ActualCheckInAt,
                x.Booking.ActualCheckOutAt
            })
            .ToListAsync(cancellationToken);

        double occupiedRoomNights = 0;

        foreach (var room in activeBookingRoomRows)
        {
            var checkIn = (room.ActualCheckInAt ?? room.ExpectedCheckInAt).Date;
            var checkOut = (room.ActualCheckOutAt ?? room.ExpectedCheckOutAt).Date;

            var overlapStart = checkIn > start ? checkIn : start;
            var overlapEnd = checkOut < periodEndExclusive.Date ? checkOut : periodEndExclusive.Date;

            var nights = (overlapEnd - overlapStart).TotalDays;
            if (nights > 0)
            {
                occupiedRoomNights += nights;
            }
        }

        var availableRoomNights = activeRooms * (periodEndExclusive.Date - start).TotalDays;
        var periodOccupancyRate = availableRoomNights <= 0
            ? 0
            : Math.Min(100d, occupiedRoomNights * 100d / availableRoomNights);

        // =========================================================
        // SUCCESSFUL BILLS IN SELECTED PERIOD
        // PaymentDate is preferred; CreatedAt is fallback for older data.
        // =========================================================
        var successfulBills = await _context.Bills
            .AsNoTracking()
            .Where(x =>
                !x.IsDeleted &&
                x.Status == 0 &&
                (x.PaymentDate ?? x.CreatedAt) >= start &&
                (x.PaymentDate ?? x.CreatedAt) < periodEndExclusive)
            .Select(x => new
            {
                x.Id,
                x.BookingId,
                x.TotalAmount,
                x.DepositAmount,
                x.FinalAmount,
                x.PaymentMethod,
                x.PaymentDate,
                x.CreatedAt,
                x.CreatedBy
            })
            .ToListAsync(cancellationToken);

        var failedBillCount = await _context.Bills
            .AsNoTracking()
            .CountAsync(x =>
                !x.IsDeleted &&
                x.Status == 1 &&
                (x.PaymentDate ?? x.CreatedAt) >= start &&
                (x.PaymentDate ?? x.CreatedAt) < periodEndExclusive,
                cancellationToken);

        var billedRevenue = successfulBills.Sum(x => x.TotalAmount ?? x.FinalAmount);
        var remainingCollected = successfulBills.Sum(x => x.FinalAmount);
        var averageBillAmount = successfulBills.Count == 0
            ? 0m
            : successfulBills.Average(x => x.FinalAmount);

        var successfulBillIds = successfulBills.Select(x => x.Id).ToHashSet();

        // =========================================================
        // DEPOSIT COLLECTIONS IN SELECTED PERIOD
        // =========================================================
        var depositRows = await _context.Bookings
            .AsNoTracking()
            .Where(x =>
                !x.IsDeleted &&
                x.DepositAmount > 0 &&
                x.PaymentDate.HasValue &&
                x.PaymentDate.Value >= start &&
                x.PaymentDate.Value < periodEndExclusive)
            .Select(x => new
            {
                x.DepositAmount,
                x.PaymentMethod
            })
            .ToListAsync(cancellationToken);

        var depositCollected = depositRows.Sum(x => x.DepositAmount);
        var depositCash = depositRows
            .Where(x => x.PaymentMethod == 0)
            .Sum(x => x.DepositAmount);
        var depositTransfer = depositRows
            .Where(x => x.PaymentMethod == 1)
            .Sum(x => x.DepositAmount);

        var remainingCash = successfulBills
            .Where(x => x.PaymentMethod == 0)
            .Sum(x => x.FinalAmount);
        var remainingTransfer = successfulBills
            .Where(x => x.PaymentMethod == 1)
            .Sum(x => x.FinalAmount);

        // =========================================================
        // ROOM REVENUE / BILL DETAIL
        // =========================================================
        var roomRevenueRows = await _context.BillDetails
            .AsNoTracking()
            .Where(x =>
                !x.IsDeleted &&
                successfulBillIds.Contains(x.BillId))
            .Select(x => new
            {
                x.TotalPrice,
                RoomTypeCode = x.BookingRoom.RoomType.RoomTypeCode,
                RoomTypeName = x.BookingRoom.RoomType.RoomTypeName
            })
            .ToListAsync(cancellationToken);

        var roomRevenue = roomRevenueRows.Sum(x => x.TotalPrice);

        var roomRevenueByType = roomRevenueRows
            .GroupBy(x => new { x.RoomTypeCode, x.RoomTypeName })
            .Select(g => new StatisticsRoomRevenueItem
            {
                Code = g.Key.RoomTypeCode,
                Name = g.Key.RoomTypeName,
                Revenue = g.Sum(x => x.TotalPrice)
            })
            .OrderByDescending(x => x.Revenue)
            .ThenBy(x => x.Code)
            .ToList();

        // =========================================================
        // SERVICES USED IN PERIOD
        // =========================================================
        var serviceRows = await _context.BookingRoomServices
            .AsNoTracking()
            .Where(x =>
                !x.IsDeleted &&
                x.ServiceId.HasValue &&
                x.UsedAt >= start &&
                x.UsedAt < periodEndExclusive &&
                !x.BookingRoom.IsDeleted &&
                !x.BookingRoom.Booking.IsDeleted &&
                !FailureStatuses.Contains(x.BookingRoom.Booking.Status))
            .Select(x => new
            {
                x.TotalPrice,
                ServiceCode = x.Service != null ? x.Service.ServiceCode : "",
                ServiceName = x.Service != null ? x.Service.ServiceName : "Dịch vụ đã ngừng tồn tại"
            })
            .ToListAsync(cancellationToken);

        var serviceRevenue = serviceRows.Sum(x => x.TotalPrice);

        var services = serviceRows
            .GroupBy(x => new { x.ServiceCode, x.ServiceName })
            .Select(g => new StatisticsServiceItem
            {
                Code = g.Key.ServiceCode,
                Name = g.Key.ServiceName,
                UsageCount = g.Count(),
                Revenue = g.Sum(x => x.TotalPrice)
            })
            .OrderByDescending(x => x.Revenue)
            .ThenByDescending(x => x.UsageCount)
            .Take(10)
            .ToList();

        // =========================================================
        // ADDITIONAL FEES IN PERIOD
        // =========================================================
        var feeRows = await _context.AdditionalFeeRooms
            .AsNoTracking()
            .Where(x =>
                !x.IsDeleted &&
                x.CreatedAt >= start &&
                x.CreatedAt < periodEndExclusive &&
                !x.BookingRoom.IsDeleted &&
                !x.BookingRoom.Booking.IsDeleted &&
                !FailureStatuses.Contains(x.BookingRoom.Booking.Status))
            .Select(x => new
            {
                x.TotalFee,
                x.FeeType
            })
            .ToListAsync(cancellationToken);

        var additionalFeeRevenue = feeRows.Sum(x => x.TotalFee);

        var feeTypes = new[]
        {
            (Type: (byte)0, Name: "Phí mua đồ hộ"),
            (Type: (byte)1, Name: "Phí hỏng hóc/mất mát"),
            (Type: (byte)2, Name: "Khác")
        };

        var feeTypeStats = feeTypes
            .Select(x => new StatisticsFeeTypeItem
            {
                Type = x.Type,
                Name = x.Name,
                Count = feeRows.Count(f => f.FeeType == x.Type),
                Revenue = feeRows.Where(f => f.FeeType == x.Type).Sum(f => f.TotalFee)
            })
            .ToList();

        // =========================================================
        // BOOKING STATUS / TYPE
        // =========================================================
        var statusNames = new Dictionary<byte, string>
        {
            [0] = "Chờ xác nhận",
            [1] = "Chờ check in",
            [2] = "Đang ở",
            [3] = "Đã check out",
            [4] = "Xác nhận thất bại",
            [5] = "Check in thất bại",
            [6] = "Check out thất bại",
            [7] = "Khách hủy trước check in",
            [8] = "Vắng mặt không thông báo/liên lạc được",
            [9] = "Khách sạn hủy"
        };

        var bookingStatuses = statusNames
            .Select(x => new StatisticsBookingStatusItem
            {
                Status = x.Key,
                Name = x.Value,
                Count = periodBookings.Count(b => b.Status == x.Key),
                Percentage = totalBookings == 0
                    ? 0
                    : periodBookings.Count(b => b.Status == x.Key) * 100d / totalBookings
            })
            .ToList();

        var bookingTypes = new[]
        {
            new StatisticsBookingTypeItem
            {
                Type = 0,
                Name = "Đặt trước",
                Count = preBookingCount,
                Percentage = totalBookings == 0 ? 0 : preBookingCount * 100d / totalBookings
            },
            new StatisticsBookingTypeItem
            {
                Type = 1,
                Name = "Trực tiếp tại quầy",
                Count = directBookingCount,
                Percentage = totalBookings == 0 ? 0 : directBookingCount * 100d / totalBookings
            }
        };

        // =========================================================
        // GUEST DEMOGRAPHICS IN PERIOD
        // =========================================================
        var guestIds = periodBookings
            .Where(x => x.GuestId.HasValue)
            .Select(x => x.GuestId!.Value)
            .Distinct()
            .ToList();

        var guestSexRows = guestIds.Count == 0
            ? new List<byte?>()
            : await _context.Guests
                .AsNoTracking()
                .Where(x => !x.IsDeleted && guestIds.Contains(x.Id))
                .Select(x => x.Sex)
                .ToListAsync(cancellationToken);

        var sexOptions = new[]
        {
            (Sex: (byte?)0, Name: "Nam"),
            (Sex: (byte?)1, Name: "Nữ"),
            (Sex: (byte?)2, Name: "Khác"),
            (Sex: (byte?)null, Name: "Chưa cập nhật")
        };

        var guestSexes = sexOptions
            .Select(x => new StatisticsSexItem
            {
                Sex = x.Sex,
                Name = x.Name,
                Count = guestSexRows.Count(s => s == x.Sex),
                Percentage = guestSexRows.Count == 0
                    ? 0
                    : guestSexRows.Count(s => s == x.Sex) * 100d / guestSexRows.Count
            })
            .ToList();

        // =========================================================
        // TOP GUEST SPENDING
        // =========================================================
        var topGuestRows = await _context.Bills
            .AsNoTracking()
            .Where(x =>
                !x.IsDeleted &&
                x.Status == 0 &&
                (x.PaymentDate ?? x.CreatedAt) >= start &&
                (x.PaymentDate ?? x.CreatedAt) < periodEndExclusive &&
                x.Booking.GuestId.HasValue &&
                x.Booking.Guest != null &&
                !x.Booking.Guest.IsDeleted)
            .Select(x => new
            {
                GuestCode = x.Booking.Guest!.GuestCode,
                GuestName = x.Booking.Guest.Name,
                Amount = x.FinalAmount
            })
            .ToListAsync(cancellationToken);

        var topGuests = topGuestRows
            .GroupBy(x => new { x.GuestCode, x.GuestName })
            .Select(g => new StatisticsGuestSpendingItem
            {
                GuestCode = g.Key.GuestCode,
                GuestName = g.Key.GuestName,
                Spending = g.Sum(x => x.Amount)
            })
            .OrderByDescending(x => x.Spending)
            .Take(10)
            .ToList();

        // =========================================================
        // ROOM TYPE CURRENT STATUS
        // =========================================================
        var roomTypesData = await _context.RoomTypes
            .AsNoTracking()
            .Where(x => !x.IsDeleted)
            .Select(x => new
            {
                x.Id,
                x.RoomTypeCode,
                x.RoomTypeName,
                x.IsActive
            })
            .ToListAsync(cancellationToken);

        var roomTypes = roomTypesData
            .Select(type =>
            {
                var rooms = currentRooms.Where(r => r.RoomTypeId == type.Id).ToList();
                var active = rooms.Count(r => r.IsActive);
                var occupied = rooms.Count(r => r.IsActive && r.Status == 1);

                return new StatisticsRoomTypeItem
                {
                    Code = type.RoomTypeCode,
                    Name = type.RoomTypeName,
                    Total = rooms.Count,
                    Active = active,
                    Occupied = occupied,
                    Available = rooms.Count(r => r.IsActive && r.Status == 0),
                    WaitingClean = rooms.Count(r => r.IsActive && r.Status == 2),
                    Cleaning = rooms.Count(r => r.IsActive && r.Status == 3),
                    Maintenance = rooms.Count(r => r.Status == 4),
                    OccupancyRate = active == 0 ? 0 : occupied * 100d / active
                };
            })
            .OrderByDescending(x => x.Active)
            .ThenBy(x => x.Code)
            .ToList();

        // =========================================================
        // SYSTEM RESOURCES
        // =========================================================
        var activeRoomTypeCount = roomTypesData.Count(x => x.IsActive);
        var inactiveRoomTypeCount = roomTypesData.Count - activeRoomTypeCount;

        var serviceCounts = await _context.Services
            .AsNoTracking()
            .Where(x => !x.IsDeleted)
            .GroupBy(x => x.IsActive)
            .Select(g => new { IsActive = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var activeServiceCount = serviceCounts.FirstOrDefault(x => x.IsActive)?.Count ?? 0;
        var inactiveServiceCount = serviceCounts.FirstOrDefault(x => !x.IsActive)?.Count ?? 0;

        var amenityCounts = await _context.Amenities
            .AsNoTracking()
            .Where(x => !x.IsDeleted)
            .GroupBy(x => x.IsActive)
            .Select(g => new { IsActive = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var activeAmenityCount = amenityCounts.FirstOrDefault(x => x.IsActive)?.Count ?? 0;
        var inactiveAmenityCount = amenityCounts.FirstOrDefault(x => !x.IsActive)?.Count ?? 0;

        var employeeCounts = await _context.Employees
            .AsNoTracking()
            .Where(x => !x.IsDeleted)
            .GroupBy(x => x.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var workingEmployeeCount = employeeCounts.FirstOrDefault(x => x.Status == 1)?.Count ?? 0;
        var formerEmployeeCount = employeeCounts.FirstOrDefault(x => x.Status == 0)?.Count ?? 0;

        var userCounts = await _context.Users
            .AsNoTracking()
            .Where(x => !x.IsDeleted)
            .GroupBy(x => x.IsActive)
            .Select(g => new { IsActive = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var activeUserCount = userCounts.FirstOrDefault(x => x.IsActive)?.Count ?? 0;
        var inactiveUserCount = userCounts.FirstOrDefault(x => !x.IsActive)?.Count ?? 0;

        var activeGuestCount = await _context.Guests
            .AsNoTracking()
            .CountAsync(x => !x.IsDeleted, cancellationToken);

        var newGuestCount = await _context.Guests
            .AsNoTracking()
            .CountAsync(x =>
                !x.IsDeleted &&
                x.CreatedAt >= start &&
                x.CreatedAt < periodEndExclusive,
                cancellationToken);

        // =========================================================
        // REVENUE / BOOKING TRENDS BY MONTH
        // =========================================================
        var revenueTrend = successfulBills
            .Select(x => new
            {
                Period = new DateTime(
                    (x.PaymentDate ?? x.CreatedAt).Year,
                    (x.PaymentDate ?? x.CreatedAt).Month,
                    1),
                Amount = x.TotalAmount ?? x.FinalAmount
            })
            .GroupBy(x => x.Period)
            .OrderBy(g => g.Key)
            .Select(g => new StatisticsRevenueTrendItem
            {
                Period = g.Key,
                Label = $"{g.Key:MM/yyyy}",
                Revenue = g.Sum(x => x.Amount)
            })
            .ToList();

        var bookingTrend = periodBookings
            .GroupBy(x => new DateTime(x.CreatedAt.Year, x.CreatedAt.Month, 1))
            .OrderBy(g => g.Key)
            .Select(g => new StatisticsBookingTrendItem
            {
                Period = g.Key,
                Label = $"{g.Key:MM/yyyy}",
                Count = g.Count()
            })
            .ToList();

        // =========================================================
        // STAFF ACTIVITY
        // =========================================================
        var staffIds = periodBookings.Select(x => x.CreatedBy)
            .Concat(successfulBills.Select(x => x.CreatedBy))
            .Distinct()
            .ToList();

        var staffUsers = new List<(Guid Id, string UserCode, string UserName)>();

        if (staffIds.Count > 0)
        {
            var staffRows = await _context.Users
                .AsNoTracking()
                .Where(x => staffIds.Contains(x.Id))
                .Select(x => new { x.Id, x.UserCode, x.UserName })
                .ToListAsync(cancellationToken);

            staffUsers = staffRows
                .Select(x => (x.Id, x.UserCode, x.UserName))
                .ToList();
        }

        var bookingCreatedBy = periodBookings
            .GroupBy(x => x.CreatedBy)
            .ToDictionary(g => g.Key, g => g.Count());
        var billCreatedBy = successfulBills
            .GroupBy(x => x.CreatedBy)
            .ToDictionary(g => g.Key, g => g.Count());

        var staffActivities = staffUsers
            .Select(x => new StatisticsStaffActivityItem
            {
                UserCode = x.UserCode,
                UserName = x.UserName,
                BookingsCreated = bookingCreatedBy.GetValueOrDefault(x.Id),
                BillsCreated = billCreatedBy.GetValueOrDefault(x.Id)
            })
            .OrderByDescending(x => x.BookingsCreated)
            .ThenByDescending(x => x.BillsCreated)
            .ThenBy(x => x.UserCode)
            .Take(15)
            .ToList();

        return new StatisticsViewModel
        {
            StartDate = start,
            EndDate = end,
            GeneratedAt = now,
            TotalBookings = totalBookings,
            PreBookingCount = preBookingCount,
            DirectBookingCount = directBookingCount,
            SuccessfulBookingCount = successfulBookingCount,
            CancelledOrFailedBookingCount = cancelledOrFailedBookingCount,
            CheckInFailureCount = checkInFailureCount,
            NoShowCount = noShowCount,
            CheckedOutCount = checkedOutCount,
            ActualCheckInCount = actualCheckInCount,
            ActualCheckOutCount = actualCheckOutCount,
            TotalAdults = totalAdults,
            TotalChildren = totalChildren,
            AverageLeadTimeDays = leadTimes.Count == 0 ? 0 : leadTimes.Average(),
            AverageStayNights = stayLengths.Count == 0 ? 0 : stayLengths.Average(),
            RoomRevenue = roomRevenue,
            ServiceRevenue = serviceRevenue,
            AdditionalFeeRevenue = additionalFeeRevenue,
            BilledRevenue = billedRevenue,
            DepositCollected = depositCollected,
            RemainingCollected = remainingCollected,
            AverageBillAmount = averageBillAmount,
            SuccessfulBillCount = successfulBills.Count,
            FailedBillCount = failedBillCount,
            DepositCash = depositCash,
            DepositTransfer = depositTransfer,
            RemainingCash = remainingCash,
            RemainingTransfer = remainingTransfer,
            TotalRooms = totalRooms,
            ActiveRooms = activeRooms,
            InactiveRooms = inactiveRooms,
            OccupiedRooms = occupiedRooms,
            AvailableRooms = availableRooms,
            WaitingCleanRooms = waitingCleanRooms,
            CleaningRooms = cleaningRooms,
            MaintenanceRooms = maintenanceRooms,
            CurrentOccupancyRate = currentOccupancyRate,
            PeriodOccupancyRate = periodOccupancyRate,
            OccupiedRoomNights = occupiedRoomNights,
            AvailableRoomNights = availableRoomNights,
            ActiveRoomTypeCount = activeRoomTypeCount,
            InactiveRoomTypeCount = inactiveRoomTypeCount,
            ActiveServiceCount = activeServiceCount,
            InactiveServiceCount = inactiveServiceCount,
            ActiveAmenityCount = activeAmenityCount,
            InactiveAmenityCount = inactiveAmenityCount,
            WorkingEmployeeCount = workingEmployeeCount,
            FormerEmployeeCount = formerEmployeeCount,
            ActiveUserCount = activeUserCount,
            InactiveUserCount = inactiveUserCount,
            ActiveGuestCount = activeGuestCount,
            NewGuestCount = newGuestCount,
            RoomTypes = roomTypes,
            BookingStatuses = bookingStatuses,
            BookingTypes = bookingTypes,
            GuestSexes = guestSexes,
            Services = services,
            FeeTypes = feeTypeStats,
            RoomRevenueByType = roomRevenueByType,
            RevenueTrend = revenueTrend,
            BookingTrend = bookingTrend,
            TopGuests = topGuests,
            StaffActivities = staffActivities
        };
    }
}
