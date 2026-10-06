using System.Data;
using System.ComponentModel.DataAnnotations;
using Hotel_Management_Website.IServices;
using Hotel_Management_Website.Models;
using Hotel_Management_Website.Models.ViewModels.Guest;
using Hotel_Management_Website.Models.ViewModels.UI;
using Microsoft.EntityFrameworkCore;

namespace Hotel_Management_Website.Services;

public sealed class GuestService : IGuestService
{
    private static readonly HashSet<byte> FullEditableStatuses = new() { 0, 1 };
    private static readonly HashSet<byte> FullRequiredStatus = new() { 2 };
    private static readonly HashSet<byte> TerminalStatuses = new() { 3, 4, 5, 6, 7, 8, 9 };
    private static readonly HashSet<byte> DeletableStatuses = new() { 4, 5, 7, 8, 9 };

    private readonly AppDbContext _context;

    public GuestService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<GuestIndexViewModel> GetIndexAsync(
        GuestListFilterViewModel filter,
        CancellationToken cancellationToken = default)
    {
        filter ??= new GuestListFilterViewModel();

        var search = filter.Search?.Trim() ?? string.Empty;
        var sort = NormalizeSort(filter.Sort);
        var sex = NormalizeSex(filter.Sex);
        var pageSize = NormalizePageSize(filter.PageSize);
        var requestedPage = Math.Max(filter.CurrentPage, 1);

        var query = _context.Guests
            .AsNoTracking()
            .Where(x => !x.IsDeleted);

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(x =>
                x.GuestCode.Contains(search) ||
                x.Name.Contains(search));
        }

        if (sex.HasValue)
        {
            query = query.Where(x => x.Sex == sex.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
        var currentPage = Math.Min(requestedPage, totalPages);

        var guests = await ApplySort(query, sort)
            .Skip((currentPage - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new GuestListItemViewModel
            {
                Id = x.Id,
                GuestCode = x.GuestCode,
                Name = x.Name,
                DateOfBirth = x.DateOfBirth,
                Sex = x.Sex,
                PhoneNumber = x.PhoneNumber,
                Email = x.Email,
                LastModifiedAt = x.LastModifiedAt
            })
            .ToListAsync(cancellationToken);

        await PopulatePermissionsAsync(guests, cancellationToken);

        return new GuestIndexViewModel
        {
            Filter = new GuestListFilterViewModel
            {
                Search = search,
                Sort = sort,
                Sex = sex,
                CurrentPage = currentPage,
                PageSize = pageSize
            },
            Guests = guests,
            TotalCount = totalCount,
            Pagination = new PaginationViewModel
            {
                CurrentPage = currentPage,
                PageSize = pageSize,
                TotalItems = totalCount,
                PageParam = "Filter.CurrentPage",
                PageSizeParam = "Filter.PageSize",
                BaseUrl = "/Guest/Index",
                ItemLabel = "khách hàng"
            }
        };
    }

    public async Task<GuestDetailsViewModel?> GetDetailsAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
            return null;

        return await _context.Guests
            .AsNoTracking()
            .Where(x => x.Id == id && !x.IsDeleted)
            .Select(x => new GuestDetailsViewModel
            {
                Id = x.Id,
                GuestCode = x.GuestCode,
                Name = x.Name,
                DateOfBirth = x.DateOfBirth,
                Sex = x.Sex,
                Address = x.Address,
                PhoneNumber = x.PhoneNumber,
                Email = x.Email,
                IdentityNumber = x.IdentityNumber,
                IdentityType = x.IdentityType,
                Note = x.Note,
                CreatedAt = x.CreatedAt,
                LastModifiedAt = x.LastModifiedAt
            })
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<GuestEditViewModel?> GetEditModelAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
            return null;

        var guest = await _context.Guests
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken);

        if (guest is null)
            return null;

        var statuses = await GetActiveBookingStatusesAsync(id, cancellationToken);
        var rule = ResolveRule(statuses);

        return new GuestEditViewModel
        {
            Id = guest.Id,
            GuestCode = guest.GuestCode,
            Name = guest.Name,
            DateOfBirth = guest.DateOfBirth,
            Sex = guest.Sex,
            Address = guest.Address,
            PhoneNumber = guest.PhoneNumber,
            Email = guest.Email,
            IdentityNumber = guest.IdentityNumber,
            IdentityType = guest.IdentityType,
            Note = guest.Note,
            CanEditDetails = rule.CanEditDetails,
            RequireFullDetails = rule.RequireFullDetails,
            CanEditNoteOnly = rule.CanEditNoteOnly,
            BookingRuleDescription = BuildRuleDescription(statuses, rule)
        };
    }

    public async Task<GuestActionResult> UpdateAsync(
        GuestEditViewModel model,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        if (currentUserId == Guid.Empty)
            return Fail("Không xác định được người dùng đang đăng nhập.");

        if (model is null || model.Id == Guid.Empty)
            return Fail("Dữ liệu chỉnh sửa khách hàng không hợp lệ.");

        var errors = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var now = DateTime.Now;

        await using var transaction = await _context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        try
        {
            var guest = await _context.Guests
                .FirstOrDefaultAsync(x => x.Id == model.Id && !x.IsDeleted, cancellationToken);

            if (guest is null)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                return Fail("Khách hàng không tồn tại hoặc đã bị xóa.");
            }

            var statuses = await GetActiveBookingStatusesAsync(guest.Id, cancellationToken);
            var rule = ResolveRule(statuses);

            if (rule.RejectEdit)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                return Fail("Khách hàng không có lịch đặt phòng hợp lệ để chỉnh sửa.");
            }

            if (rule.CanEditDetails)
            {
                ValidateGuestFields(model, rule.RequireFullDetails, errors);

                if (!errors.ContainsKey(nameof(GuestEditViewModel.GuestCode)))
                {
                    var normalizedCode = model.GuestCode.Trim();
                    if (await _context.Guests.AnyAsync(
                            x => x.Id != guest.Id && !x.IsDeleted && x.GuestCode == normalizedCode,
                            cancellationToken))
                    {
                        errors[nameof(GuestEditViewModel.GuestCode)] = "Mã khách hàng đã tồn tại.";
                    }
                }

                if (errors.Count > 0)
                {
                    await transaction.RollbackAsync(CancellationToken.None);
                    return new GuestActionResult
                    {
                        Succeeded = false,
                        ErrorMessage = "Vui lòng kiểm tra lại các thông tin được đánh dấu.",
                        FieldErrors = errors
                    };
                }

                ApplyFullGuestEdit(guest, model, currentUserId, now);
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(model.Note) && model.Note.Trim().Length > 255)
                {
                    await transaction.RollbackAsync(CancellationToken.None);
                    return new GuestActionResult
                    {
                        Succeeded = false,
                        ErrorMessage = "Ghi chú khách hàng không được vượt quá 255 ký tự.",
                        FieldErrors = new Dictionary<string, string>
                        {
                            [nameof(GuestEditViewModel.Note)] = "Ghi chú khách hàng không được vượt quá 255 ký tự."
                        }
                    };
                }

                guest.Note = NormalizeOptional(model.Note);
                guest.LastModifiedBy = currentUserId;
                guest.LastModifiedAt = now;
            }

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return new GuestActionResult { Succeeded = true };
        }
        catch (OperationCanceledException)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            return Fail("Không thể cập nhật thông tin khách hàng. Vui lòng thử lại.");
        }
    }

    public async Task<GuestActionResult> DeleteAsync(
        Guid id,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        if (currentUserId == Guid.Empty)
            return Fail("Không xác định được người dùng đang đăng nhập.");

        if (id == Guid.Empty)
            return Fail("Khách hàng không hợp lệ.");

        var now = DateTime.Now;

        await using var transaction = await _context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        try
        {
            var guest = await _context.Guests
                .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken);

            if (guest is null)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                return Fail("Khách hàng không tồn tại hoặc đã bị xóa.");
            }

            var bookings = await _context.Bookings
                .Where(x => x.GuestId == guest.Id && !x.IsDeleted)
                .OrderBy(x => x.CreatedAt)
                .ToListAsync(cancellationToken);

            if (bookings.Count == 0)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                return Fail("Khách hàng không còn lịch đặt phòng hợp lệ để xóa.");
            }

            if (bookings.Any(x => !DeletableStatuses.Contains(x.Status)))
            {
                await transaction.RollbackAsync(CancellationToken.None);
                return Fail("Không thể xóa khách hàng vì còn lịch đặt phòng chưa ở trạng thái được phép xóa.");
            }

            var bookingIds = bookings.Select(x => x.Id).ToArray();

            var bookingRooms = await _context.BookingRooms
                .Where(x => bookingIds.Contains(x.BookingId) && !x.IsDeleted)
                .ToListAsync(cancellationToken);

            var bookingRoomIds = bookingRooms.Select(x => x.Id).ToArray();

            var bookingServices = bookingRoomIds.Length == 0
                ? new List<BookingRoomService>()
                : await _context.BookingRoomServices
                    .Where(x => bookingRoomIds.Contains(x.BookingRoomId) && !x.IsDeleted)
                    .ToListAsync(cancellationToken);

            var additionalFees = bookingRoomIds.Length == 0
                ? new List<AdditionalFeeRoom>()
                : await _context.AdditionalFeeRooms
                    .Where(x => bookingRoomIds.Contains(x.BookingRoomId) && !x.IsDeleted)
                    .ToListAsync(cancellationToken);

            guest.IsDeleted = true;
            guest.LastModifiedBy = currentUserId;
            guest.LastModifiedAt = now;

            foreach (var booking in bookings)
            {
                booking.IsDeleted = true;
                booking.LastModifiedBy = currentUserId;
                booking.LastModifiedAt = now;
            }

            foreach (var bookingRoom in bookingRooms)
            {
                bookingRoom.IsDeleted = true;
                bookingRoom.LastModifiedBy = currentUserId;
                bookingRoom.LastModifiedAt = now;
            }

            foreach (var service in bookingServices)
            {
                service.IsDeleted = true;
                service.LastModifiedBy = currentUserId;
                service.LastModifiedAt = now;
            }

            foreach (var fee in additionalFees)
            {
                fee.IsDeleted = true;
                fee.LastModifiedBy = currentUserId;
                fee.LastModifiedAt = now;
            }

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return new GuestActionResult { Succeeded = true };
        }
        catch (OperationCanceledException)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            return Fail("Không thể xóa khách hàng và dữ liệu Booking liên quan. Vui lòng thử lại.");
        }
    }

    private async Task<List<byte>> GetActiveBookingStatusesAsync(
        Guid guestId,
        CancellationToken cancellationToken)
    {
        return await _context.Bookings
            .AsNoTracking()
            .Where(x => x.GuestId == guestId && !x.IsDeleted)
            .Select(x => x.Status)
            .ToListAsync(cancellationToken);
    }

    private async Task PopulatePermissionsAsync(
        IReadOnlyList<GuestListItemViewModel> guests,
        CancellationToken cancellationToken)
    {
        if (guests.Count == 0)
            return;

        var ids = guests.Select(x => x.Id).ToArray();
        var rows = await _context.Bookings
            .AsNoTracking()
            .Where(x => x.GuestId.HasValue && ids.Contains(x.GuestId.Value) && !x.IsDeleted)
            .Select(x => new { GuestId = x.GuestId!.Value, x.Status })
            .ToListAsync(cancellationToken);

        foreach (var guest in guests)
        {
            var statuses = rows
                .Where(x => x.GuestId == guest.Id)
                .Select(x => x.Status)
                .ToList();

            var rule = ResolveRule(statuses);
            guest.CanEditDetails = rule.CanEditDetails;
            guest.RequireFullDetails = rule.RequireFullDetails;
            guest.CanEditNoteOnly = rule.CanEditNoteOnly;
            guest.CanDelete = statuses.Count > 0 && statuses.All(DeletableStatuses.Contains);
        }
    }

    private static GuestRule ResolveRule(IReadOnlyCollection<byte> statuses)
    {
        if (statuses.Count == 0)
        {
            return new GuestRule(false, false, true, true);
        }

        if (statuses.Any(FullRequiredStatus.Contains))
            return new GuestRule(true, true, false, false);

        if (statuses.Any(FullEditableStatuses.Contains))
            return new GuestRule(true, false, false, false);

        if (statuses.All(TerminalStatuses.Contains))
            return new GuestRule(false, false, true, false);

        return new GuestRule(false, false, true, false);
    }

    private static string BuildRuleDescription(
        IReadOnlyCollection<byte> statuses,
        GuestRule rule)
    {
        if (statuses.Count == 0)
            return "Khách hàng hiện không có Booking đang tồn tại; chỉ cho phép sửa Ghi chú.";

        if (rule.RequireFullDetails)
            return "Có Booking đang ở trạng thái Đang ở: Mã, Tên, Ngày sinh, Giới tính, Địa chỉ, liên hệ và giấy tờ phải được khai báo đầy đủ.";

        if (rule.CanEditDetails)
            return "Booking đang ở Chờ xác nhận hoặc Chờ check in: Mã và Tên bắt buộc; phải có ít nhất Số điện thoại hoặc Email; các thông tin còn lại được phép để trống.";

        return "Các Booking đã ở trạng thái kết thúc/hủy/thất bại: chỉ cho phép sửa Ghi chú khách hàng.";
    }

    private static void ValidateGuestFields(
        GuestEditViewModel model,
        bool requireFullDetails,
        IDictionary<string, string> errors)
    {
        var code = model.GuestCode?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(code))
            errors[nameof(GuestEditViewModel.GuestCode)] = "Mã khách hàng không được để trống.";
        else if (model.GuestCode.Any(char.IsWhiteSpace))
            errors[nameof(GuestEditViewModel.GuestCode)] = "Mã khách hàng không được chứa dấu cách hoặc khoảng trắng.";
        else if (code.Length > 50)
            errors[nameof(GuestEditViewModel.GuestCode)] = "Mã khách hàng không được vượt quá 50 ký tự.";

        var name = model.Name?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(name))
            errors[nameof(GuestEditViewModel.Name)] = "Tên khách hàng là bắt buộc.";
        else if (name.Length > 100)
            errors[nameof(GuestEditViewModel.Name)] = "Tên khách hàng không được vượt quá 100 ký tự.";

        if (model.DateOfBirth.HasValue)
        {
            if (model.DateOfBirth.Value.Date > DateTime.Today)
                errors[nameof(GuestEditViewModel.DateOfBirth)] = "Ngày sinh không được lớn hơn ngày hiện tại.";
        }
        else if (requireFullDetails)
        {
            errors[nameof(GuestEditViewModel.DateOfBirth)] = "Ngày sinh là bắt buộc khi Booking đang ở trạng thái Đang ở.";
        }

        if (model.Sex.HasValue)
        {
            if (model.Sex is not 0 and not 1 and not 2)
                errors[nameof(GuestEditViewModel.Sex)] = "Giới tính không hợp lệ.";
        }
        else if (requireFullDetails)
        {
            errors[nameof(GuestEditViewModel.Sex)] = "Giới tính là bắt buộc khi Booking đang ở trạng thái Đang ở.";
        }

        var address = model.Address?.Trim();
        if (requireFullDetails && string.IsNullOrWhiteSpace(address))
            errors[nameof(GuestEditViewModel.Address)] = "Địa chỉ là bắt buộc khi Booking đang ở trạng thái Đang ở.";
        else if (!string.IsNullOrWhiteSpace(address) && address.Length > 255)
            errors[nameof(GuestEditViewModel.Address)] = "Địa chỉ không được vượt quá 255 ký tự.";

        var phone = model.PhoneNumber?.Trim();
        var email = model.Email?.Trim();
        if (string.IsNullOrWhiteSpace(phone) && string.IsNullOrWhiteSpace(email))
        {
            errors[nameof(GuestEditViewModel.PhoneNumber)] = "Phải nhập ít nhất Số điện thoại hoặc Email.";
            errors[nameof(GuestEditViewModel.Email)] = "Phải nhập ít nhất Số điện thoại hoặc Email.";
        }
        else if (!string.IsNullOrWhiteSpace(phone) && phone.Length > 20)
        {
            errors[nameof(GuestEditViewModel.PhoneNumber)] = "Số điện thoại không được vượt quá 20 ký tự.";
        }

        if (!string.IsNullOrWhiteSpace(email))
        {
            if (email.Length > 4000 || !new EmailAddressAttribute().IsValid(email))
                errors[nameof(GuestEditViewModel.Email)] = "Email không hợp lệ.";
        }

        var identityNumber = model.IdentityNumber?.Trim();
        if (requireFullDetails && string.IsNullOrWhiteSpace(identityNumber))
            errors[nameof(GuestEditViewModel.IdentityNumber)] = "Số giấy tờ là bắt buộc khi Booking đang ở trạng thái Đang ở.";
        else if (!string.IsNullOrWhiteSpace(identityNumber) && identityNumber.Length > 50)
            errors[nameof(GuestEditViewModel.IdentityNumber)] = "Số giấy tờ không được vượt quá 50 ký tự.";

        if (requireFullDetails && !model.IdentityType.HasValue)
            errors[nameof(GuestEditViewModel.IdentityType)] = "Loại giấy tờ là bắt buộc khi Booking đang ở trạng thái Đang ở.";
        else if (model.IdentityType.HasValue && model.IdentityType is not 0 and not 1 and not 2)
            errors[nameof(GuestEditViewModel.IdentityType)] = "Loại giấy tờ không hợp lệ.";

        if (!string.IsNullOrWhiteSpace(model.Note) && model.Note.Trim().Length > 255)
            errors[nameof(GuestEditViewModel.Note)] = "Ghi chú khách hàng không được vượt quá 255 ký tự.";
    }

    private static void ApplyFullGuestEdit(
        Guest guest,
        GuestEditViewModel model,
        Guid currentUserId,
        DateTime now)
    {
        guest.GuestCode = model.GuestCode.Trim();
        guest.Name = model.Name.Trim();
        guest.DateOfBirth = model.DateOfBirth?.Date;
        guest.Sex = model.Sex;
        guest.Address = NormalizeOptional(model.Address);
        guest.PhoneNumber = NormalizeOptional(model.PhoneNumber);
        guest.Email = NormalizeOptional(model.Email);
        guest.IdentityNumber = NormalizeOptional(model.IdentityNumber);
        guest.IdentityType = model.IdentityType;
        guest.Note = NormalizeOptional(model.Note);
        guest.LastModifiedBy = currentUserId;
        guest.LastModifiedAt = now;
    }

    private static IQueryable<Guest> ApplySort(
        IQueryable<Guest> query,
        GuestSortOption sort)
    {
        return sort switch
        {
            GuestSortOption.GuestCodeAsc => query.OrderBy(x => x.GuestCode),
            GuestSortOption.GuestCodeDesc => query.OrderByDescending(x => x.GuestCode),
            GuestSortOption.NameAsc => query.OrderBy(x => x.Name),
            GuestSortOption.NameDesc => query.OrderByDescending(x => x.Name),
            GuestSortOption.DateOfBirthAsc => query
                .OrderBy(x => x.DateOfBirth == null)
                .ThenBy(x => x.DateOfBirth),
            GuestSortOption.DateOfBirthDesc => query
                .OrderBy(x => x.DateOfBirth == null)
                .ThenByDescending(x => x.DateOfBirth),
            GuestSortOption.LastModifiedAsc => query.OrderBy(x => x.LastModifiedAt),
            _ => query.OrderByDescending(x => x.LastModifiedAt)
        };
    }

    private static GuestSortOption NormalizeSort(GuestSortOption sort) =>
        Enum.IsDefined(sort) ? sort : GuestSortOption.LastModifiedDesc;

    private static byte? NormalizeSex(byte? sex) =>
        sex is 0 or 1 or 2 ? sex : null;

    private static int NormalizePageSize(int pageSize) =>
        pageSize is 20 or 50 ? pageSize : 10;

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static GuestActionResult Fail(string message) =>
        new()
        {
            Succeeded = false,
            ErrorMessage = message
        };

    private readonly record struct GuestRule(
        bool CanEditDetails,
        bool RequireFullDetails,
        bool CanEditNoteOnly,
        bool RejectEdit);
}
