using Hotel_Management_Website.Models.ViewModels.Bill;

namespace Hotel_Management_Website.IServices;

public interface IBillService
{
    Task<BillIndexViewModel> GetIndexAsync(
        BillListFilterViewModel filter,
        CancellationToken cancellationToken = default);

    Task<BillDetailsViewModel?> GetDetailsAsync(
        Guid id,
        CancellationToken cancellationToken = default);
}
