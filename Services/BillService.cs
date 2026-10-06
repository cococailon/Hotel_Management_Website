using Hotel_Management_Website.IServices;
using Hotel_Management_Website.Models.ViewModels.Bill;
using Hotel_Management_Website.Models.ViewModels.UI;
using Microsoft.EntityFrameworkCore;

namespace Hotel_Management_Website.Services;

public sealed class BillService : IBillService
{
    private readonly Models.AppDbContext _context;

    public BillService(Models.AppDbContext context)
    {
        _context = context;
    }

    public async Task<BillIndexViewModel> GetIndexAsync(
        BillListFilterViewModel filter,
        CancellationToken cancellationToken = default)
    {
        filter ??= new BillListFilterViewModel();

        var normalizedSearch = filter.Search?.Trim() ?? string.Empty;
        var normalizedSort = NormalizeSort(filter.Sort);
        var normalizedStatus = NormalizeStatus(filter.Status);
        var pageSize = NormalizePageSize(filter.PageSize);
        var requestedPage = Math.Max(filter.CurrentPage, 1);

        var query = _context.Bills
            .AsNoTracking()
            .Where(x => !x.IsDeleted);

        if (!string.IsNullOrWhiteSpace(normalizedSearch))
        {
            query = query.Where(x =>
                x.BillCode.Contains(normalizedSearch) ||
                (x.Booking != null && x.Booking.BookingCode.Contains(normalizedSearch)));
        }

        if (normalizedStatus.HasValue)
        {
            query = query.Where(x => x.Status == normalizedStatus.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var totalPages = totalCount == 0
            ? 1
            : (int)Math.Ceiling(totalCount / (double)pageSize);

        var currentPage = Math.Min(requestedPage, totalPages);

        var rows = await ApplySort(query, normalizedSort)
            .Skip((currentPage - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new
            {
                x.Id,
                x.BillCode,
                BookingCode = x.Booking != null
                    ? x.Booking.BookingCode
                    : string.Empty,
                x.TotalAmount,
                x.DepositAmount,
                x.FinalAmount,
                x.PaymentDate,
                x.Status
            })
            .ToListAsync(cancellationToken);

        var bills = rows
            .Select(x => new BillListItemViewModel
            {
                Id = x.Id,
                BillCode = x.BillCode,
                BookingCode = x.BookingCode,
                TotalAmount = x.TotalAmount,
                DepositAmount = x.DepositAmount,
                FinalAmount = x.FinalAmount,
                PaidAt = x.PaymentDate,
                Status = x.Status,
                StatusName = GetBillStatusName(x.Status),
                StatusBadgeClass = GetBillStatusBadgeClass(x.Status)
            })
            .ToList();

        return new BillIndexViewModel
        {
            Filter = new BillListFilterViewModel
            {
                Search = normalizedSearch,
                Sort = normalizedSort,
                Status = normalizedStatus,
                CurrentPage = currentPage,
                PageSize = pageSize
            },
            Bills = bills,
            TotalCount = totalCount,
            Pagination = new PaginationViewModel
            {
                CurrentPage = currentPage,
                PageSize = pageSize,
                TotalItems = totalCount,
                PageParam = "Filter.CurrentPage",
                PageSizeParam = "Filter.PageSize",
                BaseUrl = "/Bill/Index",
                ItemLabel = "hóa đơn"
            }
        };
    }

    public async Task<BillDetailsViewModel?> GetDetailsAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
        {
            return null;
        }

        var bill = await _context.Bills
            .AsNoTracking()
            .Where(x => x.Id == id && !x.IsDeleted)
            .Select(x => new
            {
                x.Id,
                x.BillCode,
                x.BookingId,
                BookingCode = x.Booking != null ? x.Booking.BookingCode : string.Empty,
                x.TotalAmount,
                x.DepositAmount,
                x.FinalAmount,
                x.PaymentMethod,
                x.PaymentDate,
                x.ReferenceCode,
                DepositPaymentMethod = x.Booking != null ? x.Booking.PaymentMethod : null,
                DepositReferenceCode = x.Booking != null ? x.Booking.ReferenceCode : null,
                DepositPaymentDate = x.Booking != null ? x.Booking.PaymentDate : null,
                x.Status,
                x.Note,
                x.CreatedAt,
                x.LastModifiedAt,
                BookingType = x.Booking != null
                    ? (byte?)x.Booking.BookingType
                    : null,
                GuestCode = x.Booking != null && x.Booking.Guest != null
                    ? x.Booking.Guest.GuestCode
                    : string.Empty,
                GuestName = x.Booking != null && x.Booking.Guest != null
                    ? x.Booking.Guest.Name
                    : string.Empty,
                GuestDateOfBirth = x.Booking != null && x.Booking.Guest != null
                    ? x.Booking.Guest.DateOfBirth
                    : null,
                GuestSex = x.Booking != null && x.Booking.Guest != null
                    ? x.Booking.Guest.Sex
                    : null,
                GuestAddress = x.Booking != null && x.Booking.Guest != null
                    ? x.Booking.Guest.Address
                    : null,
                GuestPhoneNumber = x.Booking != null && x.Booking.Guest != null
                    ? x.Booking.Guest.PhoneNumber
                    : null,
                GuestEmail = x.Booking != null && x.Booking.Guest != null
                    ? x.Booking.Guest.Email
                    : null,
                GuestIdentityNumber = x.Booking != null && x.Booking.Guest != null
                    ? x.Booking.Guest.IdentityNumber
                    : null,
                GuestIdentityType = x.Booking != null && x.Booking.Guest != null
                    ? x.Booking.Guest.IdentityType
                    : null,
                GuestNote = x.Booking != null && x.Booking.Guest != null
                    ? x.Booking.Guest.Note
                    : null,
                BookingExpectedCheckInAt = x.Booking != null
                    ? x.Booking.ExpectedCheckInAt
                    : (DateTime?)null,
                BookingExpectedCheckOutAt = x.Booking != null
                    ? x.Booking.ExpectedCheckOutAt
                    : (DateTime?)null,
                BookingActualCheckInAt = x.Booking != null
                    ? x.Booking.ActualCheckInAt
                    : (DateTime?)null,
                BookingActualCheckOutAt = x.Booking != null
                    ? x.Booking.ActualCheckOutAt
                    : (DateTime?)null,
                BookingStatus = x.Booking != null
                    ? (byte?)x.Booking.Status
                    : null
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (bill is null)
        {
            return null;
        }

        var billDetails = await _context.BillDetails
            .AsNoTracking()
            .Where(x => x.BillId == bill.Id && !x.IsDeleted)
            .OrderBy(x => x.CreatedAt)
            .Select(x => new BillDetailItemViewModel
            {
                Id = x.Id,
                BookingRoomId = x.BookingRoomId,
                RoomTypeCode = x.BookingRoom != null && x.BookingRoom.RoomType != null
                    ? x.BookingRoom.RoomType.RoomTypeCode
                    : string.Empty,
                RoomTypeName = x.BookingRoom != null && x.BookingRoom.RoomType != null
                    ? x.BookingRoom.RoomType.RoomTypeName
                    : string.Empty,
                RoomCode = x.BookingRoom != null && x.BookingRoom.Room != null
                    ? x.BookingRoom.Room.RoomCode
                    : string.Empty,
                RoomName = x.BookingRoom != null && x.BookingRoom.Room != null
                    ? x.BookingRoom.Room.RoomName
                    : string.Empty,
                Adults = x.BookingRoom != null ? x.BookingRoom.Adults : null,
                Children = x.BookingRoom != null ? x.BookingRoom.Children : null,
                TotalPrice = x.TotalPrice,
                Note = x.Note,
                CreatedAt = x.CreatedAt,
                LastModifiedAt = x.LastModifiedAt
            })
            .ToListAsync(cancellationToken);

        var bookingRoomIds = billDetails
            .Select(x => x.BookingRoomId)
            .Distinct()
            .ToArray();

        var services = bookingRoomIds.Length == 0
            ? new List<BillServiceItemViewModel>()
            : await _context.BookingRoomServices
                .AsNoTracking()
                .Where(x =>
                    bookingRoomIds.Contains(x.BookingRoomId) &&
                    !x.IsDeleted)
                .OrderBy(x => x.UsedAt)
                .Select(x => new BillServiceItemViewModel
                {
                    Id = x.Id,
                    BookingRoomId = x.BookingRoomId,
                    ServiceCode = x.Service != null
                        ? x.Service.ServiceCode
                        : string.Empty,
                    ServiceName = x.Service != null
                        ? x.Service.ServiceName
                        : string.Empty,
                    TotalPrice = x.TotalPrice,
                    UsedAt = x.UsedAt,
                    Note = x.Note
                })
                .ToListAsync(cancellationToken);

        var fees = bookingRoomIds.Length == 0
            ? new List<BillAdditionalFeeItemViewModel>()
            : await _context.AdditionalFeeRooms
                .AsNoTracking()
                .Where(x =>
                    bookingRoomIds.Contains(x.BookingRoomId) &&
                    !x.IsDeleted)
                .OrderBy(x => x.CreatedAt)
                .Select(x => new BillAdditionalFeeItemViewModel
                {
                    Id = x.Id,
                    BookingRoomId = x.BookingRoomId,
                    FeeName = x.FeeName,
                    FeeType = x.FeeType,
                    FeeTypeName = GetFeeTypeName(x.FeeType),
                    TotalFee = x.TotalFee,
                    Description = x.Decription
                })
                .ToListAsync(cancellationToken);

        var serviceLookup = services
            .GroupBy(x => x.BookingRoomId)
            .ToDictionary(
                x => x.Key,
                x => (IReadOnlyList<BillServiceItemViewModel>)x.ToList());

        var feeLookup = fees
            .GroupBy(x => x.BookingRoomId)
            .ToDictionary(
                x => x.Key,
                x => (IReadOnlyList<BillAdditionalFeeItemViewModel>)x.ToList());

        var detailRows = billDetails
            .Select(x => new BillDetailItemViewModel
            {
                Id = x.Id,
                BookingRoomId = x.BookingRoomId,
                RoomTypeCode = x.RoomTypeCode,
                RoomTypeName = x.RoomTypeName,
                RoomCode = x.RoomCode,
                RoomName = x.RoomName,
                Adults = x.Adults,
                Children = x.Children,
                TotalPrice = x.TotalPrice,
                Note = x.Note,
                CreatedAt = x.CreatedAt,
                LastModifiedAt = x.LastModifiedAt,
                Services = serviceLookup.TryGetValue(x.BookingRoomId, out var roomServices)
                    ? roomServices
                    : Array.Empty<BillServiceItemViewModel>(),
                AdditionalFees = feeLookup.TryGetValue(x.BookingRoomId, out var roomFees)
                    ? roomFees
                    : Array.Empty<BillAdditionalFeeItemViewModel>()
            })
            .ToList();

        return new BillDetailsViewModel
        {
            Id = bill.Id,
            BillCode = bill.BillCode,
            BookingId = bill.BookingId,
            BookingCode = bill.BookingCode,
            TotalAmount = bill.TotalAmount,
            DepositAmount = bill.DepositAmount,
            FinalAmount = bill.FinalAmount,
            PaymentMethod = bill.PaymentMethod,
            ReferenceCode = bill.ReferenceCode,
            DepositPaymentMethod = bill.DepositPaymentMethod,
            DepositReferenceCode = bill.DepositReferenceCode,
            DepositPaymentDate = bill.DepositPaymentDate,
            PaidAt = bill.PaymentDate,
            Status = bill.Status,
            StatusName = GetBillStatusName(bill.Status),
            StatusBadgeClass = GetBillStatusBadgeClass(bill.Status),
            Note = bill.Note,
            CreatedAt = bill.CreatedAt,
            LastModifiedAt = bill.LastModifiedAt,
            BookingExpectedCheckInAt = bill.BookingExpectedCheckInAt,
            BookingExpectedCheckOutAt = bill.BookingExpectedCheckOutAt,
            BookingActualCheckInAt = bill.BookingActualCheckInAt,
            BookingActualCheckOutAt = bill.BookingActualCheckOutAt,
            BookingStatus = bill.BookingStatus,
            BookingStatusName = bill.BookingStatus.HasValue
                ? GetBookingStatusName(bill.BookingStatus.Value)
                : null,
            BookingType = bill.BookingType,
            BookingTypeName = bill.BookingType.HasValue
                ? GetBookingTypeName(bill.BookingType.Value)
                : null,
            GuestCode = bill.GuestCode,
            GuestName = bill.GuestName,
            GuestDateOfBirth = bill.GuestDateOfBirth,
            GuestSex = bill.GuestSex,
            GuestSexName = bill.GuestSex.HasValue
                ? GetGuestSexName(bill.GuestSex.Value)
                : null,
            GuestAddress = bill.GuestAddress,
            GuestPhoneNumber = bill.GuestPhoneNumber,
            GuestEmail = bill.GuestEmail,
            GuestIdentityNumber = bill.GuestIdentityNumber,
            GuestIdentityType = bill.GuestIdentityType,
            GuestIdentityTypeName = bill.GuestIdentityType.HasValue
                ? GetIdentityTypeName(bill.GuestIdentityType.Value)
                : null,
            GuestNote = bill.GuestNote,
            Details = detailRows
        };
    }

    private static BillSortOption NormalizeSort(BillSortOption sort)
    {
        return Enum.IsDefined(typeof(BillSortOption), sort)
            ? sort
            : BillSortOption.LastModifiedDesc;
    }

    private static byte? NormalizeStatus(byte? status)
    {
        return status.HasValue && status.Value <= 1
            ? status
            : null;
    }

    private static IOrderedQueryable<Models.Bill> ApplySort(
        IQueryable<Models.Bill> query,
        BillSortOption sort)
    {
        return sort switch
        {
            BillSortOption.BillCodeAsc =>
                query.OrderBy(x => x.BillCode)
                    .ThenByDescending(x => x.LastModifiedAt),

            BillSortOption.BillCodeDesc =>
                query.OrderByDescending(x => x.BillCode)
                    .ThenByDescending(x => x.LastModifiedAt),

            BillSortOption.TotalAmountAsc =>
                query.OrderBy(x => x.TotalAmount)
                    .ThenBy(x => x.BillCode),

            BillSortOption.TotalAmountDesc =>
                query.OrderByDescending(x => x.TotalAmount)
                    .ThenBy(x => x.BillCode),

            BillSortOption.DepositAmountAsc =>
                query.OrderBy(x => x.DepositAmount)
                    .ThenBy(x => x.BillCode),

            BillSortOption.DepositAmountDesc =>
                query.OrderByDescending(x => x.DepositAmount)
                    .ThenBy(x => x.BillCode),

            BillSortOption.PaidAtDesc =>
                query.OrderByDescending(x => x.PaymentDate)
                    .ThenBy(x => x.BillCode),

            BillSortOption.PaidAtAsc =>
                query.OrderBy(x => x.PaymentDate)
                    .ThenBy(x => x.BillCode),

            BillSortOption.LastModifiedAsc =>
                query.OrderBy(x => x.LastModifiedAt)
                    .ThenBy(x => x.BillCode),

            _ =>
                query.OrderByDescending(x => x.LastModifiedAt)
                    .ThenBy(x => x.BillCode)
        };
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

    private static string GetBillStatusName(byte status)
    {
        return status switch
        {
            0 => "Thanh toán thành công",
            1 => "Thanh toán thất bại",
            _ => "Không xác định"
        };
    }

    private static string GetBillStatusBadgeClass(byte status)
    {
        return status switch
        {
            0 => "text-bg-success",
            1 => "text-bg-danger",
            _ => "text-bg-secondary"
        };
    }

    private static string GetFeeTypeName(byte feeType)
    {
        return feeType switch
        {
            0 => "Mua đồ hộ",
            1 => "Hỏng hóc / mất mát",
            2 => "Khác",
            _ => "Không xác định"
        };
    }

    private static string GetBookingTypeName(byte bookingType)
    {
        return bookingType switch
        {
            0 => "Đặt trước",
            1 => "Trực tiếp tại quầy",
            _ => "Không xác định"
        };
    }

    private static string GetGuestSexName(byte sex)
    {
        return sex switch
        {
            0 => "Nam",
            1 => "Nữ",
            2 => "Khác",
            _ => "Không xác định"
        };
    }

    private static string GetIdentityTypeName(byte identityType)
    {
        return identityType switch
        {
            0 => "CCCD",
            1 => "Giấy phép lái xe",
            2 => "Hộ chiếu",
            _ => "Không xác định"
        };
    }

    private static string GetBookingStatusName(byte status)
    {
        return status switch
        {
            0 => "Chờ xác nhận",
            1 => "Chờ check in",
            2 => "Đang ở",
            3 => "Đã check out",
            4 => "Xác nhận thất bại",
            5 => "Check in thất bại",
            6 => "Check out thất bại",
            7 => "Khách hủy trước check in",
            8 => "Vắng mặt",
            9 => "Khách sạn hủy",
            _ => "Không xác định"
        };
    }
}
