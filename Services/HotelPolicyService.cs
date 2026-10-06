using Hotel_Management_Website.IServices;
using Hotel_Management_Website.Models;
using Hotel_Management_Website.Models.ViewModels.HotelPolicy;
using Microsoft.EntityFrameworkCore;

namespace Hotel_Management_Website.Services;

public sealed class HotelPolicyService : IHotelPolicyService
{
    private readonly AppDbContext _context;

    public HotelPolicyService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<HotelPolicyViewModel> GetAsync(
        CancellationToken cancellationToken = default)
    {
        var setting = await _context.HotelSettings
            .AsNoTracking()
            .OrderBy(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);

        var policy = await _context.Policies
            .AsNoTracking()
            .OrderByDescending(x => x.LastModifiedAt)
            .ThenBy(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);

        return new HotelPolicyViewModel
        {
            HotelSettingId = setting?.Id ?? Guid.Empty,
            PolicyId = policy?.Id ?? Guid.Empty,
            CheckInTime = setting?.CheckInTime ?? new TimeOnly(14, 0),
            CheckOutTime = setting?.CheckOutTime ?? new TimeOnly(12, 0),
            HotelSettingsNote = setting?.Note,
            PolicyContent = policy?.Content,
            PolicyNote = policy?.Note,
            PolicyLastModifiedAt = policy?.LastModifiedAt
        };
    }

    public async Task<(bool Succeeded, string? ErrorMessage)> UpdateAsync(
        HotelPolicyViewModel model,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        if (currentUserId == Guid.Empty)
            return (false, "Không xác định được tài khoản đang thực hiện thao tác.");

        var elapsedHours = ((model.CheckOutTime - model.CheckInTime).TotalHours + 24d) % 24d;
        if (Math.Abs(elapsedHours - 22d) > 0.0001d)
        {
            return (false, "Giờ check in và giờ check out phải cách nhau đúng 22 tiếng.");
        }

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var setting = await _context.HotelSettings
                .OrderBy(x => x.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (setting is null)
            {
                setting = new HotelSetting
                {
                    Id = Guid.NewGuid()
                };
                _context.HotelSettings.Add(setting);
            }

            setting.CheckInTime = model.CheckInTime;
            setting.CheckOutTime = model.CheckOutTime;
            setting.Note = NormalizeNullable(model.HotelSettingsNote, 255);

            var policy = await _context.Policies
                .OrderByDescending(x => x.LastModifiedAt)
                .ThenBy(x => x.Id)
                .FirstOrDefaultAsync(cancellationToken);

            var now = DateTime.Now;

            if (policy is null)
            {
                policy = new Policy
                {
                    Id = Guid.NewGuid(),
                    CreatedBy = currentUserId,
                    CreatedAt = now
                };
                _context.Policies.Add(policy);
            }

            policy.Content = string.IsNullOrWhiteSpace(model.PolicyContent)
                ? null
                : model.PolicyContent.Trim();
            policy.Note = NormalizeNullable(model.PolicyNote, 255);
            policy.LastModifiedBy = currentUserId;
            policy.LastModifiedAt = now;

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return (true, null);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            await transaction.RollbackAsync(cancellationToken);
            return (false, "Không thể lưu quy định khách sạn. Vui lòng thử lại.");
        }
    }

    private static string? NormalizeNullable(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return value.Trim();
    }
}
