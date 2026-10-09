using System.Data;
using System.Text.RegularExpressions;
using Hotel_Management_Website.IServices;
using Hotel_Management_Website.Models;
using Hotel_Management_Website.Models.ViewModels.Room;
using Microsoft.EntityFrameworkCore;

namespace Hotel_Management_Website.Services;

public sealed class RoomService : IRoomService
{
    private static readonly Regex AutoRoomCodeRegex = new(
        @"^P(?<number>\d+)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private readonly AppDbContext _context;

    public RoomService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<RoomIndexViewModel> GetIndexAsync(
        RoomListFilterViewModel filter,
        CancellationToken cancellationToken = default)
    {
        filter ??= new RoomListFilterViewModel();

        var normalizedSort = NormalizeSort(filter.Sort);
        var normalizedStatus = NormalizeStatus(filter.Status);
        var pageSize = NormalizePageSize(filter.PageSize);
        var requestedPage = Math.Max(filter.CurrentPage, 1);
        var search = filter.Search?.Trim() ?? string.Empty;

        var roomTypes = await _context.RoomTypes
            .AsNoTracking()
            .Where(x => !x.IsDeleted)
            .OrderBy(x => x.RoomTypeCode)
            .Select(x => new RoomTypeOptionViewModel
            {
                Id = x.Id,
                Code = x.RoomTypeCode,
                Name = x.RoomTypeName,
                IsActive = x.IsActive,
                IsSelectable = true
            })
            .ToListAsync(cancellationToken);

        var query = _context.Rooms
            .AsNoTracking()
            .Where(x => !x.IsDeleted);

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(x =>
                x.RoomCode.Contains(search) ||
                x.RoomName.Contains(search));
        }

        if (filter.RoomTypeId.HasValue && filter.RoomTypeId.Value != Guid.Empty)
        {
            query = query.Where(x => x.RoomTypeId == filter.RoomTypeId.Value);
        }

        if (normalizedStatus.HasValue)
        {
            query = query.Where(x => x.Status == normalizedStatus.Value);
        }

        if (filter.IsActive.HasValue)
        {
            query = query.Where(x => x.IsActive == filter.IsActive.Value);
        }

        var rooms = await query
            .Select(x => new RoomListItemViewModel
            {
                Id = x.Id,
                RoomCode = x.RoomCode,
                RoomName = x.RoomName,
                RoomTypeId = x.RoomTypeId,
                RoomTypeCode = x.RoomType.RoomTypeCode,
                RoomTypeName = x.RoomType.RoomTypeName,
                Floor = x.Floor,
                Row = x.Row,
                LeftToRightOrder = x.LeftToRightOrder,
                Status = x.Status,
                IsActive = x.IsActive
            })
            .ToListAsync(cancellationToken);

        var groupedRows = rooms
            .GroupBy(x => new { x.Floor, x.Row })
            .OrderBy(x => x.Key.Floor)
            .ThenBy(x => x.Key.Row)
            .Select(group => new
            {
                group.Key.Floor,
                group.Key.Row,
                Rooms = SortRooms(group, normalizedSort).ToList()
            })
            .ToList();

        var totalPages = groupedRows.Count == 0
            ? 1
            : Math.Max(
                1,
                groupedRows.Max(x => (int)Math.Ceiling(x.Rooms.Count / (double)pageSize)));

        var currentPage = Math.Min(requestedPage, totalPages);
        var skip = (currentPage - 1) * pageSize;

        var rows = groupedRows
            .Select(group => new RoomFloorRowViewModel
            {
                Floor = group.Floor,
                Row = group.Row,
                Rooms = group.Rooms
                    .Skip(skip)
                    .Take(pageSize)
                    .ToList()
            })
            .Where(x => x.Rooms.Count > 0)
            .ToList();

        return new RoomIndexViewModel
        {
            Filter = new RoomListFilterViewModel
            {
                Search = search,
                Sort = normalizedSort,
                RoomTypeId = filter.RoomTypeId,
                Status = normalizedStatus,
                IsActive = filter.IsActive,
                CurrentPage = currentPage,
                PageSize = pageSize
            },
            Rows = rows,
            RoomTypes = roomTypes,
            TotalRooms = rooms.Count,
            TotalPages = totalPages
        };
    }

    public async Task<RoomCreateViewModel> GetCreateModelAsync(
        CancellationToken cancellationToken = default)
    {
        return new RoomCreateViewModel
        {
            RoomCode = await GetNextRoomCodeAsync(cancellationToken),
            IsActive = true,
            AvailableRoomTypes = await GetRoomTypeOptionsAsync(
                includeInactive: true,
                currentRoomTypeId: null,
                cancellationToken)
        };
    }

    public async Task<RoomEditViewModel?> GetEditModelAsync(
        Guid id,
        string? returnUrl = null,
        CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
            return null;

        var room = await _context.Rooms
            .AsNoTracking()
            .Include(x => x.RoomType)
            .FirstOrDefaultAsync(
                x => x.Id == id && !x.IsDeleted,
                cancellationToken);

        if (room is null)
            return null;

        return new RoomEditViewModel
        {
            Id = room.Id,
            RoomCode = room.RoomCode,
            RoomName = room.RoomName,
            Floor = room.Floor,
            Row = room.Row,
            LeftToRightOrder = room.LeftToRightOrder,
            RoomTypeId = room.RoomTypeId,
            Status = room.Status,
            IsActive = room.IsActive,
            Description = room.Description,
            Note = room.Note,
            ReturnUrl = returnUrl,
            AvailableRoomTypes = await GetRoomTypeOptionsAsync(
                includeInactive: true,
                currentRoomTypeId: room.RoomTypeId,
                cancellationToken)
        };
    }

    public async Task<RoomDetailsViewModel?> GetDetailsAsync(
        Guid id,
        string? returnUrl = null,
        CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
            return null;

        return await _context.Rooms
            .AsNoTracking()
            .Where(x => x.Id == id && !x.IsDeleted)
            .Select(x => new RoomDetailsViewModel
            {
                Id = x.Id,
                RoomCode = x.RoomCode,
                RoomName = x.RoomName,
                RoomTypeCode = x.RoomType.RoomTypeCode,
                RoomTypeName = x.RoomType.RoomTypeName,
                Floor = x.Floor,
                Row = x.Row,
                LeftToRightOrder = x.LeftToRightOrder,
                Description = x.Description,
                Status = x.Status,
                IsActive = x.IsActive,
                Note = x.Note,
                CreatedAt = x.CreatedAt,
                LastModifiedAt = x.LastModifiedAt,
                ReturnUrl = returnUrl
            })
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<Hotel_Management_Website.Models.ViewModels.Housekeeping.HousekeepingRoomStatusViewModel> GetHousekeepingRoomStatusAsync(
        RoomListFilterViewModel filter,
        CancellationToken cancellationToken = default)
    {
        filter ??= new RoomListFilterViewModel();

        var normalizedSort = NormalizeSort(filter.Sort);
        var normalizedStatus = NormalizeStatus(filter.Status);
        var search = filter.Search?.Trim() ?? string.Empty;

        var roomTypes = await _context.RoomTypes
            .AsNoTracking()
            .Where(x => !x.IsDeleted)
            .OrderBy(x => x.RoomTypeCode)
            .Select(x => new RoomTypeOptionViewModel
            {
                Id = x.Id,
                Code = x.RoomTypeCode,
                Name = x.RoomTypeName,
                IsActive = x.IsActive,
                IsSelectable = true
            })
            .ToListAsync(cancellationToken);

        var query = _context.Rooms
            .AsNoTracking()
            .Where(x => !x.IsDeleted);

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(x =>
                x.RoomCode.Contains(search) ||
                x.RoomName.Contains(search));
        }

        if (filter.RoomTypeId.HasValue && filter.RoomTypeId.Value != Guid.Empty)
        {
            query = query.Where(x => x.RoomTypeId == filter.RoomTypeId.Value);
        }

        if (normalizedStatus.HasValue)
        {
            query = query.Where(x => x.Status == normalizedStatus.Value);
        }

        if (filter.IsActive.HasValue)
        {
            query = query.Where(x => x.IsActive == filter.IsActive.Value);
        }

        var rooms = await query
            .Select(x => new RoomListItemViewModel
            {
                Id = x.Id,
                RoomCode = x.RoomCode,
                RoomName = x.RoomName,
                RoomTypeId = x.RoomTypeId,
                RoomTypeCode = x.RoomType.RoomTypeCode,
                RoomTypeName = x.RoomType.RoomTypeName,
                Floor = x.Floor,
                Row = x.Row,
                LeftToRightOrder = x.LeftToRightOrder,
                Status = x.Status,
                IsActive = x.IsActive
            })
            .ToListAsync(cancellationToken);

        var rows = rooms
            .GroupBy(x => new { x.Floor, x.Row })
            .OrderBy(x => x.Key.Floor)
            .ThenBy(x => x.Key.Row)
            .Select(group => new RoomFloorRowViewModel
            {
                Floor = group.Key.Floor,
                Row = group.Key.Row,
                Rooms = SortRooms(group, normalizedSort).ToList()
            })
            .ToList();

        return new Hotel_Management_Website.Models.ViewModels.Housekeeping.HousekeepingRoomStatusViewModel
        {
            Filter = new RoomListFilterViewModel
            {
                Search = search,
                Sort = normalizedSort,
                RoomTypeId = filter.RoomTypeId,
                Status = normalizedStatus,
                IsActive = filter.IsActive,
                CurrentPage = 1,
                PageSize = 5
            },
            Rows = rows,
            RoomTypes = roomTypes,
            TotalRooms = rooms.Count
        };
    }

    public async Task<(bool Succeeded, string? ErrorMessage)> StartCleaningAsync(
        Guid id,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
            return (false, "Phòng không hợp lệ.");

        if (currentUserId == Guid.Empty)
            return (false, "Không xác định được người dùng đang đăng nhập.");

        var room = await _context.Rooms
            .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken);

        if (room is null)
            return (false, "Không tìm thấy phòng.");

        if (!room.IsActive)
            return (false, "Không thể dọn dẹp phòng đang dừng hoạt động.");

        if (room.Status != 2)
            return (false, "Chỉ phòng ở trạng thái Chờ dọn dẹp mới được chuyển sang Đang dọn dẹp.");

        room.Status = 3;
        room.LastModifiedBy = currentUserId;
        room.LastModifiedAt = DateTime.Now;

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            return (true, null);
        }
        catch (DbUpdateException)
        {
            return (false, "Không thể cập nhật trạng thái dọn dẹp phòng. Vui lòng thử lại.");
        }
    }

    public async Task<(bool Succeeded, string? ErrorMessage)> CompleteCleaningAsync(
        Guid id,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
            return (false, "Phòng không hợp lệ.");

        if (currentUserId == Guid.Empty)
            return (false, "Không xác định được người dùng đang đăng nhập.");

        var room = await _context.Rooms
            .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken);

        if (room is null)
            return (false, "Không tìm thấy phòng.");

        if (!room.IsActive)
            return (false, "Không thể hoàn tất dọn dẹp phòng đang dừng hoạt động.");

        if (room.Status != 3)
            return (false, "Chỉ phòng ở trạng thái Đang dọn dẹp mới được chuyển về Trống.");

        room.Status = 0;
        room.LastModifiedBy = currentUserId;
        room.LastModifiedAt = DateTime.Now;

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            return (true, null);
        }
        catch (DbUpdateException)
        {
            return (false, "Không thể cập nhật trạng thái phòng. Vui lòng thử lại.");
        }
    }

    public async Task<string> GetNextRoomCodeAsync(
        CancellationToken cancellationToken = default)
    {
        var codes = await _context.Rooms
            .AsNoTracking()
            .Where(x => x.RoomCode.StartsWith("P"))
            .Select(x => x.RoomCode)
            .ToListAsync(cancellationToken);

        long max = 0;

        foreach (var rawCode in codes)
        {
            var code = rawCode.Trim().ToUpperInvariant();
            var match = AutoRoomCodeRegex.Match(code);

            if (!match.Success)
                continue;

            if (long.TryParse(match.Groups["number"].Value, out var number) && number > max)
            {
                max = number;
            }
        }

        return $"P{max + 1}";
    }

    public async Task<RoomPositionSuggestionViewModel> GetPositionSuggestionsAsync(
        int floor,
        int? row,
        CancellationToken cancellationToken = default)
    {
        var normalizedFloor = Math.Clamp(floor, 1, 1000);

        var floorRooms = await _context.Rooms
            .AsNoTracking()
            .Where(x => !x.IsDeleted && x.Floor == normalizedFloor)
            .Select(x => new
            {
                x.Row,
                x.LeftToRightOrder
            })
            .ToListAsync(cancellationToken);

        var groups = floorRooms
            .GroupBy(x => x.Row)
            .OrderBy(x => x.Key)
            .Select(x => new RoomPositionRowSuggestionViewModel
            {
                Row = x.Key,
                RoomCount = x.Count(),
                MaxLeftToRightOrder = x.Max(y => y.LeftToRightOrder)
            })
            .ToList();

        return new RoomPositionSuggestionViewModel
        {
            Floor = normalizedFloor,
            Row = row,
            TotalRows = groups.Count,
            Rows = groups
        };
    }

    public async Task<(bool Succeeded, string? ErrorMessage, IReadOnlyDictionary<string, string> FieldErrors)> CreateAsync(
        RoomCreateViewModel model,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        var errors = new Dictionary<string, string>();

        if (model is null)
            return (false, "Dữ liệu phòng không hợp lệ.", errors);

        if (currentUserId == Guid.Empty)
            return (false, "Không xác định được người dùng đang đăng nhập.", errors);

        NormalizeCreateModel(model);
        ValidateRoomCode(model.RoomCode, errors);
        ValidateRoomName(model.RoomName, errors);
        ValidatePosition(model.Floor, model.Row, model.LeftToRightOrder, errors);

        if (!model.RoomTypeId.HasValue || model.RoomTypeId.Value == Guid.Empty)
        {
            errors[nameof(model.RoomTypeId)] = "Vui lòng chọn loại phòng.";
        }

        if (errors.Count > 0)
            return (false, "Vui lòng kiểm tra lại các thông tin chưa hợp lệ.", errors);

        await using var transaction = await _context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        try
        {
            if (await _context.Rooms.AnyAsync(
                    x => x.RoomCode == model.RoomCode,
                    cancellationToken))
            {
                errors[nameof(model.RoomCode)] = "Mã phòng đã tồn tại.";
            }

            if (await IsPositionUsedAsync(
                    model.Floor,
                    model.Row,
                    model.LeftToRightOrder,
                    excludeRoomId: null,
                    cancellationToken))
            {
                errors[nameof(model.LeftToRightOrder)] =
                    "Vị trí Tầng + Hàng + Thứ tự đã được sử dụng bởi một phòng khác.";
            }

            var roomType = await _context.RoomTypes
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x => x.Id == model.RoomTypeId!.Value && !x.IsDeleted,
                    cancellationToken);

            if (roomType is null)
            {
                errors[nameof(model.RoomTypeId)] = "Loại phòng không tồn tại hoặc đã bị xóa.";
            }
            else if (model.IsActive && !roomType.IsActive)
            {
                errors[nameof(model.RoomTypeId)] =
                    "Phòng đang hoạt động chỉ được gán với loại phòng đang hoạt động.";
            }

            if (errors.Count > 0)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                return (false, "Không thể tạo phòng vì dữ liệu không hợp lệ.", errors);
            }

            var now = DateTime.Now;
            var room = new Room
            {
                Id = Guid.NewGuid(),
                RoomCode = model.RoomCode,
                RoomName = model.RoomName,
                RoomTypeId = model.RoomTypeId!.Value,
                Floor = model.Floor,
                Row = model.Row,
                LeftToRightOrder = model.LeftToRightOrder,
                Description = NormalizeOptional(model.Description),
                Status = 0,
                IsActive = model.IsActive,
                IsDeleted = false,
                Note = NormalizeOptional(model.Note),
                CreatedBy = currentUserId,
                CreatedAt = now,
                LastModifiedBy = currentUserId,
                LastModifiedAt = now
            };

            _context.Rooms.Add(room);

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return (true, null, errors);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            return (
                false,
                "Không thể tạo phòng. Có thể mã phòng hoặc vị trí đã tồn tại.",
                new Dictionary<string, string>
                {
                    [string.Empty] = "Không thể lưu phòng vào cơ sở dữ liệu. Vui lòng thử lại."
                });
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            return (
                false,
                "Không thể tạo phòng. Vui lòng thử lại.",
                new Dictionary<string, string>
                {
                    [string.Empty] = "Không thể tạo phòng. Vui lòng thử lại."
                });
        }
    }

    public async Task<(bool Succeeded, string? ErrorMessage, IReadOnlyDictionary<string, string> FieldErrors)> UpdateAsync(
        RoomEditViewModel model,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        var errors = new Dictionary<string, string>();

        if (model is null || model.Id == Guid.Empty)
            return (false, "Dữ liệu phòng không hợp lệ.", errors);

        if (currentUserId == Guid.Empty)
            return (false, "Không xác định được người dùng đang đăng nhập.", errors);

        NormalizeEditModel(model);
        ValidateRoomName(model.RoomName, errors);
        ValidatePosition(model.Floor, model.Row, model.LeftToRightOrder, errors);

        if (!model.RoomTypeId.HasValue || model.RoomTypeId.Value == Guid.Empty)
        {
            errors[nameof(model.RoomTypeId)] = "Vui lòng chọn loại phòng.";
        }

        if (errors.Count > 0)
            return (false, "Vui lòng kiểm tra lại các thông tin chưa hợp lệ.", errors);

        await using var transaction = await _context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        try
        {
            var room = await _context.Rooms
                .FirstOrDefaultAsync(
                    x => x.Id == model.Id && !x.IsDeleted,
                    cancellationToken);

            if (room is null)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                return (false, "Không tìm thấy phòng cần sửa.", errors);
            }

            // Khi phòng đang sử dụng, mã phòng và mọi thuộc tính khác ngoài Mô tả/Ghi chú bị khóa.
            if (room.Status == 1)
            {
                if (!string.Equals(room.RoomName, model.RoomName, StringComparison.Ordinal) ||
                    room.Floor != model.Floor ||
                    room.Row != model.Row ||
                    room.LeftToRightOrder != model.LeftToRightOrder ||
                    room.RoomTypeId != model.RoomTypeId!.Value)
                {
                    errors[string.Empty] =
                        "Phòng đang sử dụng, chỉ được phép sửa Mô tả và Ghi chú.";
                    await transaction.RollbackAsync(CancellationToken.None);
                    return (false, "Không thể sửa phòng đang sử dụng.", errors);
                }

                room.Description = NormalizeOptional(model.Description);
                room.Note = NormalizeOptional(model.Note);
                room.LastModifiedBy = currentUserId;
                room.LastModifiedAt = DateTime.Now;

                await _context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return (true, null, errors);
            }

            if (string.IsNullOrWhiteSpace(model.RoomCode))
            {
                errors[nameof(model.RoomCode)] = "Mã phòng không được để trống.";
            }
            else if (model.RoomCode.Any(char.IsWhiteSpace))
            {
                errors[nameof(model.RoomCode)] = "Mã phòng không được chứa dấu cách hoặc khoảng trắng.";
            }
            else if (model.RoomCode.Length > 50)
            {
                errors[nameof(model.RoomCode)] = "Mã phòng không được vượt quá 50 ký tự.";
            }
            else if (!Regex.IsMatch(model.RoomCode, @"^[A-Za-z0-9]+$"))
            {
                errors[nameof(model.RoomCode)] = "Mã phòng chỉ được bao gồm chữ hoa, chữ thường và số; không được chứa dấu cách hoặc ký tự đặc biệt.";
            }
            else if (await _context.Rooms.AnyAsync(
                         x => x.RoomCode == model.RoomCode && x.Id != room.Id,
                         cancellationToken))
            {
                errors[nameof(model.RoomCode)] = "Mã phòng đã tồn tại.";
            }

            if (await IsPositionUsedAsync(
                    model.Floor,
                    model.Row,
                    model.LeftToRightOrder,
                    room.Id,
                    cancellationToken))
            {
                errors[nameof(model.LeftToRightOrder)] =
                    "Vị trí Tầng + Hàng + Thứ tự đã được sử dụng bởi một phòng khác.";
            }

            var roomType = await _context.RoomTypes
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x => x.Id == model.RoomTypeId!.Value && !x.IsDeleted,
                    cancellationToken);

            if (roomType is null)
            {
                errors[nameof(model.RoomTypeId)] = "Loại phòng không tồn tại hoặc đã bị xóa.";
            }
            else if (room.IsActive && !roomType.IsActive)
            {
                errors[nameof(model.RoomTypeId)] =
                    "Phòng đang hoạt động chỉ được gán với loại phòng đang hoạt động.";
            }

            if (errors.Count > 0)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                return (false, "Không thể lưu thay đổi phòng.", errors);
            }

            room.RoomCode = model.RoomCode;
            room.RoomName = model.RoomName;
            room.Floor = model.Floor;
            room.Row = model.Row;
            room.LeftToRightOrder = model.LeftToRightOrder;
            room.RoomTypeId = model.RoomTypeId!.Value;
            // IsActive không được thay đổi tại chức năng Sửa phòng.
            room.Description = NormalizeOptional(model.Description);
            room.Note = NormalizeOptional(model.Note);
            room.LastModifiedBy = currentUserId;
            room.LastModifiedAt = DateTime.Now;

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return (true, null, errors);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            return (
                false,
                "Không thể lưu thay đổi phòng.",
                new Dictionary<string, string>
                {
                    [string.Empty] = "Không thể lưu thay đổi phòng. Vui lòng kiểm tra lại dữ liệu."
                });
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            return (
                false,
                "Không thể lưu thay đổi phòng. Vui lòng thử lại.",
                new Dictionary<string, string>
                {
                    [string.Empty] = "Không thể lưu thay đổi phòng. Vui lòng thử lại."
                });
        }
    }

    public async Task<(bool Succeeded, string? ErrorMessage)> DeactivateAsync(
        Guid id,
        Guid currentUserId,
        bool isMaintenance,
        CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
            return (false, "Phòng không hợp lệ.");

        if (currentUserId == Guid.Empty)
            return (false, "Không xác định được người dùng đang đăng nhập.");

        var room = await _context.Rooms
            .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken);

        if (room is null)
            return (false, "Không tìm thấy phòng.");

        if (room.Status == 1)
            return (false, "Không thể dừng hoạt động phòng đang sử dụng.");

        if (!room.IsActive)
            return (false, "Phòng đã ở trạng thái dừng hoạt động.");

        // Chỉ chuyển sang Bảo trì (Status = 4) khi người dùng xác nhận
        // nguyên nhân dừng hoạt động là do bảo trì. Nếu không, giữ nguyên Status.
        if (isMaintenance)
        {
            room.Status = 4;
        }

        room.IsActive = false;
        room.LastModifiedBy = currentUserId;
        room.LastModifiedAt = DateTime.Now;

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            return (true, null);
        }
        catch (DbUpdateException)
        {
            return (false, "Không thể dừng hoạt động phòng. Vui lòng thử lại.");
        }
    }

    public async Task<(bool Succeeded, string? ErrorMessage)> ActivateAsync(
        Guid id,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
            return (false, "Phòng không hợp lệ.");

        if (currentUserId == Guid.Empty)
            return (false, "Không xác định được người dùng đang đăng nhập.");

        var room = await _context.Rooms
            .Include(x => x.RoomType)
            .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken);

        if (room is null)
            return (false, "Không tìm thấy phòng.");

        if (room.IsActive)
            return (false, "Phòng đã đang hoạt động.");

        if (room.RoomType is null || room.RoomType.IsDeleted)
            return (false, "Không thể mở hoạt động vì loại phòng của phòng không còn tồn tại.");

        if (!room.RoomType.IsActive)
            return (false, "Không thể mở hoạt động phòng vì loại phòng đang dừng hoạt động.");

        // Nếu phòng đang ở trạng thái Bảo trì, việc mở hoạt động bắt buộc
        // phải đưa phòng về trạng thái Trống (Status = 0).
        // Cần sửa lại chức năng này, cho phép người dùng chọn status sau khi mở hoạt động là: Trống hay Chờ dọn dẹp
        if (room.Status == 4)
        {
            room.Status = 0;
        }

        // Các trạng thái khác được giữ nguyên theo yêu cầu nghiệp vụ.
        room.IsActive = true;
        room.LastModifiedBy = currentUserId;
        room.LastModifiedAt = DateTime.Now;

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            return (true, null);
        }
        catch (DbUpdateException)
        {
            return (false, "Không thể mở hoạt động phòng. Vui lòng thử lại.");
        }
    }

    public async Task<(bool Succeeded, string? ErrorMessage)> DeleteAsync(
        Guid id,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
            return (false, "Phòng không hợp lệ.");

        if (currentUserId == Guid.Empty)
            return (false, "Không xác định được người dùng đang đăng nhập.");

        await using var transaction = await _context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        try
        {
            var room = await _context.Rooms
                .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken);

            if (room is null)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                return (false, "Không tìm thấy phòng.");
            }

            if (room.IsActive)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                return (false, "Không thể xóa phòng đang hoạt động. Vui lòng dừng hoạt động trước.");
            }

            if (room.Status == 1)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                return (false, "Không thể xóa phòng đang sử dụng.");
            }

            room.IsDeleted = true;
            room.LastModifiedBy = currentUserId;
            room.LastModifiedAt = DateTime.Now;

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
            return (false, "Không thể xóa phòng. Vui lòng thử lại.");
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            return (false, "Không thể xóa phòng. Vui lòng thử lại.");
        }
    }

    private async Task<IReadOnlyList<RoomTypeOptionViewModel>> GetRoomTypeOptionsAsync(
        bool includeInactive,
        Guid? currentRoomTypeId,
        CancellationToken cancellationToken)
    {
        var types = await _context.RoomTypes
            .AsNoTracking()
            .Where(x => !x.IsDeleted && (includeInactive || x.IsActive))
            .OrderBy(x => x.RoomTypeCode)
            .Select(x => new RoomTypeOptionViewModel
            {
                Id = x.Id,
                Code = x.RoomTypeCode,
                Name = x.RoomTypeName,
                IsActive = x.IsActive,
                IsSelectable = true
            })
            .ToListAsync(cancellationToken);

        // Trường hợp dữ liệu cũ đang ở trạng thái:
        // Room.IsActive = true nhưng RoomType.IsActive = false.
        // Hiển thị loại phòng hiện tại nhưng không cho chọn lại.
        if (currentRoomTypeId.HasValue &&
            currentRoomTypeId.Value != Guid.Empty &&
            types.All(x => x.Id != currentRoomTypeId.Value))
        {
            var current = await _context.RoomTypes
                .AsNoTracking()
                .Where(x => x.Id == currentRoomTypeId.Value && !x.IsDeleted)
                .Select(x => new RoomTypeOptionViewModel
                {
                    Id = x.Id,
                    Code = x.RoomTypeCode,
                    Name = x.RoomTypeName,
                    IsActive = x.IsActive,
                    IsSelectable = false
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (current is not null)
                types = types.Concat(new[] { current })
                    .OrderBy(x => x.Code)
                    .ToList();
        }

        return types;
    }

    private async Task<bool> IsPositionUsedAsync(
        int floor,
        int row,
        int leftToRightOrder,
        Guid? excludeRoomId,
        CancellationToken cancellationToken)
    {
        return await _context.Rooms.AnyAsync(
            x => !x.IsDeleted &&
                 x.Floor == floor &&
                 x.Row == row &&
                 x.LeftToRightOrder == leftToRightOrder &&
                 (!excludeRoomId.HasValue || x.Id != excludeRoomId.Value),
            cancellationToken);
    }

    private static IEnumerable<RoomListItemViewModel> SortRooms(
        IEnumerable<RoomListItemViewModel> rooms,
        RoomSortOption sort)
    {
        return sort switch
        {
            RoomSortOption.LeftToRightDesc => rooms
                .OrderByDescending(x => x.LeftToRightOrder)
                .ThenBy(x => x.RoomCode),

            RoomSortOption.RoomCodeAsc => rooms
                .OrderBy(x => x.RoomCode)
                .ThenBy(x => x.LeftToRightOrder),

            RoomSortOption.RoomCodeDesc => rooms
                .OrderByDescending(x => x.RoomCode)
                .ThenBy(x => x.LeftToRightOrder),

            RoomSortOption.RoomNameAsc => rooms
                .OrderBy(x => x.RoomName)
                .ThenBy(x => x.LeftToRightOrder),

            RoomSortOption.RoomNameDesc => rooms
                .OrderByDescending(x => x.RoomName)
                .ThenBy(x => x.LeftToRightOrder),

            _ => rooms
                .OrderBy(x => x.LeftToRightOrder)
                .ThenBy(x => x.RoomCode)
        };
    }

    private static RoomSortOption NormalizeSort(RoomSortOption sort)
    {
        return Enum.IsDefined(sort)
            ? sort
            : RoomSortOption.LeftToRightAsc;
    }

    private static byte? NormalizeStatus(byte? status)
    {
        return status is >= 0 and <= 4
            ? status
            : null;
    }

    private static int NormalizePageSize(int pageSize)
    {
        return pageSize switch
        {
            5 => 5,
            10 => 10,
            20 => 20,
            50 => 50,
            _ => 5
        };
    }

    private static void NormalizeCreateModel(RoomCreateViewModel model)
    {
        model.RoomCode = (model.RoomCode ?? string.Empty).Trim();
        model.RoomName = (model.RoomName ?? string.Empty).Trim();
        model.Description = NormalizeOptional(model.Description);
        model.Note = NormalizeOptional(model.Note);
    }

    private static void NormalizeEditModel(RoomEditViewModel model)
    {
        model.RoomCode = (model.RoomCode ?? string.Empty).Trim();
        model.RoomName = (model.RoomName ?? string.Empty).Trim();
        model.Description = NormalizeOptional(model.Description);
        model.Note = NormalizeOptional(model.Note);
    }

    private static void ValidateRoomCode(
        string? roomCode,
        IDictionary<string, string> errors)
    {
        var code = roomCode?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(code))
        {
            errors[nameof(RoomCreateViewModel.RoomCode)] = "Vui lòng nhập mã phòng.";
            return;
        }

        if (code.Length > 50)
        {
            errors[nameof(RoomCreateViewModel.RoomCode)] = "Mã phòng không được vượt quá 50 ký tự.";
            return;
        }

        if (!Regex.IsMatch(code, @"^[A-Za-z0-9]+$"))
            errors[nameof(RoomCreateViewModel.RoomCode)] = "Mã phòng chỉ được bao gồm chữ hoa, chữ thường và số; không được chứa dấu cách hoặc ký tự đặc biệt.";
    }

    private static void ValidateRoomName(
        string? roomName,
        IDictionary<string, string> errors)
    {
        if (string.IsNullOrWhiteSpace(roomName))
            errors[nameof(RoomCreateViewModel.RoomName)] = "Vui lòng nhập tên phòng.";
        else if (roomName.Trim().Length > 100)
            errors[nameof(RoomCreateViewModel.RoomName)] = "Tên phòng không được vượt quá 100 ký tự.";
    }

    private static void ValidatePosition(
        int floor,
        int row,
        int order,
        IDictionary<string, string> errors)
    {
        if (floor < 1 || floor > 1000)
            errors[nameof(RoomCreateViewModel.Floor)] = "Tầng phải từ 1 đến 1000.";

        if (row < 1 || row > 1000)
            errors[nameof(RoomCreateViewModel.Row)] = "Hàng phải từ 1 đến 1000.";

        if (order < 1 || order > 1000)
            errors[nameof(RoomCreateViewModel.LeftToRightOrder)] = "Thứ tự phải từ 1 đến 1000.";
    }

    private static string? NormalizeOptional(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }
}
