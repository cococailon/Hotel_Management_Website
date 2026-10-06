using Hotel_Management_Website.Filters;
using Hotel_Management_Website.IServices;
using Hotel_Management_Website.Models.ViewModels.Statistics;
using Microsoft.AspNetCore.Mvc;

namespace Hotel_Management_Website.Controllers;

[AdminAccess]
public sealed class StatisticsController : Controller
{
    private readonly IStatisticsService _statisticsService;
    private readonly ILogger<StatisticsController> _logger;

    public StatisticsController(
        IStatisticsService statisticsService,
        ILogger<StatisticsController> logger)
    {
        _statisticsService = statisticsService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        DateTime? startDate,
        DateTime? endDate,
        CancellationToken cancellationToken)
    {
        var today = DateTime.Now.Date;
        var defaultStart = new DateTime(today.Year, today.Month, 1);

        var start = startDate?.Date ?? defaultStart;
        var end = endDate?.Date ?? today;

        if (end < start)
        {
            ModelState.AddModelError(string.Empty,
                "Ngày kết thúc phải lớn hơn hoặc bằng ngày bắt đầu.");
            (start, end) = (end, start);
        }

        try
        {
            var model = await _statisticsService.GetAsync(
                start,
                end,
                cancellationToken);

            if (!ModelState.IsValid)
            {
                model = new StatisticsViewModel
                {
                    StartDate = start,
                    EndDate = end,
                    GeneratedAt = model.GeneratedAt,
                    LoadError = string.Join(" ",
                        ModelState.Values
                            .SelectMany(x => x.Errors)
                            .Select(x => x.ErrorMessage)
                            .Where(x => !string.IsNullOrWhiteSpace(x)))
                };
            }

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
                "Không thể tải trang thống kê khách sạn.");

            return View(new StatisticsViewModel
            {
                StartDate = start,
                EndDate = end,
                GeneratedAt = DateTime.Now,
                LoadError =
                    "Không thể tải dữ liệu thống kê. Vui lòng thử lại."
            });
        }
    }
}
