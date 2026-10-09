using System.Text.RegularExpressions;
using Hotel_Management_Website.IServices;
using Hotel_Management_Website.Models;
using Hotel_Management_Website.Models.ViewModels.Amenity;
using Hotel_Management_Website.Models.ViewModels.UI;
using Microsoft.EntityFrameworkCore;

namespace Hotel_Management_Website.Services;

public sealed class AmenityService : IAmenityService
{
    private static readonly Regex AutoAmenityCodeRegex =
        new(@"^TN(?<number>\d+)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly AppDbContext _context;

    public AmenityService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<AmenityIndexViewModel> GetIndexAsync(
        AmenityListFilterViewModel filter,
        CancellationToken cancellationToken = default)
    {
        filter ??= new AmenityListFilterViewModel();

        var search = filter.Search?.Trim() ?? string.Empty;
        var sort = NormalizeSort(filter.Sort);
        var pageSize = NormalizePageSize(filter.PageSize);
        var requestedPage = Math.Max(filter.CurrentPage, 1);

        var query = _context.Amenities
            .AsNoTracking()
            .Where(x => !x.IsDeleted);

        if (filter.Status.HasValue)
        {
            query = query.Where(x => x.IsActive == filter.Status.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(x =>
                x.AmenityCode.Contains(search) ||
                x.AmenityName.Contains(search));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
        var currentPage = Math.Min(requestedPage, totalPages);

        var page = await ApplySort(query, sort)
            .Skip((currentPage - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new AmenityListItemViewModel
            {
                Id = x.Id,
                AmenityCode = x.AmenityCode,
                AmenityName = x.AmenityName,
                IsActive = x.IsActive,
                LastModifiedAt = x.LastModifiedAt,
                HasActiveBookings = _context.RoomTypeAmenities.Any(rta =>
                    !rta.IsDeleted &&
                    rta.AmenityId == x.Id &&
                    !rta.RoomType.IsDeleted &&
                    rta.RoomType.BookingRooms.Any(br =>
                        !br.IsDeleted &&
                        !br.Booking.IsDeleted &&
                        (br.Booking.Status == 0 ||
                         br.Booking.Status == 1 ||
                         br.Booking.Status == 2)))
            })
            .ToListAsync(cancellationToken);

        return new AmenityIndexViewModel
        {
            Filter = new AmenityListFilterViewModel
            {
                Search = search,
                Sort = sort,
                Status = filter.Status,
                CurrentPage = currentPage,
                PageSize = pageSize
            },
            Amenities = page,
            TotalCount = totalCount,
            Pagination = new PaginationViewModel
            {
                CurrentPage = currentPage,
                PageSize = pageSize,
                TotalItems = totalCount,
                PageParam = "Filter.CurrentPage",
                PageSizeParam = "Filter.PageSize",
                BaseUrl = "/Amenity/Index",
                ItemLabel = "tiện nghi"
            }
        };
    }

    public async Task<string> GetNextAmenityCodeAsync(
        CancellationToken cancellationToken = default)
    {
        var codes = await _context.Amenities
            .AsNoTracking()
            .Where(x => x.AmenityCode.StartsWith("TN"))
            .Select(x => x.AmenityCode)
            .ToListAsync(cancellationToken);

        var maxNumber = 0;

        foreach (var raw in codes)
        {
            var code = raw?.Trim() ?? string.Empty;
            var match = AutoAmenityCodeRegex.Match(code);
            if (!match.Success ||
                !int.TryParse(match.Groups["number"].Value, out var number))
            {
                continue;
            }

            maxNumber = Math.Max(maxNumber, number);
        }

        var next = maxNumber + 1;
        return $"TN{next}";
    }

    public async Task<AmenityDetailsViewModel?> GetDetailsAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var amenity = await _context.Amenities
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken);

        if (amenity is null)
            return null;

        return new AmenityDetailsViewModel
        {
            Id = amenity.Id,
            AmenityCode = amenity.AmenityCode,
            AmenityName = amenity.AmenityName,
            Description = amenity.Description,
            IsActive = amenity.IsActive,
            Note = amenity.Note,
            CreatedAt = amenity.CreatedAt,
            LastModifiedAt = amenity.LastModifiedAt,
            HasActiveBookings = await HasActiveBookingsAsync(id, cancellationToken)
        };
    }

    public async Task<AmenityCreateViewModel> PrepareCreateAsync(
        CancellationToken cancellationToken = default)
    {
        return new AmenityCreateViewModel
        {
            AmenityCode = await GetNextAmenityCodeAsync(cancellationToken),
            IsActive = true
        };
    }

    public async Task<(bool Succeeded, string? ErrorMessage, IReadOnlyDictionary<string, string> FieldErrors)> CreateAsync(
        AmenityCreateViewModel model,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        var errors = new Dictionary<string, string>();

        if (model is null)
            return (false, "Dữ liệu tiện nghi không hợp lệ.", errors);

        if (currentUserId == Guid.Empty)
            return (false, "Không xác định được người dùng đang đăng nhập.", errors);

        var code = (model.AmenityCode ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(code))
        {
            errors[nameof(model.AmenityCode)] = "Vui lòng nhập mã tiện nghi.";
        }
        else if (!Regex.IsMatch(code, "^[A-Za-z0-9]+$"))
        {
            errors[nameof(model.AmenityCode)] = "Mã tiện nghi chỉ được chứa chữ hoa, chữ thường và số, không chứa dấu cách hoặc ký tự đặc biệt.";
        }

        if (code.Length > 50)
            errors[nameof(model.AmenityCode)] = "Mã tiện nghi không được vượt quá 50 ký tự.";

        if (await _context.Amenities.AnyAsync(x => x.AmenityCode == code, cancellationToken))
            errors[nameof(model.AmenityCode)] = "Mã tiện nghi đã tồn tại.";

        if (string.IsNullOrWhiteSpace(model.AmenityName))
            errors[nameof(model.AmenityName)] = "Vui lòng nhập tên tiện nghi.";

        if (model.AmenityName?.Length > 100)
            errors[nameof(model.AmenityName)] = "Tên tiện nghi không được vượt quá 100 ký tự.";

        if (model.Description?.Length > 255)
            errors[nameof(model.Description)] = "Mô tả không được vượt quá 255 ký tự.";

        if (model.Note?.Length > 255)
            errors[nameof(model.Note)] = "Ghi chú không được vượt quá 255 ký tự.";

        if (errors.Count > 0)
            return (false, "Dữ liệu tiện nghi chưa hợp lệ.", errors);

        var now = DateTime.Now;
        var entity = new Amenity
        {
            Id = Guid.NewGuid(),
            AmenityCode = code,
            AmenityName = model.AmenityName.Trim(),
            Description = NormalizeOptional(model.Description),
            IsActive = model.IsActive,
            Note = NormalizeOptional(model.Note),
            IsDeleted = false,
            CreatedBy = currentUserId,
            CreatedAt = now,
            LastModifiedBy = currentUserId,
            LastModifiedAt = now
        };

        _context.Amenities.Add(entity);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            return (true, null, errors);
        }
        catch (DbUpdateException)
        {
            return (false, "Không thể tạo tiện nghi. Mã tiện nghi có thể đã tồn tại.", new Dictionary<string, string>
            {
                [nameof(model.AmenityCode)] = "Mã tiện nghi đã tồn tại."
            });
        }
    }

    public async Task<AmenityEditViewModel?> GetEditModelAsync(
        Guid id,
        string? returnUrl,
        CancellationToken cancellationToken = default)
    {
        var amenity = await _context.Amenities
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken);

        if (amenity is null)
            return null;

        return new AmenityEditViewModel
        {
            Id = amenity.Id,
            AmenityCode = amenity.AmenityCode,
            AmenityName = amenity.AmenityName,
            Description = amenity.Description,
            IsActive = amenity.IsActive,
            Note = amenity.Note,
            HasActiveBookings = await HasActiveBookingsAsync(id, cancellationToken),
            ReturnUrl = returnUrl
        };
    }

    public async Task<(bool Succeeded, string? ErrorMessage, IReadOnlyDictionary<string, string> FieldErrors)> UpdateAsync(
        AmenityEditViewModel model,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        var errors = new Dictionary<string, string>();

        if (model is null || model.Id == Guid.Empty)
            return (false, "Dữ liệu tiện nghi không hợp lệ.", errors);

        if (currentUserId == Guid.Empty)
            return (false, "Không xác định được người dùng đang đăng nhập.", errors);

        var amenity = await _context.Amenities
            .FirstOrDefaultAsync(x => x.Id == model.Id && !x.IsDeleted, cancellationToken);

        if (amenity is null)
            return (false, "Tiện nghi không tồn tại hoặc đã bị xóa.", errors);

        var hasActiveBookings = await HasActiveBookingsAsync(model.Id, cancellationToken);

        var requestedCode = (model.AmenityCode ?? string.Empty).Trim();
        var requestedName = (model.AmenityName ?? string.Empty).Trim();

        if (hasActiveBookings)
        {
            // Không cho sửa mã và tên khi tiện nghi đang được dùng bởi RoomType
            // có Booking hoạt động. Giữ nguyên dữ liệu hiện tại từ DB.
            requestedCode = amenity.AmenityCode;
            requestedName = amenity.AmenityName;
        }
        else
        {
            if (string.IsNullOrWhiteSpace(requestedCode))
                errors[nameof(model.AmenityCode)] = "Vui lòng nhập mã tiện nghi.";
            else if (!Regex.IsMatch(requestedCode, "^[A-Za-z0-9]+$"))
                errors[nameof(model.AmenityCode)] = "Mã tiện nghi chỉ được chứa chữ hoa, chữ thường và số, không chứa dấu cách hoặc ký tự đặc biệt.";

            if (requestedCode.Length > 50)
                errors[nameof(model.AmenityCode)] = "Mã tiện nghi không được vượt quá 50 ký tự.";

            if (await _context.Amenities.AnyAsync(
                    x => x.Id != model.Id && x.AmenityCode == requestedCode,
                    cancellationToken))
            {
                errors[nameof(model.AmenityCode)] = "Mã tiện nghi đã tồn tại.";
            }

            if (string.IsNullOrWhiteSpace(requestedName))
                errors[nameof(model.AmenityName)] = "Vui lòng nhập tên tiện nghi.";
            else if (requestedName.Length > 100)
                errors[nameof(model.AmenityName)] = "Tên tiện nghi không được vượt quá 100 ký tự.";
        }

        if (model.Description?.Length > 255)
            errors[nameof(model.Description)] = "Mô tả không được vượt quá 255 ký tự.";

        if (model.Note?.Length > 255)
            errors[nameof(model.Note)] = "Ghi chú không được vượt quá 255 ký tự.";

        if (errors.Count > 0)
            return (false, "Dữ liệu tiện nghi chưa hợp lệ.", errors);

        amenity.AmenityCode = requestedCode;
        amenity.AmenityName = requestedName;
        amenity.Description = NormalizeOptional(model.Description);
        amenity.Note = NormalizeOptional(model.Note);
        // IsActive không được sửa ở màn hình Edit.
        amenity.LastModifiedBy = currentUserId;
        amenity.LastModifiedAt = DateTime.Now;

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            return (true, null, errors);
        }
        catch (DbUpdateException)
        {
            return (false, "Không thể lưu thay đổi tiện nghi. Mã tiện nghi có thể đã tồn tại.", new Dictionary<string, string>
            {
                [nameof(model.AmenityCode)] = "Mã tiện nghi đã tồn tại hoặc không hợp lệ."
            });
        }
    }

    public async Task<(bool Succeeded, string? ErrorMessage)> DeactivateAsync(
        Guid id,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
            return (false, "Tiện nghi không hợp lệ.");

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var amenity = await _context.Amenities
                .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken);

            if (amenity is null)
                return (false, "Tiện nghi không tồn tại hoặc đã bị xóa.");

            if (!amenity.IsActive)
                return (false, "Tiện nghi đã dừng hoạt động.");

            if (await HasActiveBookingsAsync(id, cancellationToken))
                return (false, "Không thể dừng hoạt động vì tiện nghi đang đang được sử dụng bởi loại phòng nằm trong lịch đặt phòng có trạng thái là chờ xác nhận, chờ check in hoặc đang ở.");

            var now = DateTime.Now;
            amenity.IsActive = false;
            amenity.LastModifiedBy = currentUserId;
            amenity.LastModifiedAt = now;

            var assignments = await _context.RoomTypeAmenities
                .Where(x => x.AmenityId == id && !x.IsDeleted && x.IsActive)
                .ToListAsync(cancellationToken);

            foreach (var assignment in assignments)
            {
                assignment.IsActive = false;
                assignment.LastModifiedBy = currentUserId;
                assignment.LastModifiedAt = now;
            }

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return (true, null);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            return (false, "Không thể dừng hoạt động tiện nghi. Vui lòng thử lại.");
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            return (false, "Không thể dừng hoạt động tiện nghi. Vui lòng thử lại.");
        }
    }

    public async Task<(bool Succeeded, string? ErrorMessage, int ReopenedAssociationCount)> ActivateAsync(
        Guid id,
        bool reactivateAssociations,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
            return (false, "Tiện nghi không hợp lệ.", 0);

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var amenity = await _context.Amenities
                .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken);

            if (amenity is null)
                return (false, "Tiện nghi không tồn tại hoặc đã bị xóa.", 0);

            if (amenity.IsActive)
                return (false, "Tiện nghi đang hoạt động.", 0);

            if (await HasActiveBookingsAsync(id, cancellationToken))
                return (false, "Không thể mở hoạt động vì tiện nghi đang được sử dụng bởi loại phòng có Booking đang hoạt động.", 0);

            var now = DateTime.Now;
            amenity.IsActive = true;
            amenity.LastModifiedBy = currentUserId;
            amenity.LastModifiedAt = now;

            var reopenedCount = 0;
            if (reactivateAssociations)
            {
                var assignments = await _context.RoomTypeAmenities
                    .Where(x => x.AmenityId == id && !x.IsDeleted && !x.IsActive)
                    .ToListAsync(cancellationToken);

                foreach (var assignment in assignments)
                {
                    assignment.IsActive = true;
                    assignment.LastModifiedBy = currentUserId;
                    assignment.LastModifiedAt = now;
                    reopenedCount++;
                }
            }

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return (true, null, reopenedCount);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            return (false, "Không thể mở hoạt động tiện nghi. Vui lòng thử lại.", 0);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            return (false, "Không thể mở hoạt động tiện nghi. Vui lòng thử lại.", 0);
        }
    }

    public async Task<(bool Succeeded, string? ErrorMessage)> DeleteAsync(
        Guid id,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
            return (false, "Tiện nghi không hợp lệ.");

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var amenity = await _context.Amenities
                .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken);

            if (amenity is null)
                return (false, "Tiện nghi không tồn tại hoặc đã bị xóa.");

            if (amenity.IsActive)
                return (false, "Chỉ được xóa tiện nghi đã dừng hoạt động.");

            if (await HasActiveBookingsAsync(id, cancellationToken))
                return (false, "Không thể xóa vì tiện nghi đang được sử dụng bởi loại phòng có Booking đang hoạt động.");

            var now = DateTime.Now;
            amenity.IsDeleted = true;
            amenity.LastModifiedBy = currentUserId;
            amenity.LastModifiedAt = now;

            var assignments = await _context.RoomTypeAmenities
                .Where(x => x.AmenityId == id && !x.IsDeleted)
                .ToListAsync(cancellationToken);

            foreach (var assignment in assignments)
            {
                assignment.IsDeleted = true;
                assignment.LastModifiedBy = currentUserId;
                assignment.LastModifiedAt = now;
            }

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return (true, null);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            return (false, "Không thể xóa tiện nghi. Vui lòng thử lại.");
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            return (false, "Không thể xóa tiện nghi. Vui lòng thử lại.");
        }
    }

    private static AmenitySortOption NormalizeSort(AmenitySortOption sort)
    {
        return Enum.IsDefined(typeof(AmenitySortOption), sort)
            ? sort
            : AmenitySortOption.LastModifiedDesc;
    }

    private static int NormalizePageSize(int pageSize)
    {
        return pageSize switch
        {
            10 => 10,
            20 => 20,
            50 => 50,
            _ => 10
        };
    }

    private static IQueryable<Amenity> ApplySort(
        IQueryable<Amenity> query,
        AmenitySortOption sort)
    {
        return sort switch
        {
            AmenitySortOption.AmenityCodeAsc =>
                query.OrderBy(x => x.AmenityCode).ThenBy(x => x.AmenityName),

            AmenitySortOption.AmenityCodeDesc =>
                query.OrderByDescending(x => x.AmenityCode).ThenBy(x => x.AmenityName),

            AmenitySortOption.AmenityNameAsc =>
                query.OrderBy(x => x.AmenityName).ThenBy(x => x.AmenityCode),

            AmenitySortOption.AmenityNameDesc =>
                query.OrderByDescending(x => x.AmenityName).ThenBy(x => x.AmenityCode),

            AmenitySortOption.LastModifiedAsc =>
                query.OrderBy(x => x.LastModifiedAt).ThenBy(x => x.AmenityCode),

            _ =>
                query.OrderByDescending(x => x.LastModifiedAt).ThenBy(x => x.AmenityCode)
        };
    }

    private async Task<bool> HasActiveBookingsAsync(
        Guid amenityId,
        CancellationToken cancellationToken)
    {
        return await _context.RoomTypeAmenities.AnyAsync(rta =>
            !rta.IsDeleted &&
            rta.AmenityId == amenityId &&
            !rta.RoomType.IsDeleted &&
            rta.RoomType.BookingRooms.Any(br =>
                !br.IsDeleted &&
                !br.Booking.IsDeleted &&
                (br.Booking.Status == 0 ||
                 br.Booking.Status == 1 ||
                 br.Booking.Status == 2)),
            cancellationToken);
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }
}
