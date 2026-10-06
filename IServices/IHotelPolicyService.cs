using Hotel_Management_Website.Models.ViewModels.HotelPolicy;

namespace Hotel_Management_Website.IServices;

public interface IHotelPolicyService
{
    Task<HotelPolicyViewModel> GetAsync(CancellationToken cancellationToken = default);

    Task<(bool Succeeded, string? ErrorMessage)> UpdateAsync(
        HotelPolicyViewModel model,
        Guid currentUserId,
        CancellationToken cancellationToken = default);
}
