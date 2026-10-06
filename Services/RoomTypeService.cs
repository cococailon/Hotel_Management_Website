using System.Data;
using System.Text.RegularExpressions;
using Hotel_Management_Website.IServices;
using Hotel_Management_Website.Models;
using Hotel_Management_Website.Models.ViewModels.RoomType;
using Hotel_Management_Website.Models.ViewModels.UI;
using Microsoft.EntityFrameworkCore;

namespace Hotel_Management_Website.Services;

public sealed class RoomTypeService : IRoomTypeService
{
    private static readonly Regex AutoRoomTypeCodeRegex =
        new(@"^LP(?<number>\d+)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly AppDbContext _context;

    public RoomTypeService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<RoomTypeIndexViewModel> GetIndexAsync(
        RoomTypeListFilterViewModel filter,
        CancellationToken cancellationToken = default)
    {
        filter ??= new RoomTypeListFilterViewModel();

        var search = filter.Search?.Trim() ?? string.Empty;
        var sort = NormalizeSort(filter.Sort);
        var pageSize = NormalizePageSize(filter.PageSize);
        var requestedPage = Math.Max(filter.CurrentPage, 1);
        var isActive = NormalizeStatus(filter.IsActive);

        var query = _context.RoomTypes
            .AsNoTracking()
            .Where(x => !x.IsDeleted);

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(x =>
                x.RoomTypeCode.Contains(search) ||
                x.RoomTypeName.Contains(search));
        }

        if (isActive.HasValue)
        {
            query = query.Where(x => x.IsActive == isActive.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
        var currentPage = Math.Min(requestedPage, totalPages);

        var page = await ApplySort(query, sort)
            .Skip((currentPage - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new RoomTypeListItemViewModel
            {
                Id = x.Id,
                RoomTypeCode = x.RoomTypeCode,
                RoomTypeName = x.RoomTypeName,
                MaxAdults = x.MaxAdults,
                MaxChildren = x.MaxChildren,
                PricePerNight = x.PricePerNight,
                DepositPercent = x.DepositPercent,
                Area = x.Area,
                IsActive = x.IsActive,
                LastModifiedAt = x.LastModifiedAt,
                HasActiveBookings = _context.BookingRooms.Any(br =>
                    !br.IsDeleted &&
                    br.RoomTypeId == x.Id &&
                    !br.Booking.IsDeleted &&
                    (br.Booking.Status == 0 || br.Booking.Status == 1 || br.Booking.Status == 2)),
                HasUndeletedRooms = _context.Rooms.Any(room =>
                    !room.IsDeleted &&
                    room.RoomTypeId == x.Id)
            })
            .ToListAsync(cancellationToken);

        return new RoomTypeIndexViewModel
        {
            Filter = new RoomTypeListFilterViewModel
            {
                Search = search,
                Sort = sort,
                IsActive = isActive,
                CurrentPage = currentPage,
                PageSize = pageSize
            },
            RoomTypes = page,
            TotalCount = totalCount,
            Pagination = new PaginationViewModel
            {
                CurrentPage = currentPage,
                PageSize = pageSize,
                TotalItems = totalCount,
                PageParam = "Filter.CurrentPage",
                PageSizeParam = "Filter.PageSize",
                BaseUrl = "/RoomType/Index",
                ItemLabel = "loại phòng"
            }
        };
    }

    public async Task<RoomTypeCreateViewModel> PrepareCreateAsync(
        CancellationToken cancellationToken = default)
    {
        var model = new RoomTypeCreateViewModel
        {
            RoomTypeCode = await GetNextRoomTypeCodeAsync(cancellationToken),
            IsActive = true
        };

        await PopulateCreateLookupsAsync(model, cancellationToken);
        return model;
    }

    public async Task PopulateCreateLookupsAsync(
        RoomTypeCreateViewModel model,
        CancellationToken cancellationToken = default)
    {
        model ??= new RoomTypeCreateViewModel();

        model.AvailableAmenities = await _context.Amenities
            .AsNoTracking()
            .Where(x => !x.IsDeleted && x.IsActive)
            .OrderBy(x => x.AmenityCode)
            .Select(x => new RoomTypeMasterOptionViewModel
            {
                Id = x.Id,
                Code = x.AmenityCode,
                Name = x.AmenityName
            })
            .ToListAsync(cancellationToken);

        model.AvailableServices = await _context.Services
            .AsNoTracking()
            .Where(x => !x.IsDeleted && x.IsActive)
            .OrderBy(x => x.ServiceCode)
            .Select(x => new RoomTypeMasterOptionViewModel
            {
                Id = x.Id,
                Code = x.ServiceCode,
                Name = x.ServiceName,
                UnitPrice = x.UnitPrice,
                PriceUnitType = x.PriceUnitType
            })
            .ToListAsync(cancellationToken);
    }

    public async Task PopulateEditLookupsAsync(
        RoomTypeEditViewModel model,
        bool loadCurrentRelationsWhenEmpty = true,
        CancellationToken cancellationToken = default)
    {
        model ??= new RoomTypeEditViewModel();

        var currentAmenityIds = model.Amenities
            .Where(x => x.ItemId.HasValue && x.ItemId.Value != Guid.Empty)
            .Select(x => x.ItemId!.Value)
            .ToHashSet();

        var currentServiceIds = model.Services
            .Where(x => x.ItemId.HasValue && x.ItemId.Value != Guid.Empty)
            .Select(x => x.ItemId!.Value)
            .ToHashSet();

        model.AvailableAmenities = await _context.Amenities
            .AsNoTracking()
            .Where(x => !x.IsDeleted && x.IsActive && !currentAmenityIds.Contains(x.Id))
            .OrderBy(x => x.AmenityCode)
            .Select(x => new RoomTypeMasterOptionViewModel
            {
                Id = x.Id,
                Code = x.AmenityCode,
                Name = x.AmenityName
            })
            .ToListAsync(cancellationToken);

        model.AvailableServices = await _context.Services
            .AsNoTracking()
            .Where(x => !x.IsDeleted && x.IsActive && !currentServiceIds.Contains(x.Id))
            .OrderBy(x => x.ServiceCode)
            .Select(x => new RoomTypeMasterOptionViewModel
            {
                Id = x.Id,
                Code = x.ServiceCode,
                Name = x.ServiceName,
                UnitPrice = x.UnitPrice,
                PriceUnitType = x.PriceUnitType
            })
            .ToListAsync(cancellationToken);

        if (model.Id == Guid.Empty)
            return;

        // Khi GET: lấy toàn bộ quan hệ chưa bị xóa từ DB.
        // Khi POST lỗi: giữ đúng các dòng đã gửi để người dùng sửa tiếp.
        if (loadCurrentRelationsWhenEmpty && model.Amenities.Count == 0 && model.Services.Count == 0)
        {
            model.CurrentAmenities = await _context.RoomTypeAmenities
                .AsNoTracking()
                .Where(x => x.RoomTypeId == model.Id && !x.IsDeleted)
                .OrderBy(x => x.Amenity.AmenityCode)
                .Select(x => new RoomTypeAssociationDisplayViewModel
                {
                    AssignmentId = x.Id,
                    ItemId = x.AmenityId,
                    Code = x.Amenity.AmenityCode,
                    Name = x.Amenity.AmenityName,
                    IsActive = x.IsActive,
                    MasterIsActive = x.Amenity.IsActive,
                    MasterIsDeleted = x.Amenity.IsDeleted
                })
                .ToListAsync(cancellationToken);

            model.CurrentServices = await _context.RoomTypeServices
                .AsNoTracking()
                .Where(x => x.RoomTypeId == model.Id && !x.IsDeleted)
                .OrderBy(x => x.Service.ServiceCode)
                .Select(x => new RoomTypeAssociationDisplayViewModel
                {
                    AssignmentId = x.Id,
                    ItemId = x.ServiceId,
                    Code = x.Service.ServiceCode,
                    Name = x.Service.ServiceName,
                    UnitPrice = x.Service.UnitPrice,
                    PriceUnitType = x.Service.PriceUnitType,
                    IsActive = x.IsActive,
                    MasterIsActive = x.Service.IsActive,
                    MasterIsDeleted = x.Service.IsDeleted
                })
                .ToListAsync(cancellationToken);
        }
        else
        {
            model.CurrentAmenities = await RehydrateAmenitiesAsync(model, cancellationToken);
            model.CurrentServices = await RehydrateServicesAsync(model, cancellationToken);
        }
    }

    public async Task<RoomTypeEditViewModel?> GetEditModelAsync(
        Guid id,
        string? returnUrl,
        CancellationToken cancellationToken = default)
    {
        var roomType = await _context.RoomTypes
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken);

        if (roomType is null)
            return null;

        var model = new RoomTypeEditViewModel
        {
            Id = roomType.Id,
            RoomTypeCode = roomType.RoomTypeCode,
            RoomTypeName = roomType.RoomTypeName,
            MaxAdults = roomType.MaxAdults,
            MaxChildren = roomType.MaxChildren,
            PricePerNight = roomType.PricePerNight,
            DepositPercent = roomType.DepositPercent,
            Area = roomType.Area,
            Description = roomType.Description,
            IsActive = roomType.IsActive,
            Note = roomType.Note,
            HasActiveBookings = await HasActiveBookingsAsync(id, cancellationToken),
            ReturnUrl = returnUrl
        };

        model.CurrentAmenities = await _context.RoomTypeAmenities
            .AsNoTracking()
            .Where(x => x.RoomTypeId == id && !x.IsDeleted)
            .OrderBy(x => x.Amenity.AmenityCode)
            .Select(x => new RoomTypeAssociationDisplayViewModel
            {
                AssignmentId = x.Id,
                ItemId = x.AmenityId,
                Code = x.Amenity.AmenityCode,
                Name = x.Amenity.AmenityName,
                IsActive = x.IsActive,
                MasterIsActive = x.Amenity.IsActive,
                MasterIsDeleted = x.Amenity.IsDeleted
            })
            .ToListAsync(cancellationToken);

        model.CurrentServices = await _context.RoomTypeServices
            .AsNoTracking()
            .Where(x => x.RoomTypeId == id && !x.IsDeleted)
            .OrderBy(x => x.Service.ServiceCode)
            .Select(x => new RoomTypeAssociationDisplayViewModel
            {
                AssignmentId = x.Id,
                ItemId = x.ServiceId,
                Code = x.Service.ServiceCode,
                Name = x.Service.ServiceName,
                UnitPrice = x.Service.UnitPrice,
                PriceUnitType = x.Service.PriceUnitType,
                IsActive = x.IsActive,
                MasterIsActive = x.Service.IsActive,
                MasterIsDeleted = x.Service.IsDeleted
            })
            .ToListAsync(cancellationToken);

        model.Amenities = model.CurrentAmenities
            .Select(x => new RoomTypeAssociationInputViewModel
            {
                AssignmentId = x.AssignmentId,
                ItemId = x.ItemId,
                IsActive = x.IsActive
            })
            .ToList();

        model.Services = model.CurrentServices
            .Select(x => new RoomTypeAssociationInputViewModel
            {
                AssignmentId = x.AssignmentId,
                ItemId = x.ItemId,
                IsActive = x.IsActive
            })
            .ToList();

        await PopulateEditLookupsAsync(model, true, cancellationToken);
        return model;
    }

    public async Task<RoomTypeDetailsViewModel?> GetDetailsAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
            return null;

        return await _context.RoomTypes
            .AsNoTracking()
            .Where(x => x.Id == id && !x.IsDeleted)
            .Select(x => new RoomTypeDetailsViewModel
            {
                Id = x.Id,
                RoomTypeCode = x.RoomTypeCode,
                RoomTypeName = x.RoomTypeName,
                MaxAdults = x.MaxAdults,
                MaxChildren = x.MaxChildren,
                PricePerNight = x.PricePerNight,
                DepositPercent = x.DepositPercent,
                Area = x.Area,
                Description = x.Description,
                IsActive = x.IsActive,
                Note = x.Note,
                CreatedAt = x.CreatedAt,
                LastModifiedAt = x.LastModifiedAt
            })
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<string> GetNextRoomTypeCodeAsync(
        CancellationToken cancellationToken = default)
    {
        var codes = await _context.RoomTypes
            .AsNoTracking()
            .Where(x => x.RoomTypeCode.StartsWith("LP"))
            .Select(x => x.RoomTypeCode)
            .ToListAsync(cancellationToken);

        long max = 0;
        foreach (var code in codes)
        {
            var match = AutoRoomTypeCodeRegex.Match(code.Trim());
            if (!match.Success)
                continue;

            if (long.TryParse(match.Groups["number"].Value, out var number) && number > max)
                max = number;
        }

        return $"LP{max + 1}";
    }

    public async Task<(bool Succeeded, string? ErrorMessage, IReadOnlyDictionary<string, string> FieldErrors)> CreateAsync(
        RoomTypeCreateViewModel model,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        var errors = new Dictionary<string, string>();
        model.RoomTypeCode = NormalizeCode(model.RoomTypeCode);
        model.RoomTypeName = model.RoomTypeName?.Trim() ?? string.Empty;
        model.Description = NormalizeOptional(model.Description);
        model.Note = NormalizeOptional(model.Note);

        if (string.IsNullOrWhiteSpace(model.RoomTypeCode))
            errors[nameof(model.RoomTypeCode)] = "Mã loại phòng không được để trống.";
        else if (model.RoomTypeCode.Any(char.IsWhiteSpace))
            errors[nameof(model.RoomTypeCode)] = "Mã loại phòng không được chứa dấu cách hoặc khoảng trắng.";

        if (model.RoomTypeCode.Length > 50)
            errors[nameof(model.RoomTypeCode)] = "Mã loại phòng không được vượt quá 50 ký tự.";

        if (string.IsNullOrWhiteSpace(model.RoomTypeName))
            errors[nameof(model.RoomTypeName)] = "Vui lòng nhập tên loại phòng.";

        if (await _context.RoomTypes.AnyAsync(x => x.RoomTypeCode == model.RoomTypeCode, cancellationToken))
            errors[nameof(model.RoomTypeCode)] = "Mã loại phòng đã tồn tại.";

        foreach (var item in model.Amenities)
        {
            if (!item.ItemId.HasValue || item.ItemId.Value == Guid.Empty)
                continue;

            var valid = await _context.Amenities.AnyAsync(
                x => x.Id == item.ItemId.Value && !x.IsDeleted && x.IsActive,
                cancellationToken);

            if (!valid)
            {
                errors[nameof(model.Amenities)] = "Tiện nghi được chọn không tồn tại, đã xóa hoặc đã ngừng hoạt động.";
                break;
            }
        }

        foreach (var item in model.Services)
        {
            if (!item.ItemId.HasValue || item.ItemId.Value == Guid.Empty)
                continue;

            var valid = await _context.Services.AnyAsync(
                x => x.Id == item.ItemId.Value && !x.IsDeleted && x.IsActive,
                cancellationToken);

            if (!valid)
            {
                errors[nameof(model.Services)] = "Dịch vụ được chọn không tồn tại, đã xóa hoặc đã ngừng hoạt động.";
                break;
            }
        }

        if (errors.Count > 0)
            return (false, "Vui lòng kiểm tra lại các thông tin chưa hợp lệ.", errors);

        await using var transaction = await _context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        if (await _context.RoomTypes.AnyAsync(x => x.RoomTypeCode == model.RoomTypeCode, cancellationToken))
        {
            errors[nameof(model.RoomTypeCode)] = "Mã loại phòng đã tồn tại.";
            return (false, "Mã loại phòng đã tồn tại.", errors);
        }

        var now = DateTime.Now;
        var roomType = new RoomType
        {
            Id = Guid.NewGuid(),
            RoomTypeCode = model.RoomTypeCode,
            RoomTypeName = model.RoomTypeName,
            MaxAdults = model.MaxAdults,
            MaxChildren = model.MaxChildren,
            PricePerNight = model.PricePerNight,
            DepositPercent = model.DepositPercent,
            Area = model.Area,
            Description = model.Description,
            IsActive = model.IsActive,
            Note = model.Note,
            IsDeleted = false,
            CreatedBy = currentUserId,
            CreatedAt = now,
            LastModifiedBy = currentUserId,
            LastModifiedAt = now
        };

        _context.RoomTypes.Add(roomType);

        foreach (var input in UniqueAssociations(model.Amenities))
        {
            _context.RoomTypeAmenities.Add(new RoomTypeAmenity
            {
                Id = Guid.NewGuid(),
                RoomTypeId = roomType.Id,
                AmenityId = input.ItemId!.Value,
                IsActive = input.IsActive,
                IsDeleted = false,
                CreatedBy = currentUserId,
                CreatedAt = now,
                LastModifiedBy = currentUserId,
                LastModifiedAt = now
            });
        }

        foreach (var input in UniqueAssociations(model.Services))
        {
            _context.RoomTypeServices.Add(new Models.RoomTypeService
            {
                Id = Guid.NewGuid(),
                RoomTypeId = roomType.Id,
                ServiceId = input.ItemId!.Value,
                IsActive = input.IsActive,
                IsDeleted = false,
                CreatedBy = currentUserId,
                CreatedAt = now,
                LastModifiedBy = currentUserId,
                LastModifiedAt = now
            });
        }

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(cancellationToken);
            errors[nameof(model.RoomTypeCode)] = "Mã loại phòng đã tồn tại hoặc không thể lưu vào cơ sở dữ liệu.";
            return (false, "Không thể tạo loại phòng.", errors);
        }

        return (true, null, errors);
    }

    public async Task<(bool Succeeded, string? ErrorMessage, IReadOnlyDictionary<string, string> FieldErrors)> UpdateAsync(
        RoomTypeEditViewModel model,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        var errors = new Dictionary<string, string>();
        var roomType = await _context.RoomTypes
            .FirstOrDefaultAsync(x => x.Id == model.Id && !x.IsDeleted, cancellationToken);

        if (roomType is null)
            return (false, "Loại phòng không tồn tại hoặc đã bị xóa.", errors);

        var hasActiveBookings = await HasActiveBookingsAsync(model.Id, cancellationToken);
        var requestedAmenities = UniqueAssociations(model.Amenities);
        var requestedServices = UniqueAssociations(model.Services);

        if (hasActiveBookings)
        {
            // Khi còn Booking hoạt động, mã phải giữ nguyên giá trị hiện tại.
            model.RoomTypeCode = roomType.RoomTypeCode;

            if (roomType.MaxAdults != model.MaxAdults)
                errors[nameof(model.MaxAdults)] = "Không thể thay đổi tổng người lớn vì loại phòng đang có Booking hoạt động.";
            if (roomType.MaxChildren != model.MaxChildren)
                errors[nameof(model.MaxChildren)] = "Không thể thay đổi tổng trẻ em vì loại phòng đang có Booking hoạt động.";
            if (roomType.PricePerNight != model.PricePerNight)
                errors[nameof(model.PricePerNight)] = "Không thể thay đổi giá một đêm vì loại phòng đang có Booking hoạt động.";
            if (roomType.DepositPercent != model.DepositPercent)
                errors[nameof(model.DepositPercent)] = "Không thể thay đổi cọc vì loại phòng đang có Booking hoạt động.";
            if (roomType.IsActive != model.IsActive)
                errors[nameof(model.IsActive)] = "Không thể thay đổi trạng thái vì loại phòng đang có Booking hoạt động.";

            var dbAmenities = await _context.RoomTypeAmenities
                .AsNoTracking()
                .Where(x => x.RoomTypeId == model.Id && !x.IsDeleted)
                .Select(x => new { x.AmenityId, x.IsActive })
                .ToListAsync(cancellationToken);
            var dbServices = await _context.RoomTypeServices
                .AsNoTracking()
                .Where(x => x.RoomTypeId == model.Id && !x.IsDeleted)
                .Select(x => new { x.ServiceId, x.IsActive })
                .ToListAsync(cancellationToken);

            if (dbAmenities.Count != requestedAmenities.Count ||
                dbAmenities.Any(db => !requestedAmenities.Any(req => req.ItemId == db.AmenityId && req.IsActive == db.IsActive)))
            {
                errors[nameof(model.Amenities)] = "Không thể thêm, xóa hoặc thay đổi trạng thái tiện nghi khi loại phòng đang có Booking hoạt động.";
            }

            if (dbServices.Count != requestedServices.Count ||
                dbServices.Any(db => !requestedServices.Any(req => req.ItemId == db.ServiceId && req.IsActive == db.IsActive)))
            {
                errors[nameof(model.Services)] = "Không thể thêm, xóa hoặc thay đổi trạng thái dịch vụ khi loại phòng đang có Booking hoạt động.";
            }

            if (errors.Count > 0)
                return (false, "Loại phòng đang có Booking hoạt động nên một số thuộc tính không được phép thay đổi.", errors);

            // Booking đang hoạt động: chỉ ghi 4 thuộc tính được phép sửa.
            // Không gọi SyncAmenities/SyncServices và không ghi lại trạng thái/giá/sức chứa.
            roomType.RoomTypeName = model.RoomTypeName?.Trim() ?? string.Empty;
            roomType.Area = model.Area;
            roomType.Description = NormalizeOptional(model.Description);
            roomType.Note = NormalizeOptional(model.Note);
            roomType.LastModifiedBy = currentUserId;
            roomType.LastModifiedAt = DateTime.Now;

            try
            {
                await _context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                return (false, "Không thể lưu thay đổi loại phòng.", new Dictionary<string, string>
                {
                    [string.Empty] = "Không thể lưu thay đổi loại phòng. Vui lòng thử lại."
                });
            }

            return (true, null, errors);
        }

        if (string.IsNullOrWhiteSpace(model.RoomTypeCode))
            errors[nameof(model.RoomTypeCode)] = "Mã loại phòng không được để trống.";
        else if (model.RoomTypeCode.Any(char.IsWhiteSpace))
            errors[nameof(model.RoomTypeCode)] = "Mã loại phòng không được chứa dấu cách hoặc khoảng trắng.";
        else if (model.RoomTypeCode.Length > 50)
            errors[nameof(model.RoomTypeCode)] = "Mã loại phòng không được vượt quá 50 ký tự.";
        else if (await _context.RoomTypes.AnyAsync(x => x.RoomTypeCode == model.RoomTypeCode && x.Id != roomType.Id, cancellationToken))
            errors[nameof(model.RoomTypeCode)] = "Mã loại phòng đã tồn tại.";

        if (requestedAmenities.GroupBy(x => x.ItemId!.Value).Any(g => g.Count() > 1))
            errors[nameof(model.Amenities)] = "Không được thêm trùng tiện nghi.";

        if (requestedServices.GroupBy(x => x.ItemId!.Value).Any(g => g.Count() > 1))
            errors[nameof(model.Services)] = "Không được thêm trùng dịch vụ.";

        if (errors.Count > 0)
            return (false, "Vui lòng kiểm tra lại thông tin.", errors);

        roomType.RoomTypeCode = model.RoomTypeCode;
        roomType.RoomTypeName = model.RoomTypeName?.Trim() ?? string.Empty;
        roomType.MaxAdults = model.MaxAdults;
        roomType.MaxChildren = model.MaxChildren;
        roomType.PricePerNight = model.PricePerNight;
        roomType.DepositPercent = model.DepositPercent;
        roomType.Area = model.Area;
        roomType.Description = NormalizeOptional(model.Description);
        roomType.IsActive = model.IsActive;
        roomType.Note = NormalizeOptional(model.Note);
        roomType.LastModifiedBy = currentUserId;
        roomType.LastModifiedAt = DateTime.Now;

        var associationError = await SyncAmenitiesAsync(roomType.Id, requestedAmenities, currentUserId, false, cancellationToken);
        if (associationError is not null)
        {
            errors[nameof(model.Amenities)] = associationError;
            return (false, associationError, errors);
        }

        associationError = await SyncServicesAsync(roomType.Id, requestedServices, currentUserId, false, cancellationToken);
        if (associationError is not null)
        {
            errors[nameof(model.Services)] = associationError;
            return (false, associationError, errors);
        }

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return (false, "Không thể lưu thay đổi loại phòng.", new Dictionary<string, string>
            {
                [string.Empty] = "Không thể lưu thay đổi loại phòng. Vui lòng thử lại."
            });
        }

        return (true, null, errors);
    }

    public async Task<(bool Succeeded, string? ErrorMessage)> DeleteAsync(
        Guid id,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        var roomType = await _context.RoomTypes
            .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken);

        if (roomType is null)
            return (false, "Loại phòng không tồn tại hoặc đã bị xóa.");

        if (roomType.IsActive)
            return (false, "Chỉ được xóa loại phòng đã dừng hoạt động.");

        if (await HasActiveBookingsAsync(id, cancellationToken))
            return (false, "Không thể xóa vì loại phòng đang có Booking chờ xác nhận, chờ check in hoặc đang ở.");

        if (await _context.Rooms.AnyAsync(x => !x.IsDeleted && x.RoomTypeId == id, cancellationToken))
            return (false, "Không thể xóa vì vẫn còn phòng chưa xóa đang gán cho loại phòng này.");

        var now = DateTime.Now;
        roomType.IsDeleted = true;
        roomType.LastModifiedBy = currentUserId;
        roomType.LastModifiedAt = now;

        var amenities = await _context.RoomTypeAmenities
            .Where(x => x.RoomTypeId == id && !x.IsDeleted)
            .ToListAsync(cancellationToken);

        var services = await _context.RoomTypeServices
            .Where(x => x.RoomTypeId == id && !x.IsDeleted)
            .ToListAsync(cancellationToken);

        foreach (var item in amenities)
        {
            item.IsDeleted = true;
            item.LastModifiedBy = currentUserId;
            item.LastModifiedAt = now;
        }

        foreach (var item in services)
        {
            item.IsDeleted = true;
            item.LastModifiedBy = currentUserId;
            item.LastModifiedAt = now;
        }

        await _context.SaveChangesAsync(cancellationToken);
        return (true, null);
    }

    public async Task<(bool Succeeded, string? ErrorMessage)> DeactivateAsync(
        Guid id,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        var roomType = await _context.RoomTypes
            .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken);

        if (roomType is null)
            return (false, "Loại phòng không tồn tại hoặc đã bị xóa.");

        if (!roomType.IsActive)
            return (false, "Loại phòng đã dừng hoạt động.");

        if (await HasActiveBookingsAsync(id, cancellationToken))
            return (false, "Không thể dừng hoạt động vì loại phòng đang có Booking chờ xác nhận, chờ check in hoặc đang ở.");

        var now = DateTime.Now;
        roomType.IsActive = false;
        roomType.LastModifiedBy = currentUserId;
        roomType.LastModifiedAt = now;

        var rooms = await _context.Rooms
            .Where(x => !x.IsDeleted && x.RoomTypeId == id && x.IsActive)
            .ToListAsync(cancellationToken);

        foreach (var room in rooms)
        {
            room.IsActive = false;
            room.LastModifiedBy = currentUserId;
            room.LastModifiedAt = now;
        }

        await _context.SaveChangesAsync(cancellationToken);
        return (true, null);
    }

    public async Task<(bool Succeeded, string? ErrorMessage, int ReopenedRoomCount)> ActivateAsync(
        Guid id,
        bool reopenRooms,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        var roomType = await _context.RoomTypes
            .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken);

        if (roomType is null)
            return (false, "Loại phòng không tồn tại hoặc đã bị xóa.", 0);

        if (roomType.IsActive)
            return (false, "Loại phòng đang hoạt động.", 0);

        var now = DateTime.Now;
        roomType.IsActive = true;
        roomType.LastModifiedBy = currentUserId;
        roomType.LastModifiedAt = now;

        var reopened = 0;
        if (reopenRooms)
        {
            // Diễn giải lựa chọn “mở luôn hoạt động của phòng” là mở các phòng
            // đang IsActive = false. Nếu IsActive = true thì phòng đã hoạt động rồi.
            var rooms = await _context.Rooms
                .Where(x => !x.IsDeleted && x.RoomTypeId == id && !x.IsActive)
                .ToListAsync(cancellationToken);

            foreach (var room in rooms)
            {
                room.IsActive = true;
                room.LastModifiedBy = currentUserId;
                room.LastModifiedAt = now;
                reopened++;
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
        return (true, null, reopened);
    }

    private async Task<bool> HasActiveBookingsAsync(Guid roomTypeId, CancellationToken cancellationToken)
    {
        return await _context.BookingRooms.AnyAsync(br =>
            !br.IsDeleted &&
            br.RoomTypeId == roomTypeId &&
            !br.Booking.IsDeleted &&
            (br.Booking.Status == 0 || br.Booking.Status == 1 || br.Booking.Status == 2),
            cancellationToken);
    }

    private async Task<string?> SyncAmenitiesAsync(
        Guid roomTypeId,
        IReadOnlyList<RoomTypeAssociationInputViewModel> requested,
        Guid currentUserId,
        bool restrictExistingChanges,
        CancellationToken cancellationToken)
    {
        var rows = await _context.RoomTypeAmenities
            .Where(x => x.RoomTypeId == roomTypeId)
            .ToListAsync(cancellationToken);

        var requestedIds = requested
            .Where(x => x.ItemId.HasValue && x.ItemId.Value != Guid.Empty)
            .Select(x => x.ItemId!.Value)
            .ToHashSet();

        if (restrictExistingChanges)
        {
            foreach (var row in rows.Where(x => !x.IsDeleted))
            {
                if (!requestedIds.Contains(row.AmenityId))
                    return "Không thể xóa tiện nghi của loại phòng khi đang có Booking hoạt động.";

                var requestedRow = requested.FirstOrDefault(x => x.ItemId == row.AmenityId);
                if (requestedRow is not null && requestedRow.IsActive != row.IsActive)
                    return "Không thể thay đổi trạng thái tiện nghi của loại phòng khi đang có Booking hoạt động.";
            }
        }

        var now = DateTime.Now;

        foreach (var row in rows)
        {
            if (!restrictExistingChanges && !requestedIds.Contains(row.AmenityId) && !row.IsDeleted)
            {
                row.IsDeleted = true;
                row.LastModifiedBy = currentUserId;
                row.LastModifiedAt = now;
            }
        }

        foreach (var input in requested.Where(x => x.ItemId.HasValue && x.ItemId.Value != Guid.Empty))
        {
            var master = await _context.Amenities
                .FirstOrDefaultAsync(x => x.Id == input.ItemId!.Value, cancellationToken);

            if (master is null)
                return "Tiện nghi được chọn không tồn tại.";

            var row = rows.FirstOrDefault(x => x.AmenityId == master.Id);
            var needsMasterCheck = row is null || row.IsDeleted || (!row.IsActive && input.IsActive);

            if (needsMasterCheck && (master.IsDeleted || !master.IsActive))
                return $"Tiện nghi {master.AmenityCode} - {master.AmenityName} đã bị xóa hoặc đã ngừng hoạt động nên không thể gán/mở hoạt động.";

            if (row is null)
            {
                _context.RoomTypeAmenities.Add(new RoomTypeAmenity
                {
                    Id = Guid.NewGuid(),
                    RoomTypeId = roomTypeId,
                    AmenityId = master.Id,
                    IsActive = input.IsActive,
                    IsDeleted = false,
                    CreatedBy = currentUserId,
                    CreatedAt = now,
                    LastModifiedBy = currentUserId,
                    LastModifiedAt = now
                });
            }
            else
            {
                row.IsDeleted = false;
                row.IsActive = input.IsActive;
                row.LastModifiedBy = currentUserId;
                row.LastModifiedAt = now;
            }
        }

        return null;
    }

    private async Task<string?> SyncServicesAsync(
        Guid roomTypeId,
        IReadOnlyList<RoomTypeAssociationInputViewModel> requested,
        Guid currentUserId,
        bool restrictExistingChanges,
        CancellationToken cancellationToken)
    {
        var rows = await _context.RoomTypeServices
            .Where(x => x.RoomTypeId == roomTypeId)
            .ToListAsync(cancellationToken);

        var requestedIds = requested
            .Where(x => x.ItemId.HasValue && x.ItemId.Value != Guid.Empty)
            .Select(x => x.ItemId!.Value)
            .ToHashSet();

        if (restrictExistingChanges)
        {
            foreach (var row in rows.Where(x => !x.IsDeleted))
            {
                if (!requestedIds.Contains(row.ServiceId))
                    return "Không thể xóa dịch vụ của loại phòng khi đang có Booking hoạt động.";

                var requestedRow = requested.FirstOrDefault(x => x.ItemId == row.ServiceId);
                if (requestedRow is not null && requestedRow.IsActive != row.IsActive)
                    return "Không thể thay đổi trạng thái dịch vụ của loại phòng khi đang có Booking hoạt động.";
            }
        }

        var now = DateTime.Now;

        foreach (var row in rows)
        {
            if (!restrictExistingChanges && !requestedIds.Contains(row.ServiceId) && !row.IsDeleted)
            {
                row.IsDeleted = true;
                row.LastModifiedBy = currentUserId;
                row.LastModifiedAt = now;
            }
        }

        foreach (var input in requested.Where(x => x.ItemId.HasValue && x.ItemId.Value != Guid.Empty))
        {
            var master = await _context.Services
                .FirstOrDefaultAsync(x => x.Id == input.ItemId!.Value, cancellationToken);

            if (master is null)
                return "Dịch vụ được chọn không tồn tại.";

            var row = rows.FirstOrDefault(x => x.ServiceId == master.Id);
            var needsMasterCheck = row is null || row.IsDeleted || (!row.IsActive && input.IsActive);

            if (needsMasterCheck && (master.IsDeleted || !master.IsActive))
                return $"Dịch vụ {master.ServiceCode} - {master.ServiceName} đã bị xóa hoặc đã ngừng hoạt động nên không thể gán/mở hoạt động.";

            if (row is null)
            {
                _context.RoomTypeServices.Add(new Models.RoomTypeService
                {
                    Id = Guid.NewGuid(),
                    RoomTypeId = roomTypeId,
                    ServiceId = master.Id,
                    IsActive = input.IsActive,
                    IsDeleted = false,
                    CreatedBy = currentUserId,
                    CreatedAt = now,
                    LastModifiedBy = currentUserId,
                    LastModifiedAt = now
                });
            }
            else
            {
                row.IsDeleted = false;
                row.IsActive = input.IsActive;
                row.LastModifiedBy = currentUserId;
                row.LastModifiedAt = now;
            }
        }

        return null;
    }

    private async Task<IReadOnlyList<RoomTypeAssociationDisplayViewModel>> RehydrateAmenitiesAsync(
        RoomTypeEditViewModel model,
        CancellationToken cancellationToken)
    {
        var ids = model.Amenities
            .Where(x => x.ItemId.HasValue && x.ItemId.Value != Guid.Empty)
            .Select(x => x.ItemId!.Value)
            .Distinct()
            .ToArray();

        var data = await _context.Amenities
            .AsNoTracking()
            .Where(x => ids.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        return model.Amenities
            .Where(x => x.ItemId.HasValue && x.ItemId.Value != Guid.Empty && data.ContainsKey(x.ItemId!.Value))
            .Select(x =>
            {
                var item = data[x.ItemId!.Value];
                return new RoomTypeAssociationDisplayViewModel
                {
                    AssignmentId = x.AssignmentId,
                    ItemId = item.Id,
                    Code = item.AmenityCode,
                    Name = item.AmenityName,
                    IsActive = x.IsActive,
                    MasterIsActive = item.IsActive,
                    MasterIsDeleted = item.IsDeleted
                };
            })
            .ToList();
    }

    private async Task<IReadOnlyList<RoomTypeAssociationDisplayViewModel>> RehydrateServicesAsync(
        RoomTypeEditViewModel model,
        CancellationToken cancellationToken)
    {
        var ids = model.Services
            .Where(x => x.ItemId.HasValue && x.ItemId.Value != Guid.Empty)
            .Select(x => x.ItemId!.Value)
            .Distinct()
            .ToArray();

        var data = await _context.Services
            .AsNoTracking()
            .Where(x => ids.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        return model.Services
            .Where(x => x.ItemId.HasValue && x.ItemId.Value != Guid.Empty && data.ContainsKey(x.ItemId!.Value))
            .Select(x =>
            {
                var item = data[x.ItemId!.Value];
                return new RoomTypeAssociationDisplayViewModel
                {
                    AssignmentId = x.AssignmentId,
                    ItemId = item.Id,
                    Code = item.ServiceCode,
                    Name = item.ServiceName,
                    UnitPrice = item.UnitPrice,
                    PriceUnitType = item.PriceUnitType,
                    IsActive = x.IsActive,
                    MasterIsActive = item.IsActive,
                    MasterIsDeleted = item.IsDeleted
                };
            })
            .ToList();
    }

    private static IReadOnlyList<RoomTypeAssociationInputViewModel> UniqueAssociations(
        IEnumerable<RoomTypeAssociationInputViewModel>? source)
    {
        var result = new Dictionary<Guid, RoomTypeAssociationInputViewModel>();
        foreach (var item in source ?? Enumerable.Empty<RoomTypeAssociationInputViewModel>())
        {
            if (!item.ItemId.HasValue || item.ItemId.Value == Guid.Empty)
                continue;

            result[item.ItemId.Value] = item;
        }

        return result.Values.ToList();
    }

    private static string NormalizeCode(string? code) => (code ?? string.Empty).Trim().ToUpperInvariant();

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static int NormalizePageSize(int pageSize) => pageSize is 10 or 20 or 50 ? pageSize : 10;

    private static bool? NormalizeStatus(bool? value) => value;

    private static RoomTypeSortOption NormalizeSort(RoomTypeSortOption sort) =>
        Enum.IsDefined(typeof(RoomTypeSortOption), sort) ? sort : RoomTypeSortOption.LastModifiedDesc;

    private static IOrderedQueryable<RoomType> ApplySort(
        IQueryable<RoomType> query,
        RoomTypeSortOption sort)
    {
        return sort switch
        {
            RoomTypeSortOption.LastModifiedAsc => query.OrderBy(x => x.LastModifiedAt).ThenBy(x => x.RoomTypeCode),
            RoomTypeSortOption.RoomTypeCodeDesc => query.OrderByDescending(x => x.RoomTypeCode.Length).ThenByDescending(x => x.RoomTypeCode),
            RoomTypeSortOption.RoomTypeCodeAsc => query.OrderBy(x => x.RoomTypeCode.Length).ThenBy(x => x.RoomTypeCode),
            RoomTypeSortOption.RoomTypeNameAsc => query.OrderBy(x => x.RoomTypeName).ThenBy(x => x.RoomTypeCode),
            RoomTypeSortOption.RoomTypeNameDesc => query.OrderByDescending(x => x.RoomTypeName).ThenBy(x => x.RoomTypeCode),
            RoomTypeSortOption.PriceAsc => query.OrderBy(x => x.PricePerNight).ThenBy(x => x.RoomTypeCode),
            RoomTypeSortOption.PriceDesc => query.OrderByDescending(x => x.PricePerNight).ThenBy(x => x.RoomTypeCode),
            RoomTypeSortOption.DepositAsc => query.OrderBy(x => x.DepositPercent).ThenBy(x => x.RoomTypeCode),
            RoomTypeSortOption.DepositDesc => query.OrderByDescending(x => x.DepositPercent).ThenBy(x => x.RoomTypeCode),
            RoomTypeSortOption.AreaAsc => query.OrderBy(x => x.Area).ThenBy(x => x.RoomTypeCode),
            RoomTypeSortOption.AreaDesc => query.OrderByDescending(x => x.Area).ThenBy(x => x.RoomTypeCode),
            _ => query.OrderByDescending(x => x.LastModifiedAt).ThenBy(x => x.RoomTypeCode)
        };
    }
}
