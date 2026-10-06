using Hotel_Management_Website.Models;
using Hotel_Management_Website.IServices;
using Hotel_Management_Website.Models.ViewModels.Admin;
using Microsoft.EntityFrameworkCore;

namespace Hotel_Management_Website.Services;

public sealed class AdminDashboardService : IAdminDashboardService
{
    private readonly AppDbContext _context;

    public AdminDashboardService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<AdminDashboardViewModel> GetSummaryAsync(
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.Now;
        var todayStart = now.Date;
        var tomorrowStart = todayStart.AddDays(1);
        var monthStart = new DateTime(now.Year, now.Month, 1);
        var nextMonthStart = monthStart.AddMonths(1);

        // =========================================================
        // REVENUE
        //
        // Bills does not have a dedicated payment timestamp in the
        // current DB design, so CreatedAt is used as the billing/
        // revenue timestamp. Only successful, non-deleted bills count.
        // =========================================================

        var successfulBills = _context.Bills
            .AsNoTracking()
            .Where(x => !x.IsDeleted && x.Status == 0);

        var revenueToday = await successfulBills
            .Where(x =>
                x.CreatedAt >= todayStart &&
                x.CreatedAt < tomorrowStart)
            .SumAsync(x => (decimal?)x.FinalAmount, cancellationToken)
            ?? 0m;

        var revenueThisMonth = await successfulBills
            .Where(x =>
                x.CreatedAt >= monthStart &&
                x.CreatedAt < nextMonthStart)
            .SumAsync(x => (decimal?)x.FinalAmount, cancellationToken)
            ?? 0m;

        // =========================================================
        // ROOMS
        //
        // Only non-deleted + active rooms are counted in the
        // operational dashboard.
        // 0 = Trống
        // 1 = Đang sử dụng
        // 2 = Chờ dọn dẹp
        // =========================================================

        var activeRooms = _context.Rooms
            .AsNoTracking()
            .Where(x => !x.IsDeleted && x.IsActive);

        var occupiedRooms = await activeRooms
            .CountAsync(x => x.Status == 1, cancellationToken);

        var availableRooms = await activeRooms
            .CountAsync(x => x.Status == 0, cancellationToken);

        var roomsNeedCleaning = await activeRooms
            .CountAsync(x => x.Status == 2, cancellationToken);

        // =========================================================
        // BOOKINGS
        //
        // "Tổng lượt đặt" = all non-deleted bookings in the system.
        // =========================================================

        var totalBookings = await _context.Bookings
            .AsNoTracking()
            .CountAsync(x => !x.IsDeleted, cancellationToken);

        return new AdminDashboardViewModel
        {
            RevenueToday = revenueToday,
            RevenueThisMonth = revenueThisMonth,
            OccupiedRooms = occupiedRooms,
            AvailableRooms = availableRooms,
            RoomsNeedCleaning = roomsNeedCleaning,
            TotalBookings = totalBookings,
            GeneratedAt = now
        };
    }
}
