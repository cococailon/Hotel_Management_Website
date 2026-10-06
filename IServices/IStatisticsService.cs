using Hotel_Management_Website.Models.ViewModels.Statistics;

namespace Hotel_Management_Website.IServices;

public interface IStatisticsService
{
    Task<StatisticsViewModel> GetAsync(
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default);
}
