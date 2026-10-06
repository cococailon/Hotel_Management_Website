using Hotel_Management_Website.Filters;
using Hotel_Management_Website.IServices;
using Microsoft.AspNetCore.Mvc;

namespace Hotel_Management_Website.Controllers;

[AdminAccess]
public sealed class AdminController : Controller
{
    private readonly IAdminDashboardService _dashboardService;
    private readonly ILogger<AdminController> _logger;

    public AdminController(
        IAdminDashboardService dashboardService,
        ILogger<AdminController> logger)
    {
        _dashboardService = dashboardService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        CancellationToken cancellationToken)
    {
        try
        {
            var model = await _dashboardService.GetSummaryAsync(
                cancellationToken);

            return View(model);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Không thể tải Admin Dashboard.");

            return View(new Models.ViewModels.Admin.AdminDashboardViewModel
            {
                GeneratedAt = DateTime.Now,
                LoadError =
                    "Không thể tải dữ liệu thống kê dashboard. Vui lòng thử lại."
            });
        }
    }
}
