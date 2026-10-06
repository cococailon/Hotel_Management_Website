using Hotel_Management_Website.Models.ViewModels.Admin;

namespace Hotel_Management_Website.IServices;

public interface IAdminDashboardService
{
    Task<AdminDashboardViewModel> GetSummaryAsync(
        CancellationToken cancellationToken = default);
}
