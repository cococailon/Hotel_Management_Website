using System.Text.RegularExpressions;
using Hotel_Management_Website.IServices;
using Hotel_Management_Website.Models;
using Hotel_Management_Website.Models.ViewModels.Service;
using Hotel_Management_Website.Models.ViewModels.UI;
using Microsoft.EntityFrameworkCore;

namespace Hotel_Management_Website.Services;

public sealed class ServiceManagementService : IServiceManagementService
{
    private static readonly Regex AutoServiceCodeRegex =
        new(@"^DV(?<number>\d+)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex PriceUnitRegex =
        new(@"^(/[^/\s]+)+$", RegexOptions.Compiled);

    private readonly AppDbContext _context;

    public ServiceManagementService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<ServiceIndexViewModel> GetIndexAsync(
        ServiceListFilterViewModel filter,
        CancellationToken cancellationToken = default)
    {
        filter ??= new ServiceListFilterViewModel();

        var search = filter.Search?.Trim() ?? string.Empty;
        var sort = NormalizeSort(filter.Sort);
        var pageSize = NormalizePageSize(filter.PageSize);
        var requestedPage = Math.Max(filter.CurrentPage, 1);
        var priceFilter = NormalizePriceFilter(filter.PriceFilter);

        var query = _context.Services
            .AsNoTracking()
            .Where(x => !x.IsDeleted);

        if (filter.Status.HasValue)
        {
            query = query.Where(x => x.IsActive == filter.Status.Value);
        }

        query = priceFilter switch
        {
            ServicePriceFilterOption.Free => query.Where(x => !x.UnitPrice.HasValue || x.UnitPrice.Value <= 0),
            ServicePriceFilterOption.Paid => query.Where(x => x.UnitPrice.HasValue && x.UnitPrice.Value > 0),
            _ => query
        };

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(x =>
                x.ServiceCode.Contains(search) ||
                x.ServiceName.Contains(search));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
        var currentPage = Math.Min(requestedPage, totalPages);

        var page = await ApplySort(query, sort)
            .Skip((currentPage - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new ServiceListItemViewModel
            {
                Id = x.Id,
                ServiceCode = x.ServiceCode,
                ServiceName = x.ServiceName,
                UnitPrice = x.UnitPrice,
                PriceUnitType = x.PriceUnitType,
                IsActive = x.IsActive,
                LastModifiedAt = x.LastModifiedAt,
                HasActiveBookings = _context.RoomTypeServices.Any(rts =>
                    !rts.IsDeleted &&
                    rts.ServiceId == x.Id &&
                    !rts.RoomType.IsDeleted &&
                    rts.RoomType.BookingRooms.Any(br =>
                        !br.IsDeleted &&
                        !br.Booking.IsDeleted &&
                        (br.Booking.Status == 0 ||
                         br.Booking.Status == 1 ||
                         br.Booking.Status == 2)))
            })
            .ToListAsync(cancellationToken);

        return new ServiceIndexViewModel
        {
            Filter = new ServiceListFilterViewModel
            {
                Search = search,
                Sort = sort,
                Status = filter.Status,
                PriceFilter = priceFilter,
                CurrentPage = currentPage,
                PageSize = pageSize
            },
            Services = page,
            TotalCount = totalCount,
            Pagination = new PaginationViewModel
            {
                CurrentPage = currentPage,
                PageSize = pageSize,
                TotalItems = totalCount,
                PageParam = "Filter.CurrentPage",
                PageSizeParam = "Filter.PageSize",
                BaseUrl = "/Services/Index",
                ItemLabel = "dịch vụ"
            }
        };
    }

    public async Task<string> GetNextServiceCodeAsync(CancellationToken cancellationToken = default)
    {
        var codes = await _context.Services
            .AsNoTracking()
            .Where(x => x.ServiceCode.StartsWith("DV"))
            .Select(x => x.ServiceCode)
            .ToListAsync(cancellationToken);

        var maxNumber = 0;
        var width = 2;

        foreach (var raw in codes)
        {
            var code = raw?.Trim() ?? string.Empty;
            var match = AutoServiceCodeRegex.Match(code);
            if (!match.Success || !int.TryParse(match.Groups["number"].Value, out var number))
                continue;

            maxNumber = Math.Max(maxNumber, number);
            width = Math.Max(width, match.Groups["number"].Value.Length);
        }

        var next = maxNumber + 1;
        width = Math.Max(width, next.ToString().Length);
        return $"DV{next.ToString($"D{width}")}";
    }

    public async Task<ServiceDetailsViewModel?> GetDetailsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var service = await _context.Services
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken);

        if (service is null)
            return null;

        return new ServiceDetailsViewModel
        {
            Id = service.Id,
            ServiceCode = service.ServiceCode,
            ServiceName = service.ServiceName,
            UnitPrice = service.UnitPrice,
            PriceUnitType = service.PriceUnitType,
            IsActive = service.IsActive,
            Description = service.Description,
            Note = service.Note,
            CreatedAt = service.CreatedAt,
            LastModifiedAt = service.LastModifiedAt,
            HasActiveBookings = await HasActiveBookingsAsync(id, cancellationToken)
        };
    }

    public async Task<ServiceCreateViewModel> PrepareCreateAsync(CancellationToken cancellationToken = default)
    {
        return new ServiceCreateViewModel
        {
            ServiceCode = await GetNextServiceCodeAsync(cancellationToken),
            IsActive = true
        };
    }

    public async Task<(bool Succeeded, string? ErrorMessage, IReadOnlyDictionary<string, string> FieldErrors)> CreateAsync(
        ServiceCreateViewModel model,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        var errors = new Dictionary<string, string>();

        if (model is null)
            return (false, "Dữ liệu dịch vụ không hợp lệ.", errors);

        if (currentUserId == Guid.Empty)
            return (false, "Không xác định được người dùng đang đăng nhập.", errors);

        var code = (model.ServiceCode ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(code))
        {
            code = await GetNextServiceCodeAsync(cancellationToken);
        }
        else if (code.Any(char.IsWhiteSpace))
        {
            errors[nameof(model.ServiceCode)] = "Mã dịch vụ không được chứa dấu cách hoặc khoảng trắng.";
        }

        if (code.Length > 50)
            errors[nameof(model.ServiceCode)] = "Mã dịch vụ không được vượt quá 50 ký tự.";

        if (await _context.Services.AnyAsync(x => x.ServiceCode == code, cancellationToken))
            errors[nameof(model.ServiceCode)] = $"Mã dịch vụ '{code}' đã tồn tại.";

        ValidateCommonFields(model.ServiceName, model.UnitPrice, model.PriceUnitType, model.Description, model.Note, errors);

        if (errors.Count > 0)
            return (false, "Dữ liệu dịch vụ chưa hợp lệ.", errors);

        var now = DateTime.Now;
        var entity = new Service
        {
            Id = Guid.NewGuid(),
            ServiceCode = code,
            ServiceName = model.ServiceName.Trim(),
            UnitPrice = NormalizePrice(model.UnitPrice),
            PriceUnitType = NormalizeOptional(model.PriceUnitType),
            IsActive = model.IsActive,
            Description = NormalizeOptional(model.Description),
            Note = NormalizeOptional(model.Note),
            IsDeleted = false,
            CreatedBy = currentUserId,
            CreatedAt = now,
            LastModifiedBy = currentUserId,
            LastModifiedAt = now
        };

        _context.Services.Add(entity);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            return (true, null, errors);
        }
        catch (DbUpdateException)
        {
            return (false, "Không thể tạo dịch vụ. Mã dịch vụ có thể đã tồn tại.", new Dictionary<string, string>
            {
                [nameof(model.ServiceCode)] = "Mã dịch vụ đã tồn tại hoặc không hợp lệ."
            });
        }
    }

    public async Task<ServiceEditViewModel?> GetEditModelAsync(Guid id, string? returnUrl, CancellationToken cancellationToken = default)
    {
        var service = await _context.Services
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken);

        if (service is null)
            return null;

        return new ServiceEditViewModel
        {
            Id = service.Id,
            ServiceCode = service.ServiceCode,
            ServiceName = service.ServiceName,
            UnitPrice = service.UnitPrice,
            PriceUnitType = service.PriceUnitType,
            IsActive = service.IsActive,
            Description = service.Description,
            Note = service.Note,
            HasActiveBookings = await HasActiveBookingsAsync(id, cancellationToken),
            ReturnUrl = returnUrl
        };
    }

    public async Task<(bool Succeeded, string? ErrorMessage, IReadOnlyDictionary<string, string> FieldErrors)> UpdateAsync(
        ServiceEditViewModel model,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        var errors = new Dictionary<string, string>();

        if (model is null || model.Id == Guid.Empty)
            return (false, "Dữ liệu dịch vụ không hợp lệ.", errors);

        if (currentUserId == Guid.Empty)
            return (false, "Không xác định được người dùng đang đăng nhập.", errors);

        var service = await _context.Services
            .FirstOrDefaultAsync(x => x.Id == model.Id && !x.IsDeleted, cancellationToken);

        if (service is null)
            return (false, "Dịch vụ không tồn tại hoặc đã bị xóa.", errors);

        var hasActiveBookings = await HasActiveBookingsAsync(model.Id, cancellationToken);
        var requestedCode = (model.ServiceCode ?? string.Empty).Trim();
        var requestedName = (model.ServiceName ?? string.Empty).Trim();

        if (hasActiveBookings)
        {
            requestedCode = service.ServiceCode;
            requestedName = service.ServiceName;
            model.UnitPrice = service.UnitPrice;
            model.PriceUnitType = service.PriceUnitType;
        }
        else
        {
            if (string.IsNullOrWhiteSpace(requestedCode))
                errors[nameof(model.ServiceCode)] = "Vui lòng nhập mã dịch vụ.";
            else if (requestedCode.Any(char.IsWhiteSpace))
                errors[nameof(model.ServiceCode)] = "Mã dịch vụ không được chứa dấu cách hoặc khoảng trắng.";

            if (requestedCode.Length > 50)
                errors[nameof(model.ServiceCode)] = "Mã dịch vụ không được vượt quá 50 ký tự.";

            if (await _context.Services.AnyAsync(x => x.Id != model.Id && x.ServiceCode == requestedCode, cancellationToken))
                errors[nameof(model.ServiceCode)] = $"Mã dịch vụ '{requestedCode}' đã tồn tại.";
        }

        if (string.IsNullOrWhiteSpace(requestedName))
            errors[nameof(model.ServiceName)] = "Vui lòng nhập tên dịch vụ.";
        else if (requestedName.Length > 100)
            errors[nameof(model.ServiceName)] = "Tên dịch vụ không được vượt quá 100 ký tự.";

        if (!hasActiveBookings)
        {
            ValidatePriceAndUnit(model.UnitPrice, model.PriceUnitType, errors);
        }

        if (model.Description?.Length > 255)
            errors[nameof(model.Description)] = "Mô tả không được vượt quá 255 ký tự.";

        if (model.Note?.Length > 255)
            errors[nameof(model.Note)] = "Ghi chú không được vượt quá 255 ký tự.";

        if (errors.Count > 0)
            return (false, "Dữ liệu dịch vụ chưa hợp lệ.", errors);

        service.ServiceCode = requestedCode;
        service.ServiceName = requestedName;
        service.UnitPrice = hasActiveBookings ? service.UnitPrice : NormalizePrice(model.UnitPrice);
        service.PriceUnitType = hasActiveBookings ? service.PriceUnitType : NormalizeOptional(model.PriceUnitType);
        service.Description = NormalizeOptional(model.Description);
        service.Note = NormalizeOptional(model.Note);
        service.LastModifiedBy = currentUserId;
        service.LastModifiedAt = DateTime.Now;

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            return (true, null, errors);
        }
        catch (DbUpdateException)
        {
            return (false, "Không thể lưu thay đổi dịch vụ. Mã dịch vụ có thể đã tồn tại.", new Dictionary<string, string>
            {
                [nameof(model.ServiceCode)] = "Mã dịch vụ đã tồn tại hoặc không hợp lệ."
            });
        }
    }

    public async Task<(bool Succeeded, string? ErrorMessage)> DeactivateAsync(Guid id, Guid currentUserId, CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
            return (false, "Dịch vụ không hợp lệ.");
        if (currentUserId == Guid.Empty)
            return (false, "Không xác định được người dùng đang đăng nhập.");

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var service = await _context.Services.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken);
            if (service is null)
                return (false, "Dịch vụ không tồn tại hoặc đã bị xóa.");
            if (!service.IsActive)
                return (false, "Dịch vụ đã dừng hoạt động.");
            if (await HasActiveBookingsAsync(id, cancellationToken))
                return (false, "Không thể dừng hoạt động vì dịch vụ đang được sử dụng bởi loại phòng có Booking đang hoạt động.");

            var now = DateTime.Now;
            service.IsActive = false;
            service.LastModifiedBy = currentUserId;
            service.LastModifiedAt = now;

            var assignments = await _context.RoomTypeServices
                .Where(x => x.ServiceId == id && !x.IsDeleted && x.IsActive)
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
        catch (OperationCanceledException) { throw; }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            return (false, "Không thể dừng hoạt động dịch vụ. Vui lòng thử lại.");
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            return (false, "Không thể dừng hoạt động dịch vụ. Vui lòng thử lại.");
        }
    }

    public async Task<(bool Succeeded, string? ErrorMessage, int ReopenedAssociationCount)> ActivateAsync(Guid id, bool reactivateAssociations, Guid currentUserId, CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
            return (false, "Dịch vụ không hợp lệ.", 0);
        if (currentUserId == Guid.Empty)
            return (false, "Không xác định được người dùng đang đăng nhập.", 0);

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var service = await _context.Services.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken);
            if (service is null)
                return (false, "Dịch vụ không tồn tại hoặc đã bị xóa.", 0);
            if (service.IsActive)
                return (false, "Dịch vụ đang hoạt động.", 0);
            if (await HasActiveBookingsAsync(id, cancellationToken))
                return (false, "Không thể mở hoạt động vì dịch vụ đang được sử dụng bởi loại phòng có Booking đang hoạt động.", 0);

            var now = DateTime.Now;
            service.IsActive = true;
            service.LastModifiedBy = currentUserId;
            service.LastModifiedAt = now;

            var reopenedCount = 0;
            if (reactivateAssociations)
            {
                var assignments = await _context.RoomTypeServices
                    .Where(x => x.ServiceId == id && !x.IsDeleted && !x.IsActive)
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
        catch (OperationCanceledException) { throw; }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            return (false, "Không thể mở hoạt động dịch vụ. Vui lòng thử lại.", 0);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            return (false, "Không thể mở hoạt động dịch vụ. Vui lòng thử lại.", 0);
        }
    }

    public async Task<(bool Succeeded, string? ErrorMessage)> DeleteAsync(Guid id, Guid currentUserId, CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
            return (false, "Dịch vụ không hợp lệ.");
        if (currentUserId == Guid.Empty)
            return (false, "Không xác định được người dùng đang đăng nhập.");

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var service = await _context.Services.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken);
            if (service is null)
                return (false, "Dịch vụ không tồn tại hoặc đã bị xóa.");
            if (service.IsActive)
                return (false, "Chỉ được xóa dịch vụ đã dừng hoạt động.");
            if (await HasActiveBookingsAsync(id, cancellationToken))
                return (false, "Không thể xóa vì dịch vụ đang được sử dụng bởi loại phòng có Booking đang hoạt động.");

            var now = DateTime.Now;
            service.IsDeleted = true;
            service.LastModifiedBy = currentUserId;
            service.LastModifiedAt = now;

            var assignments = await _context.RoomTypeServices
                .Where(x => x.ServiceId == id && !x.IsDeleted)
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
        catch (OperationCanceledException) { throw; }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            return (false, "Không thể xóa dịch vụ. Vui lòng thử lại.");
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            return (false, "Không thể xóa dịch vụ. Vui lòng thử lại.");
        }
    }

    public Task<bool> HasActiveBookingsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return _context.RoomTypeServices.AnyAsync(rts =>
            !rts.IsDeleted &&
            rts.ServiceId == id &&
            !rts.RoomType.IsDeleted &&
            rts.RoomType.BookingRooms.Any(br =>
                !br.IsDeleted &&
                !br.Booking.IsDeleted &&
                (br.Booking.Status == 0 || br.Booking.Status == 1 || br.Booking.Status == 2)),
            cancellationToken);
    }

    private static void ValidateCommonFields(
        string? serviceName,
        decimal? unitPrice,
        string? priceUnitType,
        string? description,
        string? note,
        IDictionary<string, string> errors)
    {
        if (string.IsNullOrWhiteSpace(serviceName))
            errors[nameof(ServiceCreateViewModel.ServiceName)] = "Vui lòng nhập tên dịch vụ.";
        else if (serviceName.Length > 100)
            errors[nameof(ServiceCreateViewModel.ServiceName)] = "Tên dịch vụ không được vượt quá 100 ký tự.";

        ValidatePriceAndUnit(unitPrice, priceUnitType, errors);

        if (description?.Length > 255)
            errors[nameof(ServiceCreateViewModel.Description)] = "Mô tả không được vượt quá 255 ký tự.";
        if (note?.Length > 255)
            errors[nameof(ServiceCreateViewModel.Note)] = "Ghi chú không được vượt quá 255 ký tự.";
    }

    private static void ValidatePriceAndUnit(decimal? unitPrice, string? priceUnitType, IDictionary<string, string> errors)
    {
        if (unitPrice.HasValue && unitPrice.Value < 0)
            errors[nameof(ServiceCreateViewModel.UnitPrice)] = "Đơn giá phải lớn hơn hoặc bằng 0.";

        var unit = priceUnitType?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(unit))
        {
            if (unitPrice.HasValue && unitPrice.Value > 0)
                errors[nameof(ServiceCreateViewModel.PriceUnitType)] = "Dịch vụ có thu phí phải có đơn vị theo dạng /x/x/x... .";
            return;
        }

        if (unit.Length > 50)
            errors[nameof(ServiceCreateViewModel.PriceUnitType)] = "Đơn vị không được vượt quá 50 ký tự.";
        else if (!PriceUnitRegex.IsMatch(unit))
            errors[nameof(ServiceCreateViewModel.PriceUnitType)] = "Đơn vị phải có dạng /x/x/x..., ví dụ /người/lần/kg.";
    }

    private static decimal? NormalizePrice(decimal? value) => value is null || value.Value <= 0 ? (value is null ? null : 0m) : value;
    private static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static ServiceSortOption NormalizeSort(ServiceSortOption sort) => Enum.IsDefined(typeof(ServiceSortOption), sort) ? sort : ServiceSortOption.LastModifiedDesc;
    private static ServicePriceFilterOption NormalizePriceFilter(ServicePriceFilterOption filter) => Enum.IsDefined(typeof(ServicePriceFilterOption), filter) ? filter : ServicePriceFilterOption.All;
    private static int NormalizePageSize(int pageSize) => pageSize switch { 10 => 10, 20 => 20, 50 => 50, _ => 10 };

    private static IQueryable<Service> ApplySort(IQueryable<Service> query, ServiceSortOption sort)
    {
        return sort switch
        {
            ServiceSortOption.ServiceCodeAsc => query.OrderBy(x => x.ServiceCode).ThenBy(x => x.ServiceName),
            ServiceSortOption.ServiceCodeDesc => query.OrderByDescending(x => x.ServiceCode).ThenBy(x => x.ServiceName),
            ServiceSortOption.ServiceNameAsc => query.OrderBy(x => x.ServiceName).ThenBy(x => x.ServiceCode),
            ServiceSortOption.ServiceNameDesc => query.OrderByDescending(x => x.ServiceName).ThenBy(x => x.ServiceCode),
            ServiceSortOption.UnitPriceAsc => query.OrderBy(x => x.UnitPrice ?? 0m).ThenBy(x => x.ServiceCode),
            ServiceSortOption.UnitPriceDesc => query.OrderByDescending(x => x.UnitPrice ?? 0m).ThenBy(x => x.ServiceCode),
            ServiceSortOption.LastModifiedAsc => query.OrderBy(x => x.LastModifiedAt).ThenBy(x => x.ServiceCode),
            _ => query.OrderByDescending(x => x.LastModifiedAt).ThenBy(x => x.ServiceCode)
        };
    }
}
