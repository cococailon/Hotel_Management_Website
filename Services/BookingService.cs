using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Text.RegularExpressions;
using Hotel_Management_Website.IServices;
using Hotel_Management_Website.Models.ViewModels.Booking;
using Hotel_Management_Website.Models.ViewModels.UI;
using Microsoft.EntityFrameworkCore;

namespace Hotel_Management_Website.Services;

public sealed class BookingService : IBookingService
{
    private static readonly byte[] ReservedBookingStatuses = { 0, 1, 2 };

    private readonly Models.AppDbContext _context;

    public BookingService(Models.AppDbContext context)
    {
        _context = context;
    }

    public async Task<BookingCreateStep1ViewModel> GetCreateStep1ModelAsync(
        CancellationToken cancellationToken = default)
    {
        var legacy = await GetCreateModelAsync(cancellationToken);
        return MapLegacyToStep1(legacy, null);
    }

    public async Task<BookingCreateStep1ViewModel> RefreshCreateStep1Async(
        BookingCreateStep1ViewModel model,
        CancellationToken cancellationToken = default)
    {
        if (model is null)
        {
            return await GetCreateStep1ModelAsync(cancellationToken);
        }

        var settings = await GetHotelSettingAsync(cancellationToken);
        var normalized = await NormalizeCreateModelAsync(
            MapStep1ToLegacy(model),
            settings,
            cancellationToken);

        normalized.Step = 1;
        normalized.AvailabilitySucceeded = false;
        normalized.AvailabilityMessage = null;

        return MapLegacyToStep1(normalized, model.DraftId);
    }

    public async Task<BookingCreateStep1ValidationResult> ValidateCreateStep1Async(
        BookingCreateStep1ViewModel model,
        CancellationToken cancellationToken = default)
    {
        if (model is null)
        {
            return new BookingCreateStep1ValidationResult
            {
                Succeeded = false,
                ErrorMessage = "Dữ liệu bước 1 không hợp lệ.",
                Model = await GetCreateStep1ModelAsync(cancellationToken)
            };
        }

        var legacy = MapStep1ToLegacy(model);
        var result = await ValidateCreateStepAsync(legacy, cancellationToken);

        return new BookingCreateStep1ValidationResult
        {
            Succeeded = result.Succeeded,
            ErrorMessage = result.ErrorMessage,
            FieldErrors = result.FieldErrors,
            Model = MapLegacyToStep1(result.Model, model.DraftId)
        };
    }

    public async Task<BookingCreateStep2ViewModel> GetCreateStep2ModelAsync(
        BookingCreateStep1ViewModel step1,
        string draftId,
        CancellationToken cancellationToken = default)
    {
        var settings = await GetHotelSettingAsync(cancellationToken);
        var normalized = await NormalizeCreateModelAsync(
            MapStep1ToLegacy(step1),
            settings,
            cancellationToken);

        normalized.Step = 2;
        normalized.AvailabilitySucceeded = step1.AvailabilitySucceeded;
        normalized.AvailabilityMessage = step1.AvailabilityMessage;
        await PopulateCreateReferenceDataAsync(normalized, cancellationToken);

        return MapLegacyToStep2(normalized, draftId);
    }

    public async Task<BookingCreateStep2Result> CreateAsync(
        BookingCreateStep1ViewModel step1,
        BookingCreateStep2ViewModel step2,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        if (step1 is null || step2 is null)
        {
            return new BookingCreateStep2Result
            {
                Succeeded = false,
                ErrorMessage = "Dữ liệu tạo lịch đặt phòng không hợp lệ.",
                Model = step2 ?? new BookingCreateStep2ViewModel()
            };
        }

        var legacy = MergeStep1AndStep2(step1, step2);
        legacy.BookingType = 0;
        var result = await CreateAsync(legacy, currentUserId, cancellationToken);

        return new BookingCreateStep2Result
        {
            Succeeded = result.Succeeded,
            ErrorMessage = result.ErrorMessage,
            FieldErrors = result.FieldErrors,
            Model = MapLegacyToStep2(result.Model, step2.DraftId)
        };
    }

    public async Task<BookingDirectCreateViewModel> GetDirectCreateModelAsync(
        CancellationToken cancellationToken = default)
    {
        var settings = await GetHotelSettingAsync(cancellationToken);
        var now = DateTime.Now;
        var checkoutDate = DateTime.Today.AddDays(1);
        return await BuildDirectCreateModelAsync(
            new BookingDirectCreateViewModel
            {
                CheckOutDate = checkoutDate,
                BookingCode = await GetNextBookingCodeAsync(cancellationToken),
                TotalAdults = 0,
                TotalChildren = 0
            },
            settings,
            now,
            cancellationToken);
    }

    public async Task<BookingDirectCreateViewModel> RefreshDirectCreateModelAsync(
        BookingDirectCreateViewModel model,
        CancellationToken cancellationToken = default)
    {
        model ??= new BookingDirectCreateViewModel();
        var settings = await GetHotelSettingAsync(cancellationToken);
        return await BuildDirectCreateModelAsync(
            model,
            settings,
            DateTime.Now,
            cancellationToken);
    }

    public async Task<BookingDirectCreateResult> CreateDirectAsync(
        BookingDirectCreateViewModel model,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        if (currentUserId == Guid.Empty)
        {
            return new BookingDirectCreateResult
            {
                Succeeded = false,
                ErrorMessage = "Không xác định được người dùng đang đăng nhập.",
                Model = model ?? new BookingDirectCreateViewModel()
            };
        }

        if (model is null)
        {
            return new BookingDirectCreateResult
            {
                Succeeded = false,
                ErrorMessage = "Dữ liệu tạo trực tiếp tại quầy không hợp lệ.",
                Model = await GetDirectCreateModelAsync(cancellationToken)
            };
        }

        var settings = await GetHotelSettingAsync(cancellationToken);
        var now = DateTime.Now;
        var normalized = await BuildDirectCreateModelAsync(model, settings, now, cancellationToken);
        var errors = ValidateDirectCreateModel(normalized, now);
        if (errors.Count == 0)
        {
            await ValidateDirectRoomAssignmentsLiveAsync(normalized, now, cancellationToken, errors);
        }

        if (errors.Count > 0)
        {
            return new BookingDirectCreateResult
            {
                Succeeded = false,
                ErrorMessage = "Vui lòng kiểm tra lại thông tin tạo trực tiếp tại quầy.",
                FieldErrors = errors,
                Model = normalized
            };
        }

        await using var transaction = await _context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        try
        {
            // Tính lại toàn bộ trạng thái phòng/giá/cọc ngay trong transaction.
            var liveNow = DateTime.Now;
            normalized = await BuildDirectCreateModelAsync(model, settings, liveNow, cancellationToken);
            errors = ValidateDirectCreateModel(normalized, liveNow);
            if (errors.Count == 0)
            {
                await ValidateDirectRoomAssignmentsLiveAsync(normalized, liveNow, cancellationToken, errors);
            }
            if (errors.Count > 0)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                return new BookingDirectCreateResult
                {
                    Succeeded = false,
                    ErrorMessage = "Dữ liệu phòng hoặc thông tin tạo trực tiếp đã thay đổi. Vui lòng kiểm tra lại.",
                    FieldErrors = errors,
                    Model = normalized
                };
            }

            var bookingCode = normalized.BookingCode.Trim();
            if (await _context.Bookings.AnyAsync(x => x.BookingCode == bookingCode, cancellationToken))
            {
                errors[nameof(BookingDirectCreateViewModel.BookingCode)] = "Mã đặt phòng đã tồn tại.";
                await transaction.RollbackAsync(CancellationToken.None);
                return new BookingDirectCreateResult
                {
                    Succeeded = false,
                    ErrorMessage = "Mã đặt phòng đã tồn tại. Vui lòng dùng mã khác.",
                    FieldErrors = errors,
                    Model = normalized
                };
            }

            var guestCode = normalized.GuestCode.Trim();
            if (await _context.Guests.AnyAsync(
                    x => !x.IsDeleted && x.GuestCode == guestCode,
                    cancellationToken))
            {
                errors[nameof(BookingDirectCreateViewModel.GuestCode)] = "Mã khách hàng đã tồn tại.";
                await transaction.RollbackAsync(CancellationToken.None);
                return new BookingDirectCreateResult
                {
                    Succeeded = false,
                    ErrorMessage = "Mã khách hàng đã tồn tại.",
                    FieldErrors = errors,
                    Model = normalized
                };
            }

            // Quy ước hiện tại: mỗi Booking mới luôn tạo một Guest mới.
            var guest = new Models.Guest
            {
                Id = Guid.NewGuid(),
                GuestCode = guestCode,
                Name = normalized.GuestName.Trim(),
                DateOfBirth = normalized.GuestDateOfBirth!.Value.Date,
                Sex = normalized.GuestSex,
                Address = normalized.GuestAddress.Trim(),
                PhoneNumber = NormalizePhoneNumberForStorage(normalized.GuestPhoneNumber),
                Email = NormalizeOptional(normalized.GuestEmail),
                IdentityNumber = normalized.GuestIdentityNumber.Trim(),
                IdentityType = normalized.GuestIdentityType,
                Note = NormalizeOptional(normalized.GuestNote),
                IsDeleted = false,
                CreatedBy = currentUserId,
                CreatedAt = now,
                LastModifiedBy = currentUserId,
                LastModifiedAt = now
            };
            await _context.Guests.AddAsync(guest, cancellationToken);

            var booking = new Models.Booking
            {
                Id = Guid.NewGuid(),
                BookingCode = bookingCode,
                GuestId = guest.Id,
                BookingType = 1,
                ExpectedCheckInAt = DateTime.Today.Date.Add(settings.CheckInTime.ToTimeSpan()),
                ExpectedCheckOutAt = normalized.ExpectedCheckOutAt,
                ActualCheckInAt = now,
                ActualCheckOutAt = null,
                TotalAdults = normalized.TotalAdults,
                TotalChildren = normalized.TotalChildren,
                DepositAmount = normalized.DepositAmount,
                PaymentMethod = normalized.DepositAmount > 0 ? normalized.PaymentMethod : null,
                PaymentDate = normalized.DepositAmount > 0 ? normalized.PaymentDate : null,
                ReferenceCode = normalized.DepositAmount > 0 && normalized.PaymentMethod == 1
                    ? NormalizeOptional(normalized.ReferenceCode)
                    : null,
                Status = 2,
                Note = NormalizeOptional(normalized.BookingNote),
                IsDeleted = false,
                CreatedBy = currentUserId,
                CreatedAt = now,
                LastModifiedBy = currentUserId,
                LastModifiedAt = now
            };

            await _context.Bookings.AddAsync(booking, cancellationToken);

            var roomIds = normalized.RoomAssignments
                .Where(x => x.RoomId.HasValue)
                .Select(x => x.RoomId!.Value)
                .Distinct()
                .ToArray();

            var rooms = await _context.Rooms
                .Where(x => roomIds.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id, cancellationToken);

            foreach (var assignment in normalized.RoomAssignments)
            {
                var room = rooms[assignment.RoomId!.Value];
                room.Status = 1;
                room.LastModifiedBy = currentUserId;
                room.LastModifiedAt = now;

                await _context.BookingRooms.AddAsync(new Models.BookingRoom
                {
                    Id = Guid.NewGuid(),
                    BookingId = booking.Id,
                    RoomTypeId = assignment.RoomTypeId,
                    RoomId = room.Id,
                    Adults = assignment.Adults,
                    Children = assignment.Children,
                    Note = NormalizeOptional(assignment.Note),
                    IsDeleted = false,
                    CreatedBy = currentUserId,
                    CreatedAt = now,
                    LastModifiedBy = currentUserId,
                    LastModifiedAt = now
                }, cancellationToken);
            }

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            normalized.BookingCode = booking.BookingCode;
            return new BookingDirectCreateResult
            {
                Succeeded = true,
                BookingId = booking.Id,
                Model = normalized
            };
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            return new BookingDirectCreateResult
            {
                Succeeded = false,
                ErrorMessage = "Không thể lưu lịch trực tiếp tại quầy. Dữ liệu có thể đã thay đổi, vui lòng thử lại.",
                Model = normalized
            };
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            return new BookingDirectCreateResult
            {
                Succeeded = false,
                ErrorMessage = "Không thể tạo lịch trực tiếp tại quầy. Vui lòng thử lại.",
                Model = normalized
            };
        }
    }

    public async Task<BookingIndexViewModel> GetIndexAsync(
        BookingListFilterViewModel filter,
        CancellationToken cancellationToken = default)
    {
        filter ??= new BookingListFilterViewModel();

        var normalizedSearch = filter.Search?.Trim() ?? string.Empty;
        var normalizedSort = NormalizeSort(filter.Sort);
        var normalizedStatus = NormalizeStatus(filter.Status);
        var pageSize = NormalizePageSize(filter.PageSize);
        var requestedPage = Math.Max(filter.CurrentPage, 1);

        var query = _context.Bookings
            .AsNoTracking()
            .Where(x => !x.IsDeleted);

        if (!string.IsNullOrWhiteSpace(normalizedSearch))
        {
            query = query.Where(x =>
                x.BookingCode.Contains(normalizedSearch) ||
                (x.Guest != null && x.Guest.GuestCode.Contains(normalizedSearch)));
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

        var bookingRows = await ApplySort(query, normalizedSort)
            .Skip((currentPage - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new
            {
                x.Id,
                x.BookingCode,
                x.BookingType,
                GuestCode = x.Guest != null ? x.Guest.GuestCode : string.Empty,
                GuestName = x.Guest != null ? x.Guest.Name : string.Empty,
                x.ExpectedCheckInAt,
                x.ExpectedCheckOutAt,
                x.TotalAdults,
                x.TotalChildren,
                x.Status
            })
            .ToListAsync(cancellationToken);

        var bookings = bookingRows
            .Select(x => new BookingListItemViewModel
            {
                Id = x.Id,
                BookingCode = x.BookingCode,
                BookingType = (byte)x.BookingType,
                BookingTypeName = GetBookingTypeName((byte)x.BookingType),
                BookingTypeBadgeClass = GetBookingTypeBadgeClass((byte)x.BookingType),
                GuestCode = x.GuestCode,
                GuestName = x.GuestName,
                ExpectedCheckInAt = x.ExpectedCheckInAt,
                ExpectedCheckOutAt = x.ExpectedCheckOutAt,
                TotalAdults = x.TotalAdults,
                TotalChildren = x.TotalChildren,
                Status = x.Status,
                StatusName = GetBookingStatusName(x.Status),
                StatusBadgeClass = GetBookingStatusBadgeClass(x.Status)
            })
            .ToList();

        return new BookingIndexViewModel
        {
            Filter = new BookingListFilterViewModel
            {
                Search = normalizedSearch,
                Sort = normalizedSort,
                Status = normalizedStatus,
                CurrentPage = currentPage,
                PageSize = pageSize
            },
            Bookings = bookings,
            TotalCount = totalCount,
            Pagination = new PaginationViewModel
            {
                CurrentPage = currentPage,
                PageSize = pageSize,
                TotalItems = totalCount,
                PageParam = "Filter.CurrentPage",
                PageSizeParam = "Filter.PageSize",
                BaseUrl = "/Booking/Index",
                ItemLabel = "đặt phòng"
            }
        };
    }

    public async Task<BookingDetailsViewModel?> GetDetailsAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
        {
            return null;
        }

        var booking = await _context.Bookings
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.Id == id && !x.IsDeleted,
                cancellationToken);

        if (booking is null)
        {
            return null;
        }

        var guest = booking.GuestId.HasValue
            ? await _context.Guests
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x => x.Id == booking.GuestId.Value && !x.IsDeleted,
                    cancellationToken)
            : null;

        var bookingRooms = await _context.BookingRooms
            .AsNoTracking()
            .Where(x => x.BookingId == booking.Id && !x.IsDeleted)
            .OrderBy(x => x.CreatedAt)
            .Select(x => new BookingRoomDetailsViewModel
            {
                Id = x.Id,
                RoomTypeCode = x.RoomType != null ? x.RoomType.RoomTypeCode : string.Empty,
                RoomTypeName = x.RoomType != null ? x.RoomType.RoomTypeName : string.Empty,
                RoomCode = x.Room != null ? x.Room.RoomCode : string.Empty,
                RoomName = x.Room != null ? x.Room.RoomName : string.Empty,
                Adults = x.Adults,
                Children = x.Children,
                Note = x.Note,
                CreatedAt = x.CreatedAt,
                LastModifiedAt = x.LastModifiedAt
            })
            .ToListAsync(cancellationToken);

        var roomIds = bookingRooms.Select(x => x.Id).ToArray();

        if (roomIds.Length > 0)
        {
            var services = await _context.BookingRoomServices
                .AsNoTracking()
                .Where(x => roomIds.Contains(x.BookingRoomId) && !x.IsDeleted)
                .OrderBy(x => x.UsedAt)
                .Select(x => new BookingRoomServiceDetailsViewModel
                {
                    Id = x.Id,
                    BookingRoomId = x.BookingRoomId,
                    ServiceCode = x.Service != null ? x.Service.ServiceCode : string.Empty,
                    ServiceName = x.Service != null ? x.Service.ServiceName : string.Empty,
                    TotalPrice = x.TotalPrice,
                    UsedAt = x.UsedAt,
                    Note = x.Note
                })
                .ToListAsync(cancellationToken);

            var fees = await _context.AdditionalFeeRooms
                .AsNoTracking()
                .Where(x => roomIds.Contains(x.BookingRoomId) && !x.IsDeleted)
                .OrderBy(x => x.CreatedAt)
                .Select(x => new AdditionalFeeRoomDetailsViewModel
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
                    x => (IReadOnlyList<BookingRoomServiceDetailsViewModel>)x.ToList());

            var feeLookup = fees
                .GroupBy(x => x.BookingRoomId)
                .ToDictionary(
                    x => x.Key,
                    x => (IReadOnlyList<AdditionalFeeRoomDetailsViewModel>)x.ToList());

            bookingRooms = bookingRooms
                .Select(room => new BookingRoomDetailsViewModel
                {
                    Id = room.Id,
                    RoomTypeCode = room.RoomTypeCode,
                    RoomTypeName = room.RoomTypeName,
                    RoomCode = room.RoomCode,
                    RoomName = room.RoomName,
                    Adults = room.Adults,
                    Children = room.Children,
                    Note = room.Note,
                    CreatedAt = room.CreatedAt,
                    LastModifiedAt = room.LastModifiedAt,
                    Services = serviceLookup.TryGetValue(room.Id, out var roomServices)
                        ? roomServices
                        : Array.Empty<BookingRoomServiceDetailsViewModel>(),
                    AdditionalFees = feeLookup.TryGetValue(room.Id, out var roomFees)
                        ? roomFees
                        : Array.Empty<AdditionalFeeRoomDetailsViewModel>()
                })
                .ToList();
        }

        BillDetailsViewModel? bill = null;

        // Hiển thị Bill khi Booking đã có Bill, bao gồm cả checkout thành công (3)
        // và checkout thất bại (6).
        var billEntity = await _context.Bills
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x => x.BookingId == booking.Id && !x.IsDeleted,
                    cancellationToken);

            if (billEntity is not null)
            {
                var billDetails = await _context.BillDetails
                    .AsNoTracking()
                    .Where(x => x.BillId == billEntity.Id && !x.IsDeleted)
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
                        TotalPrice = x.TotalPrice,
                        Note = x.Note,
                        CreatedAt = x.CreatedAt,
                        LastModifiedAt = x.LastModifiedAt
                    })
                    .ToListAsync(cancellationToken);

                bill = new BillDetailsViewModel
                {
                    Id = billEntity.Id,
                    BillCode = billEntity.BillCode,
                    TotalAmount = billEntity.TotalAmount,
                    DepositAmount = billEntity.DepositAmount,
                    FinalAmount = billEntity.FinalAmount,
                    PaidAt = billEntity.PaymentDate,
                    Status = billEntity.Status,
                    StatusName = GetBillStatusName(billEntity.Status),
                    StatusBadgeClass = GetBillStatusBadgeClass(billEntity.Status),
                    Note = billEntity.Note,
                    CreatedAt = billEntity.CreatedAt,
                    LastModifiedAt = billEntity.LastModifiedAt,
                    Details = billDetails
                };
        }

        return new BookingDetailsViewModel
        {
            Id = booking.Id,
            BookingCode = booking.BookingCode,
            BookingType = (byte)booking.BookingType,
            BookingTypeName = GetBookingTypeName((byte)booking.BookingType),
            BookingTypeBadgeClass = GetBookingTypeBadgeClass((byte)booking.BookingType),
            Guest = new BookingGuestDetailsViewModel
            {
                GuestCode = guest?.GuestCode ?? string.Empty,
                GuestName = guest?.Name ?? string.Empty,
                Age = CalculateAge(guest?.DateOfBirth),
                Sex = guest?.Sex,
                SexName = GetSexName(guest?.Sex)
            },
            ExpectedCheckInAt = booking.ExpectedCheckInAt,
            ExpectedCheckOutAt = booking.ExpectedCheckOutAt,
            ActualCheckInAt = booking.ActualCheckInAt,
            ActualCheckOutAt = booking.ActualCheckOutAt,
            TotalAdults = booking.TotalAdults,
            TotalChildren = booking.TotalChildren,
            DepositAmount = booking.DepositAmount,
            PaymentMethod = booking.PaymentMethod,
            PaymentMethodName = GetPaymentMethodName(booking.PaymentMethod),
            PaymentDate = booking.PaymentDate,
            ReferenceCode = booking.ReferenceCode,
            Status = booking.Status,
            StatusName = GetBookingStatusName(booking.Status),
            StatusBadgeClass = GetBookingStatusBadgeClass(booking.Status),
            Note = booking.Note,
            CreatedAt = booking.CreatedAt,
            LastModifiedAt = booking.LastModifiedAt,
            BookingRooms = bookingRooms,
            Bill = bill
        };
    }

    public async Task<BookingEditViewModel?> GetEditModelAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
        {
            return null;
        }

        var booking = await _context.Bookings
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken);

        if (booking is null)
        {
            return null;
        }

        var guest = booking.GuestId.HasValue
            ? await _context.Guests
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x => x.Id == booking.GuestId.Value && !x.IsDeleted,
                    cancellationToken)
            : null;

        var bookingRooms = await _context.BookingRooms
            .AsNoTracking()
            .Where(x => x.BookingId == id && !x.IsDeleted)
            .Include(x => x.RoomType)
            .Include(x => x.Room)
            .OrderBy(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        var roomIds = bookingRooms.Select(x => x.Id).ToArray();

        var services = roomIds.Length == 0
            ? new List<Models.BookingRoomService>()
            : await _context.BookingRoomServices
                .AsNoTracking()
                .Where(x => roomIds.Contains(x.BookingRoomId) && !x.IsDeleted)
                .Include(x => x.Service)
                .OrderBy(x => x.UsedAt)
                .ThenBy(x => x.CreatedAt)
                .ToListAsync(cancellationToken);

        var fees = roomIds.Length == 0
            ? new List<Models.AdditionalFeeRoom>()
            : await _context.AdditionalFeeRooms
                .AsNoTracking()
                .Where(x => roomIds.Contains(x.BookingRoomId) && !x.IsDeleted)
                .OrderBy(x => x.CreatedAt)
                .ToListAsync(cancellationToken);

        var canEditPeople = booking.Status is 0 or 1;
        var canEditServices = booking.Status is 1 or 2;

        var serviceOptions = canEditServices
            ? await _context.Services
                .AsNoTracking()
                .Where(x => !x.IsDeleted)
                .OrderBy(x => x.ServiceCode)
                .Select(x => new BookingEditServiceOptionViewModel
                {
                    Id = x.Id,
                    ServiceCode = x.ServiceCode,
                    ServiceName = x.ServiceName,
                    UnitPrice = x.UnitPrice ?? 0m,
                    PriceUnitType = x.PriceUnitType,
                    IsActive = x.IsActive
                })
                .ToListAsync(cancellationToken)
            : new List<BookingEditServiceOptionViewModel>();

        var serviceLookup = services
            .GroupBy(x => x.BookingRoomId)
            .ToDictionary(x => x.Key, x => x.ToList());

        var feeLookup = fees
            .GroupBy(x => x.BookingRoomId)
            .ToDictionary(x => x.Key, x => x.ToList());

        return new BookingEditViewModel
        {
            BookingId = booking.Id,
            BookingCode = booking.BookingCode,
            BookingType = booking.BookingType,
            BookingTypeName = GetBookingTypeName(booking.BookingType),
            BookingTypeBadgeClass = GetBookingTypeBadgeClass(booking.BookingType),
            Status = booking.Status,
            StatusName = GetBookingStatusName(booking.Status),
            StatusBadgeClass = GetBookingStatusBadgeClass(booking.Status),
            ExpectedCheckInAt = booking.ExpectedCheckInAt,
            ExpectedCheckOutAt = booking.ExpectedCheckOutAt,
            ActualCheckInAt = booking.ActualCheckInAt,
            ActualCheckOutAt = booking.ActualCheckOutAt,
            TotalAdults = booking.TotalAdults,
            TotalChildren = booking.TotalChildren,
            Note = booking.Note,
            CanEditPeople = canEditPeople,
            CanEditServices = canEditServices,
            CanEditGuestDetails = booking.Status is 0 or 1 or 2,
            RequireFullGuestDetails = booking.Status == 2,
            Guest = new BookingEditGuestViewModel
            {
                GuestCode = guest?.GuestCode ?? string.Empty,
                Name = guest?.Name ?? string.Empty,
                DateOfBirth = guest?.DateOfBirth,
                Sex = guest?.Sex,
                SexName = GetSexName(guest?.Sex),
                Address = guest?.Address,
                PhoneNumber = guest?.PhoneNumber,
                Email = guest?.Email,
                IdentityNumber = guest?.IdentityNumber,
                IdentityType = guest?.IdentityType,
                IdentityTypeName = GetIdentityTypeName(guest?.IdentityType),
                Note = guest?.Note
            },
            Services = serviceOptions,
            Rooms = bookingRooms.Select(room => new BookingEditRoomViewModel
            {
                BookingRoomId = room.Id,
                RoomTypeCode = room.RoomType?.RoomTypeCode ?? string.Empty,
                RoomTypeName = room.RoomType?.RoomTypeName ?? string.Empty,
                RoomCode = room.Room?.RoomCode ?? string.Empty,
                RoomName = room.Room?.RoomName ?? string.Empty,
                Note = room.Note,
                ServiceInputs = serviceLookup.TryGetValue(room.Id, out var roomServices)
                    ? roomServices.Select(service => new BookingEditServiceItemViewModel
                    {
                        Id = service.Id,
                        ServiceId = service.ServiceId ?? Guid.Empty,
                        UsedAt = service.UsedAt,
                        TotalPrice = service.TotalPrice,
                        Note = service.Note,
                        IsDeleted = false
                    }).ToList()
                    : new List<BookingEditServiceItemViewModel>(),
                FeeInputs = feeLookup.TryGetValue(room.Id, out var roomFees)
                    ? roomFees.Select(fee => new BookingEditFeeItemViewModel
                    {
                        Id = fee.Id,
                        FeeName = fee.FeeName,
                        FeeType = fee.FeeType,
                        TotalFee = fee.TotalFee,
                        Description = fee.Decription,
                        IsDeleted = false
                    }).ToList()
                    : new List<BookingEditFeeItemViewModel>()
            }).ToList()
        };
    }

    public async Task<BookingEditResult> UpdateEditAsync(
        BookingEditViewModel model,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        if (currentUserId == Guid.Empty)
        {
            return new BookingEditResult
            {
                Succeeded = false,
                ErrorMessage = "Không xác định được người dùng đang đăng nhập.",
                Model = model ?? new BookingEditViewModel()
            };
        }

        if (model is null || model.BookingId == Guid.Empty)
        {
            return new BookingEditResult
            {
                Succeeded = false,
                ErrorMessage = "Dữ liệu chỉnh sửa lịch đặt phòng không hợp lệ.",
                Model = model ?? new BookingEditViewModel()
            };
        }

        var fieldErrors = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var now = DateTime.Now;

        await using var transaction = await _context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        try
        {
            var booking = await _context.Bookings
                .FirstOrDefaultAsync(
                    x => x.Id == model.BookingId && !x.IsDeleted,
                    cancellationToken);

            if (booking is null)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                return new BookingEditResult
                {
                    Succeeded = false,
                    ErrorMessage = "Lịch đặt phòng không tồn tại hoặc đã bị xóa.",
                    Model = model
                };
            }

            if (booking.Status > 9)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                return new BookingEditResult
                {
                    Succeeded = false,
                    ErrorMessage = "Trạng thái lịch đặt phòng không hợp lệ.",
                    Model = model
                };
            }

            var bookingRooms = await _context.BookingRooms
                .Include(x => x.RoomType)
                .Where(x => x.BookingId == booking.Id && !x.IsDeleted)
                .OrderBy(x => x.CreatedAt)
                .ToListAsync(cancellationToken);

            if (bookingRooms.Count == 0)
            {
                fieldErrors[string.Empty] = "Lịch đặt phòng không có BookingRoom hợp lệ để chỉnh sửa.";
            }

            if (model.Rooms is null)
            {
                fieldErrors[string.Empty] = "Danh sách BookingRoom không hợp lệ.";
            }
            else
            {
                var expectedRoomIds = bookingRooms.Select(x => x.Id).ToHashSet();
                var postedRoomIds = model.Rooms
                    .Where(x => x is not null)
                    .Select(x => x.BookingRoomId)
                    .ToList();

                if (postedRoomIds.Count != expectedRoomIds.Count
                    || postedRoomIds.Distinct().Count() != postedRoomIds.Count
                    || !expectedRoomIds.SetEquals(postedRoomIds))
                {
                    fieldErrors[string.Empty] = "Danh sách BookingRoom không còn đồng bộ với dữ liệu hiện tại. Vui lòng tải lại màn hình và thử lại.";
                }
            }

            var canEditPeople = booking.Status is 0 or 1;
            var canEditServices = booking.Status is 1 or 2;

            if (canEditPeople)
            {
                if (model.TotalAdults < 1)
                {
                    fieldErrors[nameof(BookingEditViewModel.TotalAdults)] = "Tổng người lớn phải lớn hơn 0.";
                }

                if (model.TotalChildren < 0)
                {
                    fieldErrors[nameof(BookingEditViewModel.TotalChildren)] = "Tổng trẻ em phải từ 0 trở lên.";
                }

                if (bookingRooms.Count > 0 && model.TotalAdults >= 0 && model.TotalChildren >= 0)
                {
                    var maxAdults = bookingRooms.Sum(x => (long)Math.Max(0, x.RoomType?.MaxAdults ?? 0));
                    var maxChildren = bookingRooms.Sum(x => (long)Math.Max(0, x.RoomType?.MaxChildren ?? 0));

                    if (model.TotalAdults > maxAdults)
                    {
                        fieldErrors[nameof(BookingEditViewModel.TotalAdults)] =
                            $"Tổng người lớn vượt sức chứa của các BookingRoom đã đặt ({maxAdults} người lớn tối đa).";
                    }

                    if (model.TotalChildren > maxChildren)
                    {
                        fieldErrors[nameof(BookingEditViewModel.TotalChildren)] =
                            $"Tổng trẻ em vượt sức chứa của các BookingRoom đã đặt ({maxChildren} trẻ em tối đa).";
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(model.Note)
                && model.Note.Trim().Length > 255)
            {
                fieldErrors[nameof(BookingEditViewModel.Note)] = "Ghi chú Booking không được vượt quá 255 ký tự.";
            }

            var canEditGuestDetails = booking.Status is 0 or 1 or 2;
            var requireFullGuestDetails = booking.Status == 2;
            var guest = booking.GuestId.HasValue
                ? await _context.Guests.FirstOrDefaultAsync(
                    x => x.Id == booking.GuestId.Value && !x.IsDeleted,
                    cancellationToken)
                : null;

            if (guest is null)
            {
                if (canEditGuestDetails)
                {
                    fieldErrors["Guest"] = "Booking hiện không có khách hàng hợp lệ để chỉnh sửa.";
                }
            }
            else if (canEditGuestDetails)
            {
                ValidateEditGuestFields(
                    model.Guest,
                    requireFullGuestDetails,
                    fieldErrors);

                if (fieldErrors.Count == 0)
                {
                    // Validation bất đồng bộ (kiểm tra mã khách hàng trùng) thực hiện ngay dưới đây.
                    var guestCode = model.Guest?.GuestCode?.Trim() ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(guestCode))
                    {
                        var duplicateGuestCode = await _context.Guests
                            .AnyAsync(
                                x => !x.IsDeleted
                                     && x.Id != guest.Id
                                     && x.GuestCode == guestCode,
                                cancellationToken);

                        if (duplicateGuestCode)
                        {
                            fieldErrors["Guest.GuestCode"] = "Mã khách hàng đã tồn tại.";
                        }
                    }
                }
            }

            if (fieldErrors.Count == 0 && guest is not null)
            {
                // Không bao giờ để client quyết định phạm vi sửa.
                // Status hiện tại của Booking mới là nguồn sự thật.
                if (canEditGuestDetails)
                {
                    ApplyEditGuestFields(guest, model.Guest, currentUserId, now);
                }
                else
                {
                    guest.Note = NormalizeOptional(model.Guest?.Note);
                    guest.LastModifiedBy = currentUserId;
                    guest.LastModifiedAt = now;
                }
            }

            var roomById = bookingRooms.ToDictionary(x => x.Id);
            var roomModels = model.Rooms ?? new List<BookingEditRoomViewModel>();

            for (var roomIndex = 0; roomIndex < roomModels.Count; roomIndex++)
            {
                var roomModel = roomModels[roomIndex];
                if (roomModel is null || !roomById.TryGetValue(roomModel.BookingRoomId, out var bookingRoom))
                {
                    fieldErrors[$"Rooms[{roomIndex}].BookingRoomId"] = "BookingRoom không hợp lệ.";
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(roomModel.Note)
                    && roomModel.Note.Trim().Length > 255)
                {
                    fieldErrors[$"Rooms[{roomIndex}].Note"] = "Ghi chú BookingRoom không được vượt quá 255 ký tự.";
                }

                if (!canEditServices)
                {
                    continue;
                }

                var serviceItems = roomModel.ServiceInputs ?? new List<BookingEditServiceItemViewModel>();
                var feeItems = roomModel.FeeInputs ?? new List<BookingEditFeeItemViewModel>();

                await ValidateEditServicesAsync(
                    roomIndex,
                    booking,
                    bookingRoom,
                    serviceItems,
                    fieldErrors,
                    now,
                    cancellationToken);

                await ValidateEditFeesAsync(
                    roomIndex,
                    bookingRoom,
                    feeItems,
                    fieldErrors,
                    cancellationToken);
            }

            if (fieldErrors.Count > 0)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                return new BookingEditResult
                {
                    Succeeded = false,
                    ErrorMessage = "Vui lòng kiểm tra lại các thông tin được đánh dấu.",
                    FieldErrors = fieldErrors,
                    Model = model
                };
            }

            if (canEditPeople)
            {
                if (booking.TotalAdults != model.TotalAdults)
                {
                    booking.TotalAdults = model.TotalAdults;
                }

                if (booking.TotalChildren != model.TotalChildren)
                {
                    booking.TotalChildren = model.TotalChildren;
                }
            }

            var normalizedBookingNote = NormalizeOptional(model.Note);
            if (!string.Equals(booking.Note, normalizedBookingNote, StringComparison.Ordinal))
            {
                booking.Note = normalizedBookingNote;
            }

            foreach (var roomModel in roomModels)
            {
                if (!roomById.TryGetValue(roomModel.BookingRoomId, out var bookingRoom))
                    continue;

                var normalizedRoomNote = NormalizeOptional(roomModel.Note);
                if (!string.Equals(bookingRoom.Note, normalizedRoomNote, StringComparison.Ordinal))
                {
                    bookingRoom.Note = normalizedRoomNote;
                    bookingRoom.LastModifiedBy = currentUserId;
                    bookingRoom.LastModifiedAt = now;
                }

                if (!canEditServices)
                    continue;

                var activeServiceEntities = await _context.BookingRoomServices
                    .Where(x => x.BookingRoomId == bookingRoom.Id)
                    .ToListAsync(cancellationToken);

                var activeFeeEntities = await _context.AdditionalFeeRooms
                    .Where(x => x.BookingRoomId == bookingRoom.Id)
                    .ToListAsync(cancellationToken);

                ApplyEditServices(
                    roomModel.ServiceInputs ?? new List<BookingEditServiceItemViewModel>(),
                    activeServiceEntities,
                    bookingRoom.Id,
                    currentUserId,
                    now);

                ApplyEditFees(
                    roomModel.FeeInputs ?? new List<BookingEditFeeItemViewModel>(),
                    activeFeeEntities,
                    bookingRoom.Id,
                    currentUserId,
                    now);
            }

            // Một lần chỉnh sửa Booking có thể chỉ thay đổi BookingRoom/Service/Fee.
            // Vẫn cập nhật audit của Booking để LastModified phản ánh lần chỉnh sửa thực tế.
            booking.LastModifiedBy = currentUserId;
            booking.LastModifiedAt = now;

            model.BookingCode = booking.BookingCode;
            model.Status = booking.Status;
            model.StatusName = GetBookingStatusName(booking.Status);
            model.StatusBadgeClass = GetBookingStatusBadgeClass(booking.Status);

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return new BookingEditResult
            {
                Succeeded = true,
                Model = model
            };
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            return new BookingEditResult
            {
                Succeeded = false,
                ErrorMessage = "Không thể lưu thay đổi lịch đặt phòng. Dữ liệu có thể đã thay đổi, vui lòng tải lại và thử lại.",
                Model = model
            };
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            return new BookingEditResult
            {
                Succeeded = false,
                ErrorMessage = "Không thể cập nhật lịch đặt phòng. Vui lòng thử lại.",
                Model = model
            };
        }
    }

    public async Task<BookingFlowViewModel?> GetFlowAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
        {
            return null;
        }

        var booking = await _context.Bookings
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken);

        if (booking is null)
        {
            return null;
        }

        var guest = booking.GuestId.HasValue
            ? await _context.Guests.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == booking.GuestId.Value && !x.IsDeleted, cancellationToken)
            : null;

        var rooms = await _context.BookingRooms
            .AsNoTracking()
            .Where(x => x.BookingId == id && !x.IsDeleted)
            .OrderBy(x => x.CreatedAt)
            .Select(x => new BookingFlowRoomViewModel
            {
                BookingRoomId = x.Id,
                RoomTypeCode = x.RoomType != null ? x.RoomType.RoomTypeCode : string.Empty,
                RoomTypeName = x.RoomType != null ? x.RoomType.RoomTypeName : string.Empty,
                RoomCode = x.Room != null ? x.Room.RoomCode : string.Empty,
                RoomName = x.Room != null ? x.Room.RoomName : string.Empty,
                Adults = x.Adults,
                Children = x.Children,
                Note = x.Note
            })
            .ToListAsync(cancellationToken);

        return new BookingFlowViewModel
        {
            BookingId = booking.Id,
            BookingCode = booking.BookingCode,
            BookingType = (byte)booking.BookingType,
            BookingTypeName = GetBookingTypeName((byte)booking.BookingType),
            BookingTypeBadgeClass = GetBookingTypeBadgeClass((byte)booking.BookingType),
            Status = booking.Status,
            StatusName = GetBookingStatusName(booking.Status),
            StatusBadgeClass = GetBookingStatusBadgeClass(booking.Status),
            ExpectedCheckInAt = booking.ExpectedCheckInAt,
            ExpectedCheckOutAt = booking.ExpectedCheckOutAt,
            ActualCheckInAt = booking.ActualCheckInAt,
            ActualCheckOutAt = booking.ActualCheckOutAt,
            TotalAdults = booking.TotalAdults,
            TotalChildren = booking.TotalChildren,
            DepositAmount = booking.DepositAmount,
            BookingNote = booking.Note,
            GuestCode = guest?.GuestCode ?? string.Empty,
            GuestName = guest?.Name ?? string.Empty,
            GuestDateOfBirth = guest?.DateOfBirth,
            GuestSex = guest?.Sex,
            GuestSexName = GetSexName(guest?.Sex),
            GuestAddress = guest?.Address,
            GuestPhoneNumber = guest?.PhoneNumber,
            GuestEmail = guest?.Email,
            GuestIdentityNumber = guest?.IdentityNumber,
            GuestIdentityType = guest?.IdentityType,
            GuestIdentityTypeName = GetIdentityTypeName(guest?.IdentityType),
            GuestNote = guest?.Note,
            Rooms = rooms
        };
    }

    public async Task<BookingCheckInViewModel?> GetCheckInModelAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var booking = await _context.Bookings
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken);

        if (booking is null || booking.Status != 1)
        {
            return null;
        }

        var guest = booking.GuestId.HasValue
            ? await _context.Guests.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == booking.GuestId.Value && !x.IsDeleted, cancellationToken)
            : null;

        var guestOptions = await _context.Guests
            .AsNoTracking()
            .Where(x => !x.IsDeleted)
            .OrderBy(x => x.Name)
            .ThenBy(x => x.GuestCode)
            .Select(x => new GuestOptionViewModel
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
                Note = x.Note
            })
            .ToListAsync(cancellationToken);

        var bookingRooms = await _context.BookingRooms
            .AsNoTracking()
            .Where(x => x.BookingId == id && !x.IsDeleted)
            .OrderBy(x => x.RoomType != null ? x.RoomType.RoomTypeCode : string.Empty)
            .ThenBy(x => x.CreatedAt)
            .Select(x => new
            {
                x.Id,
                x.RoomTypeId,
                RoomTypeCode = x.RoomType != null ? x.RoomType.RoomTypeCode : string.Empty,
                RoomTypeName = x.RoomType != null ? x.RoomType.RoomTypeName : string.Empty,
                x.Adults,
                x.Children,
                x.Note
            })
            .ToListAsync(cancellationToken);

        var roomTypeIds = bookingRooms.Select(x => x.RoomTypeId).Distinct().ToArray();
        var rooms = roomTypeIds.Length == 0
            ? new List<CheckInRoomOptionViewModel>()
            : await _context.Rooms
                .AsNoTracking()
                .Where(x =>
                    roomTypeIds.Contains(x.RoomTypeId) &&
                    x.IsActive &&
                    !x.IsDeleted &&
                    x.Status == 0)
                .OrderBy(x => x.RoomCode)
                .Select(x => new CheckInRoomOptionViewModel
                {
                    Id = x.Id,
                    RoomTypeId = x.RoomTypeId,
                    RoomCode = x.RoomCode,
                    RoomName = x.RoomName,
                    RoomTypeCode = x.RoomType != null ? x.RoomType.RoomTypeCode : string.Empty,
                    RoomTypeName = x.RoomType != null ? x.RoomType.RoomTypeName : string.Empty,
                    Status = x.Status
                })
                .ToListAsync(cancellationToken);

        var autoGeneratedGuestCode = await GetNextGuestCodeAsync(cancellationToken);

        return new BookingCheckInViewModel
        {
            BookingId = booking.Id,
            GuestId = guest?.Id,
            CreateNewGuest = guest is null,
            GuestCode = guest?.GuestCode ?? autoGeneratedGuestCode,
            AutoGeneratedGuestCode = autoGeneratedGuestCode,
            Guests = guestOptions,
            GuestName = guest?.Name ?? string.Empty,
            GuestDateOfBirth = guest?.DateOfBirth,
            GuestSex = guest?.Sex,
            GuestAddress = guest?.Address ?? string.Empty,
            GuestPhoneNumber = guest?.PhoneNumber ?? string.Empty,
            GuestEmail = guest?.Email,
            GuestIdentityNumber = guest?.IdentityNumber ?? string.Empty,
            GuestIdentityType = guest?.IdentityType,
            GuestNote = guest?.Note,
            TotalAdults = booking.TotalAdults,
            TotalChildren = booking.TotalChildren,
            BookingNote = booking.Note,
            Rooms = bookingRooms.Select(x =>
            {
                return new BookingCheckInRoomViewModel
                {
                    BookingRoomId = x.Id,
                    RoomTypeCode = x.RoomTypeCode,
                    RoomTypeName = x.RoomTypeName,
                    RoomId = null,
                    Adults = x.Adults ?? 0,
                    Children = x.Children ?? 0,
                    Note = x.Note,
                    AvailableRooms = rooms
                        .Where(r => r.RoomTypeId == x.RoomTypeId)
                        .ToList()
                };
            }).ToList()
        };
    }

    public async Task<BookingActionResult> DeleteAsync(
        Guid bookingId,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        if (currentUserId == Guid.Empty)
        {
            return FailAction("Không xác định được người dùng đang đăng nhập.");
        }

        if (bookingId == Guid.Empty)
        {
            return FailAction("Lịch đặt phòng không hợp lệ.");
        }

        await using var transaction = await _context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        try
        {
            var booking = await _context.Bookings
                .FirstOrDefaultAsync(
                    x => x.Id == bookingId && !x.IsDeleted,
                    cancellationToken);

            if (booking is null)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                return FailAction("Lịch đặt phòng không tồn tại hoặc đã bị xóa trước đó.");
            }

            if (booking.Status is not 4 and not 5 and not 7 and not 8 and not 9)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                return FailAction("Chỉ được xóa lịch đặt phòng ở trạng thái Xác nhận thất bại, Check in thất bại, Khách hủy trước check in, Vắng mặt hoặc Khách sạn hủy.");
            }

            var now = DateTime.Now;

            var bookingRooms = await _context.BookingRooms
                .Where(x => x.BookingId == booking.Id)
                .ToListAsync(cancellationToken);

            var bookingRoomIds = bookingRooms
                .Select(x => x.Id)
                .ToArray();

            if (bookingRoomIds.Length > 0)
            {
                var bookingRoomServices = await _context.BookingRoomServices
                    .Where(x => bookingRoomIds.Contains(x.BookingRoomId))
                    .ToListAsync(cancellationToken);

                foreach (var item in bookingRoomServices)
                {
                    item.IsDeleted = true;
                    item.LastModifiedBy = currentUserId;
                    item.LastModifiedAt = now;
                }

                var additionalFees = await _context.AdditionalFeeRooms
                    .Where(x => bookingRoomIds.Contains(x.BookingRoomId))
                    .ToListAsync(cancellationToken);

                foreach (var item in additionalFees)
                {
                    item.IsDeleted = true;
                    item.LastModifiedBy = currentUserId;
                    item.LastModifiedAt = now;
                }

                foreach (var item in bookingRooms)
                {
                    item.IsDeleted = true;
                    item.LastModifiedBy = currentUserId;
                    item.LastModifiedAt = now;
                }
            }

            if (booking.GuestId.HasValue)
            {
                var guest = await _context.Guests
                    .FirstOrDefaultAsync(
                        x => x.Id == booking.GuestId.Value && !x.IsDeleted,
                        cancellationToken);

                if (guest is not null)
                {
                    guest.IsDeleted = true;
                    guest.LastModifiedBy = currentUserId;
                    guest.LastModifiedAt = now;
                }
            }

            booking.IsDeleted = true;
            booking.LastModifiedBy = currentUserId;
            booking.LastModifiedAt = now;

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return SuccessAction();
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            return FailAction("Không thể xóa lịch đặt phòng. Dữ liệu liên quan có thể đã thay đổi, vui lòng thử lại.");
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            return FailAction("Không thể xóa lịch đặt phòng. Vui lòng thử lại.");
        }
    }

    public async Task<BookingActionResult> ConfirmAsync(
        BookingConfirmViewModel model,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        if (currentUserId == Guid.Empty)
        {
            return FailAction("Không xác định được người dùng đang đăng nhập.");
        }

        if (model is null || model.BookingId == Guid.Empty)
        {
            return FailAction("Lịch đặt phòng không hợp lệ.");
        }

        await using var transaction = await _context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        try
        {
            var booking = await _context.Bookings.FirstOrDefaultAsync(
                x => x.Id == model.BookingId && !x.IsDeleted,
                cancellationToken);

            if (booking is null)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                return FailAction("Lịch đặt phòng không tồn tại hoặc đã bị xóa.");
            }

            if (booking.Status != 0)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                return FailAction("Lịch đặt phòng đã được xử lý. Không thể xác nhận lại.");
            }

            if (booking.BookingType != 0)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                return FailAction("Lịch trực tiếp tại quầy không có bước Xác nhận lịch đặt phòng.");
            }

            // Nghiệp vụ hiện tại: nút "Xác nhận lịch đặt phòng" luôn xác nhận thành công.
            // Status 0 -> Status 1 (Chờ check in).
            booking.Status = 1;
            booking.LastModifiedBy = currentUserId;
            booking.LastModifiedAt = DateTime.Now;

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return SuccessAction();
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            return FailAction("Không thể cập nhật trạng thái xác nhận. Dữ liệu có thể đã thay đổi, vui lòng thử lại.");
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            return FailAction("Không thể xác nhận lịch đặt phòng. Vui lòng thử lại.");
        }
    }

    public async Task<BookingActionResult> CheckInAsync(
        BookingCheckInViewModel model,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        if (currentUserId == Guid.Empty)
        {
            return FailAction("Không xác định được người dùng đang đăng nhập.");
        }

        var validationErrors = ValidateCheckInModel(model);
        if (validationErrors.Count > 0)
        {
            return new BookingActionResult
            {
                Succeeded = false,
                ErrorMessage = "Vui lòng kiểm tra lại thông tin check in.",
                FieldErrors = validationErrors
            };
        }

        await using var transaction = await _context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        try
        {
            var booking = await _context.Bookings
                .FirstOrDefaultAsync(x => x.Id == model.BookingId && !x.IsDeleted, cancellationToken);

            if (booking is null)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                return FailAction("Lịch đặt phòng không tồn tại hoặc đã bị xóa.");
            }

            if (booking.Status != 1)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                return FailAction("Lịch đặt phòng không còn ở trạng thái Chờ check in.");
            }

            Models.Guest? guest = null;

            if (!model.CreateNewGuest && model.GuestId.HasValue && model.GuestId.Value != Guid.Empty)
            {
                guest = await _context.Guests
                    .FirstOrDefaultAsync(x => x.Id == model.GuestId.Value && !x.IsDeleted, cancellationToken);

                if (guest is null)
                {
                    await transaction.RollbackAsync(CancellationToken.None);
                    return FailAction("Khách hàng được chọn không tồn tại hoặc đã bị xóa.");
                }
            }
            else
            {
                var guestCode = string.IsNullOrWhiteSpace(model.GuestCode)
                    ? await GetNextGuestCodeAsync(cancellationToken)
                    : model.GuestCode.Trim();

                if (guestCode.Any(char.IsWhiteSpace) || guestCode.Length > 50)
                {
                    await transaction.RollbackAsync(CancellationToken.None);
                    return FailAction("Mã khách hàng không hợp lệ.", nameof(BookingCheckInViewModel.GuestCode));
                }

                while (await _context.Guests.AnyAsync(x => x.GuestCode == guestCode, cancellationToken))
                {
                    guestCode = await GetNextGuestCodeAsync(cancellationToken);
                }

                var nowForGuest = DateTime.Now;
                guest = new Models.Guest
                {
                    Id = Guid.NewGuid(),
                    GuestCode = guestCode,
                    Name = model.GuestName.Trim(),
                    DateOfBirth = model.GuestDateOfBirth!.Value.Date,
                    Sex = model.GuestSex,
                    Address = model.GuestAddress.Trim(),
                    PhoneNumber = model.GuestPhoneNumber.Trim(),
                    Email = NormalizeOptional(model.GuestEmail),
                    IdentityNumber = model.GuestIdentityNumber.Trim(),
                    IdentityType = model.GuestIdentityType,
                    Note = NormalizeOptional(model.GuestNote),
                    IsDeleted = false,
                    CreatedBy = currentUserId,
                    CreatedAt = nowForGuest,
                    LastModifiedBy = currentUserId,
                    LastModifiedAt = nowForGuest
                };

                await _context.Guests.AddAsync(guest, cancellationToken);
            }

            var bookingRooms = await _context.BookingRooms
                .Include(x => x.RoomType)
                .Include(x => x.Room)
                .Where(x => x.BookingId == booking.Id && !x.IsDeleted)
                .OrderBy(x => x.CreatedAt)
                .ToListAsync(cancellationToken);

            var liveErrors = ValidateCheckInAgainstDatabase(model, booking, bookingRooms);
            if (liveErrors.Count > 0)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                return new BookingActionResult
                {
                    Succeeded = false,
                    ErrorMessage = "Dữ liệu phòng đã thay đổi. Vui lòng kiểm tra lại thông tin check in.",
                    FieldErrors = liveErrors
                };
            }

            var selectedRoomIds = model.Rooms.Select(x => x.RoomId!.Value).Distinct().ToArray();
            var selectedRooms = await _context.Rooms
                .Include(x => x.RoomType)
                .Where(x => selectedRoomIds.Contains(x.Id))
                .ToListAsync(cancellationToken);

            if (selectedRooms.Count != selectedRoomIds.Length || selectedRooms.Any(x => x.Status != 0 || !x.IsActive || x.IsDeleted))
            {
                await transaction.RollbackAsync(CancellationToken.None);
                return FailAction("Có phòng vừa được sử dụng hoặc không còn khả dụng. Vui lòng tải lại màn hình check in.");
            }

            var roomTypeErrors = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var selectedRoomMap = selectedRooms.ToDictionary(x => x.Id);
            foreach (var input in model.Rooms)
            {
                if (!input.RoomId.HasValue || !selectedRoomMap.TryGetValue(input.RoomId.Value, out var selectedRoom))
                {
                    continue;
                }

                var bookingRoom = bookingRooms.FirstOrDefault(x => x.Id == input.BookingRoomId);
                if (bookingRoom is null || selectedRoom.RoomTypeId != bookingRoom.RoomTypeId)
                {
                    var index = model.Rooms.IndexOf(input);
                    roomTypeErrors[$"Rooms[{index}].RoomId"] = $"Phòng {selectedRoom.RoomCode} không thuộc đúng loại phòng của BookingRoom này.";
                }
            }

            if (roomTypeErrors.Count > 0)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                return new BookingActionResult
                {
                    Succeeded = false,
                    ErrorMessage = "Phòng được chọn không phù hợp với loại phòng yêu cầu.",
                    FieldErrors = roomTypeErrors
                };
            }

            var roomsById = selectedRooms.ToDictionary(x => x.Id);
            var assignmentsByBookingRoom = model.Rooms.ToDictionary(x => x.BookingRoomId);
            var now = DateTime.Now;

            var requestedGuestCode = model.GuestCode.Trim();
            var duplicateGuestCode = await _context.Guests
                .AnyAsync(x => x.Id != guest.Id && !x.IsDeleted && x.GuestCode == requestedGuestCode, cancellationToken);

            if (duplicateGuestCode)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                return FailAction("Mã khách hàng đã tồn tại.", nameof(BookingCheckInViewModel.GuestCode));
            }

            guest.GuestCode = requestedGuestCode;
            guest.Name = model.GuestName.Trim();
            guest.DateOfBirth = model.GuestDateOfBirth!.Value.Date;
            guest.Sex = model.GuestSex;
            guest.Address = model.GuestAddress.Trim();
            guest.PhoneNumber = NormalizePhoneNumberForStorage(model.GuestPhoneNumber);
            guest.Email = NormalizeOptional(model.GuestEmail);
            guest.IdentityNumber = model.GuestIdentityNumber.Trim();
            guest.IdentityType = model.GuestIdentityType;
            guest.Note = NormalizeOptional(model.GuestNote);
            guest.LastModifiedBy = currentUserId;
            guest.LastModifiedAt = now;

            foreach (var bookingRoom in bookingRooms)
            {
                var input = assignmentsByBookingRoom[bookingRoom.Id];
                var room = roomsById[input.RoomId!.Value];

                bookingRoom.RoomId = room.Id;
                bookingRoom.Adults = input.Adults;
                bookingRoom.Children = input.Children;
                bookingRoom.Note = NormalizeOptional(input.Note);
                bookingRoom.LastModifiedBy = currentUserId;
                bookingRoom.LastModifiedAt = now;

                room.Status = 1;
                room.LastModifiedBy = currentUserId;
                room.LastModifiedAt = now;
            }

            booking.GuestId = guest.Id;
            booking.TotalAdults = model.TotalAdults;
            booking.TotalChildren = model.TotalChildren;
            booking.Note = NormalizeOptional(model.BookingNote);
            booking.ActualCheckInAt = now;
            booking.Status = 2;
            booking.LastModifiedBy = currentUserId;
            booking.LastModifiedAt = now;

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return SuccessAction();
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            return FailAction("Không thể lưu thông tin check in. Dữ liệu có thể đã thay đổi, vui lòng thử lại.");
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            return FailAction("Không thể thực hiện check in. Vui lòng thử lại.");
        }
    }

    public async Task<BookingActionResult> CancelBeforeCheckInAsync(
        BookingCancelViewModel model,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        if (currentUserId == Guid.Empty)
        {
            return FailAction("Không xác định được người dùng đang đăng nhập.");
        }

        if (model is null || model.BookingId == Guid.Empty)
        {
            return FailAction("Lịch đặt phòng không hợp lệ.");
        }

        if (model.CancelStatus is not 4 and not 5 and not 7 and not 8 and not 9)
        {
            return FailAction("Vui lòng chọn một lý do hủy hợp lệ.", nameof(BookingCancelViewModel.CancelStatus));
        }

        await using var transaction = await _context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        try
        {
            var booking = await _context.Bookings.FirstOrDefaultAsync(
                x => x.Id == model.BookingId && !x.IsDeleted,
                cancellationToken);

            if (booking is null)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                return FailAction("Lịch đặt phòng không tồn tại hoặc đã bị xóa.");
            }

            var allowedForCurrentStatus = booking.Status switch
            {
                0 => model.CancelStatus is 4 or 7 or 9,
                1 => model.CancelStatus is 5 or 7 or 8 or 9,
                _ => false
            };

            if (!allowedForCurrentStatus)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                return FailAction("Lý do hủy không phù hợp với trạng thái hiện tại của Booking.");
            }

            booking.Status = model.CancelStatus.Value;
            booking.LastModifiedBy = currentUserId;
            booking.LastModifiedAt = DateTime.Now;

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return SuccessAction();
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            return FailAction("Không thể cập nhật trạng thái hủy. Dữ liệu có thể đã thay đổi, vui lòng thử lại.");
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            return FailAction("Không thể hủy lịch đặt phòng. Vui lòng thử lại.");
        }
    }

    public async Task<BookingCheckOutViewModel?> GetCheckOutModelAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var booking = await _context.Bookings
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken);

        if (booking is null || booking.Status != 2)
        {
            return null;
        }

        var guest = booking.GuestId.HasValue
            ? await _context.Guests
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == booking.GuestId.Value && !x.IsDeleted, cancellationToken)
            : null;

        var stayStart = booking.ActualCheckInAt ?? booking.ExpectedCheckInAt;
        var stayNights = Math.Max(1, (DateTime.Now.Date - stayStart.Date).Days);

        var bookingRooms = await _context.BookingRooms
            .AsNoTracking()
            .Where(x => x.BookingId == id && !x.IsDeleted)
            .Include(x => x.RoomType)
            .Include(x => x.Room)
            .OrderBy(x => x.RoomType != null ? x.RoomType.RoomTypeCode : string.Empty)
            .ThenBy(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        if (bookingRooms.Count == 0)
        {
            return new BookingCheckOutViewModel
            {
                BookingId = booking.Id,
                GuestCode = guest?.GuestCode ?? string.Empty,
                GuestName = guest?.Name ?? string.Empty,
                GuestDateOfBirth = guest?.DateOfBirth,
                GuestSex = guest?.Sex,
                GuestAddress = guest?.Address ?? string.Empty,
                GuestPhoneNumber = guest?.PhoneNumber,
                GuestEmail = guest?.Email,
                GuestIdentityNumber = guest?.IdentityNumber ?? string.Empty,
                GuestIdentityType = guest?.IdentityType,
                GuestNote = guest?.Note,
                BillCode = await GetNextBillCodeAsync(cancellationToken),
                BookingNote = booking.Note,
                PaymentReceived = false,
                StayNights = stayNights,
                PaidDepositAmount = booking.DepositAmount,
                Rooms = new List<BookingCheckOutRoomViewModel>()
            };
        }

        var bookingRoomIds = bookingRooms.Select(x => x.Id).ToArray();

        var existingServices = await _context.BookingRoomServices
            .AsNoTracking()
            .Where(x => bookingRoomIds.Contains(x.BookingRoomId) && !x.IsDeleted)
            .Include(x => x.Service)
            .OrderBy(x => x.UsedAt)
            .ThenBy(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        var existingFees = await _context.AdditionalFeeRooms
            .AsNoTracking()
            .Where(x => bookingRoomIds.Contains(x.BookingRoomId) && !x.IsDeleted)
            .OrderBy(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        var services = await _context.Services
            .AsNoTracking()
            .Where(x => !x.IsDeleted)
            .OrderBy(x => x.ServiceCode)
            .Select(x => new CheckOutServiceOptionViewModel
            {
                Id = x.Id,
                ServiceCode = x.ServiceCode,
                ServiceName = x.ServiceName,
                UnitPrice = x.UnitPrice ?? 0m,
                PriceUnitType = x.PriceUnitType,
                IsActive = x.IsActive
            })
            .ToListAsync(cancellationToken);

        var rooms = new List<BookingCheckOutRoomViewModel>(bookingRooms.Count);

        foreach (var bookingRoom in bookingRooms)
        {
            var roomBaseAmount = Math.Round(
                (bookingRoom.RoomType?.PricePerNight ?? 0m) * stayNights,
                2,
                MidpointRounding.AwayFromZero);

            var roomServiceInputs = existingServices
                .Where(x => x.BookingRoomId == bookingRoom.Id)
                .Select(x => new BookingCheckOutServiceInputViewModel
                {
                    Id = x.Id,
                    ServiceId = x.ServiceId ?? Guid.Empty,
                    UnitPrice = x.Service?.UnitPrice ?? 0m,
                    PriceUnitType = x.Service?.PriceUnitType,
                    UsedAt = x.UsedAt,
                    TotalPrice = x.TotalPrice,
                    Note = x.Note
                })
                .ToList();

            var roomFeeInputs = existingFees
                .Where(x => x.BookingRoomId == bookingRoom.Id)
                .Select(x => new BookingCheckOutFeeInputViewModel
                {
                    Id = x.Id,
                    FeeName = x.FeeName,
                    FeeType = x.FeeType,
                    TotalFee = x.TotalFee,
                    Description = x.Decription
                })
                .ToList();

            var serviceTotal = roomServiceInputs.Sum(x => x.TotalPrice);
            var feeTotal = roomFeeInputs.Sum(x => x.TotalFee);

            rooms.Add(new BookingCheckOutRoomViewModel
            {
                BookingRoomId = bookingRoom.Id,
                RoomTypeCode = bookingRoom.RoomType?.RoomTypeCode ?? string.Empty,
                RoomTypeName = bookingRoom.RoomType?.RoomTypeName ?? string.Empty,
                RoomCode = bookingRoom.Room?.RoomCode ?? string.Empty,
                RoomPricePerNight = bookingRoom.RoomType?.PricePerNight ?? 0m,
                StayNights = stayNights,
                RoomBaseAmount = roomBaseAmount,
                ServiceTotalAmount = serviceTotal,
                FeeTotalAmount = feeTotal,
                RoomTotalAmount = roomBaseAmount + serviceTotal + feeTotal,
                NeedsMaintenance = false,
                Note = bookingRoom.Note,
                ServiceInputs = roomServiceInputs,
                FeeInputs = roomFeeInputs
            });
        }

        var totalRoomAmount = rooms.Sum(x => x.RoomBaseAmount);
        var totalServiceAmount = rooms.Sum(x => x.ServiceTotalAmount);
        var totalFeeAmount = rooms.Sum(x => x.FeeTotalAmount);
        var totalAmount = Math.Round(
            totalRoomAmount + totalServiceAmount + totalFeeAmount,
            2,
            MidpointRounding.AwayFromZero);

        return new BookingCheckOutViewModel
        {
            BookingId = booking.Id,
            GuestCode = guest?.GuestCode ?? string.Empty,
            GuestName = guest?.Name ?? string.Empty,
            GuestDateOfBirth = guest?.DateOfBirth,
            GuestSex = guest?.Sex,
            GuestAddress = guest?.Address ?? string.Empty,
            GuestPhoneNumber = guest?.PhoneNumber,
            GuestEmail = guest?.Email,
            GuestIdentityNumber = guest?.IdentityNumber ?? string.Empty,
            GuestIdentityType = guest?.IdentityType,
            GuestNote = guest?.Note,
            BillCode = await GetNextBillCodeAsync(cancellationToken),
            BookingNote = booking.Note,
            PaymentReceived = false,
            PaymentMethod = null,
            ReferenceCode = null,
            StayNights = stayNights,
            TotalRoomAmount = totalRoomAmount,
            TotalServiceAmount = totalServiceAmount,
            TotalFeeAmount = totalFeeAmount,
            TotalAmount = totalAmount,
            PaidDepositAmount = booking.DepositAmount,
            FinalAmount = Math.Max(
                0m,
                Math.Round(totalAmount - booking.DepositAmount, 2, MidpointRounding.AwayFromZero)),
            Rooms = rooms,
            Services = services
        };
    }

    public async Task<BookingPaymentRecoveryViewModel?> GetPaymentRecoveryModelAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var booking = await _context.Bookings
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken);

        if (booking is null || booking.Status != 6)
            return null;

        var bill = await _context.Bills
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.BookingId == booking.Id && !x.IsDeleted && x.Status == 1,
                cancellationToken);

        if (bill is null)
            return null;

        var guest = booking.GuestId.HasValue
            ? await _context.Guests
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == booking.GuestId.Value && !x.IsDeleted, cancellationToken)
            : null;

        if (guest is null)
            return null;

        var bookingRooms = await _context.BookingRooms
            .AsNoTracking()
            .Where(x => x.BookingId == booking.Id && !x.IsDeleted)
            .Include(x => x.RoomType)
            .Include(x => x.Room)
            .OrderBy(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        if (bookingRooms.Count == 0)
            return null;

        var roomIds = bookingRooms.Select(x => x.Id).ToArray();
        var services = await _context.BookingRoomServices
            .AsNoTracking()
            .Where(x => roomIds.Contains(x.BookingRoomId) && !x.IsDeleted)
            .Include(x => x.Service)
            .OrderBy(x => x.UsedAt)
            .ThenBy(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        var fees = await _context.AdditionalFeeRooms
            .AsNoTracking()
            .Where(x => roomIds.Contains(x.BookingRoomId) && !x.IsDeleted)
            .OrderBy(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        return new BookingPaymentRecoveryViewModel
        {
            BookingId = booking.Id,
            BookingCode = booking.BookingCode,
            GuestCode = guest.GuestCode,
            GuestName = guest.Name,
            GuestDateOfBirth = guest.DateOfBirth,
            GuestSex = guest.Sex,
            GuestAddress = guest.Address ?? string.Empty,
            GuestPhoneNumber = guest.PhoneNumber ?? string.Empty,
            GuestEmail = guest.Email,
            GuestIdentityNumber = guest.IdentityNumber ?? string.Empty,
            GuestIdentityType = guest.IdentityType,
            GuestNote = guest.Note,
            BookingNote = booking.Note,
            BillId = bill.Id,
            BillCode = bill.BillCode,
            TotalAmount = bill.TotalAmount ?? 0m,
            DepositAmount = bill.DepositAmount,
            FinalAmount = bill.FinalAmount,
            BillNote = bill.Note,
            PaymentReceived = false,
            PaymentMethod = null,
            ReferenceCode = null,
            Rooms = bookingRooms.Select(room => new BookingPaymentRecoveryRoomViewModel
            {
                BookingRoomId = room.Id,
                RoomTypeCode = room.RoomType?.RoomTypeCode ?? string.Empty,
                RoomTypeName = room.RoomType?.RoomTypeName ?? string.Empty,
                RoomCode = room.Room?.RoomCode ?? string.Empty,
                RoomName = room.Room?.RoomName ?? string.Empty,
                Note = room.Note,
                Services = services
                    .Where(x => x.BookingRoomId == room.Id)
                    .Select(x => new BookingPaymentRecoveryServiceViewModel
                    {
                        Id = x.Id,
                        ServiceCode = x.Service?.ServiceCode ?? string.Empty,
                        ServiceName = x.Service?.ServiceName ?? string.Empty,
                        UsedAt = x.UsedAt,
                        TotalPrice = x.TotalPrice,
                        Note = x.Note
                    })
                    .ToList(),
                Fees = fees
                    .Where(x => x.BookingRoomId == room.Id)
                    .Select(x => new BookingPaymentRecoveryFeeViewModel
                    {
                        Id = x.Id,
                        FeeName = x.FeeName,
                        FeeType = x.FeeType,
                        TotalFee = x.TotalFee,
                        Description = x.Decription
                    })
                    .ToList()
            }).ToList()
        };
    }

    public async Task<BookingActionResult> CompletePaymentRecoveryAsync(
        BookingPaymentRecoveryViewModel model,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        if (currentUserId == Guid.Empty)
            return FailAction("Không xác định được người dùng đang đăng nhập.");

        await using var transaction = await _context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        try
        {
            var booking = await _context.Bookings
                .FirstOrDefaultAsync(x => x.Id == model.BookingId && !x.IsDeleted, cancellationToken);

            if (booking is null)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                return FailAction("Lịch đặt phòng không tồn tại hoặc đã bị xóa.");
            }

            if (booking.Status != 6)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                return FailAction("Chỉ có lịch đặt phòng ở trạng thái Check out thất bại mới được tiếp nhận thanh toán lại.");
            }

            var bill = await _context.Bills
                .FirstOrDefaultAsync(x => x.BookingId == booking.Id && !x.IsDeleted, cancellationToken);

            if (bill is null)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                return FailAction("Không tìm thấy hóa đơn của Booking.");
            }

            if (bill.Status != 1)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                return FailAction("Hóa đơn này không còn ở trạng thái Thanh toán thất bại.");
            }

            var errors = ValidatePaymentRecoveryModel(model);

            var guest = booking.GuestId.HasValue
                ? await _context.Guests.FirstOrDefaultAsync(
                    x => x.Id == booking.GuestId.Value && !x.IsDeleted,
                    cancellationToken)
                : null;

            if (guest is null)
            {
                errors[nameof(BookingPaymentRecoveryViewModel.GuestCode)] = "Booking không có khách hàng hợp lệ để cập nhật.";
            }
            else if (errors.Count == 0)
            {
                var guestCode = model.GuestCode.Trim();
                if (await _context.Guests.AnyAsync(
                        x => x.Id != guest.Id && !x.IsDeleted && x.GuestCode == guestCode,
                        cancellationToken))
                {
                    errors[nameof(BookingPaymentRecoveryViewModel.GuestCode)] = "Mã khách hàng đã tồn tại.";
                }
            }

            var bookingRooms = await _context.BookingRooms
                .Where(x => x.BookingId == booking.Id && !x.IsDeleted)
                .ToListAsync(cancellationToken);

            var roomMap = bookingRooms.ToDictionary(x => x.Id);
            var postedRooms = model.Rooms ?? new List<BookingPaymentRecoveryRoomViewModel>();
            var postedRoomIds = postedRooms.Select(x => x?.BookingRoomId ?? Guid.Empty).ToArray();
            var expectedRoomIds = roomMap.Keys.ToHashSet();

            if (postedRoomIds.Length != expectedRoomIds.Count ||
                postedRoomIds.Distinct().Count() != postedRoomIds.Length ||
                !expectedRoomIds.SetEquals(postedRoomIds))
            {
                errors[string.Empty] = "Danh sách BookingRoom gửi lên không còn đồng bộ với dữ liệu hiện tại. Vui lòng tải lại trang.";
            }

            var serviceIds = postedRooms
                .Where(x => x is not null)
                .SelectMany(x => x!.Services ?? new List<BookingPaymentRecoveryServiceViewModel>())
                .Select(x => x.Id)
                .Where(x => x != Guid.Empty)
                .ToArray();

            var feeIds = postedRooms
                .Where(x => x is not null)
                .SelectMany(x => x!.Fees ?? new List<BookingPaymentRecoveryFeeViewModel>())
                .Select(x => x.Id)
                .Where(x => x != Guid.Empty)
                .ToArray();

            var serviceEntities = serviceIds.Length == 0
                ? new List<Models.BookingRoomService>()
                : await _context.BookingRoomServices
                    .Where(x => serviceIds.Contains(x.Id) && !x.IsDeleted)
                    .ToListAsync(cancellationToken);

            var feeEntities = feeIds.Length == 0
                ? new List<Models.AdditionalFeeRoom>()
                : await _context.AdditionalFeeRooms
                    .Where(x => feeIds.Contains(x.Id) && !x.IsDeleted)
                    .ToListAsync(cancellationToken);

            var serviceMap = serviceEntities.ToDictionary(x => x.Id);
            var feeMap = feeEntities.ToDictionary(x => x.Id);

            for (var roomIndex = 0; roomIndex < postedRooms.Count; roomIndex++)
            {
                var room = postedRooms[roomIndex];
                var roomPrefix = $"Rooms[{roomIndex}]";

                if (room is null)
                {
                    errors[$"{roomPrefix}.BookingRoomId"] = "BookingRoom không hợp lệ.";
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(room.Note) && room.Note.Trim().Length > 255)
                    errors[$"{roomPrefix}.Note"] = "Ghi chú phòng không được vượt quá 255 ký tự.";

                var serviceInputs = room.Services ?? new List<BookingPaymentRecoveryServiceViewModel>();
                var feeInputs = room.Fees ?? new List<BookingPaymentRecoveryFeeViewModel>();
                var serviceIdsForRoom = new HashSet<Guid>();

                for (var serviceIndex = 0; serviceIndex < serviceInputs.Count; serviceIndex++)
                {
                    var service = serviceInputs[serviceIndex];
                    var prefix = $"{roomPrefix}.Services[{serviceIndex}].Note";

                    if (!serviceIdsForRoom.Add(service.Id))
                    {
                        errors[prefix] = "Dịch vụ bị gửi trùng.";
                        continue;
                    }

                    if (!serviceMap.TryGetValue(service.Id, out var entity) || entity.BookingRoomId != room.BookingRoomId)
                    {
                        errors[prefix] = "Dịch vụ không thuộc BookingRoom tương ứng.";
                        continue;
                    }

                    if (!string.IsNullOrWhiteSpace(service.Note) && service.Note.Trim().Length > 255)
                        errors[prefix] = "Ghi chú dịch vụ không được vượt quá 255 ký tự.";
                }

                var feeIdsForRoom = new HashSet<Guid>();
                for (var feeIndex = 0; feeIndex < feeInputs.Count; feeIndex++)
                {
                    var fee = feeInputs[feeIndex];
                    var prefix = $"{roomPrefix}.Fees[{feeIndex}].Description";

                    if (!feeIdsForRoom.Add(fee.Id))
                    {
                        errors[prefix] = "Phí phát sinh bị gửi trùng.";
                        continue;
                    }

                    if (!feeMap.TryGetValue(fee.Id, out var entity) || entity.BookingRoomId != room.BookingRoomId)
                    {
                        errors[prefix] = "Phí phát sinh không thuộc BookingRoom tương ứng.";
                        continue;
                    }

                    if (!string.IsNullOrWhiteSpace(fee.Description) && fee.Description.Trim().Length > 255)
                        errors[prefix] = "Ghi chú phí phát sinh không được vượt quá 255 ký tự.";
                }
            }

            if (errors.Count > 0)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                return new BookingActionResult
                {
                    Succeeded = false,
                    ErrorMessage = "Vui lòng kiểm tra lại thông tin thanh toán lại.",
                    FieldErrors = errors
                };
            }

            var now = DateTime.Now;
            guest!.GuestCode = model.GuestCode.Trim();
            guest.Name = model.GuestName.Trim();
            guest.DateOfBirth = model.GuestDateOfBirth!.Value.Date;
            guest.Sex = model.GuestSex;
            guest.Address = model.GuestAddress.Trim();
            guest.PhoneNumber = NormalizePhoneNumberForStorage(model.GuestPhoneNumber);
            guest.Email = NormalizeOptional(model.GuestEmail);
            guest.IdentityNumber = model.GuestIdentityNumber.Trim();
            guest.IdentityType = model.GuestIdentityType;
            guest.Note = NormalizeOptional(model.GuestNote);
            guest.LastModifiedBy = currentUserId;
            guest.LastModifiedAt = now;

            booking.Note = NormalizeOptional(model.BookingNote);
            booking.Status = 3;
            booking.LastModifiedBy = currentUserId;
            booking.LastModifiedAt = now;

            foreach (var room in postedRooms)
            {
                var roomEntity = roomMap[room!.BookingRoomId];
                roomEntity.Note = NormalizeOptional(room.Note);
                roomEntity.LastModifiedBy = currentUserId;
                roomEntity.LastModifiedAt = now;

                foreach (var service in room.Services ?? new List<BookingPaymentRecoveryServiceViewModel>())
                {
                    var serviceEntity = serviceMap[service.Id];
                    serviceEntity.Note = NormalizeOptional(service.Note);
                    serviceEntity.LastModifiedBy = currentUserId;
                    serviceEntity.LastModifiedAt = now;
                }

                foreach (var fee in room.Fees ?? new List<BookingPaymentRecoveryFeeViewModel>())
                {
                    var feeEntity = feeMap[fee.Id];
                    feeEntity.Decription = NormalizeOptional(fee.Description);
                    feeEntity.LastModifiedBy = currentUserId;
                    feeEntity.LastModifiedAt = now;
                }
            }

            var paymentMethod = model.PaymentMethod!.Value;
            bill.PaymentMethod = paymentMethod;
            bill.PaymentDate = now;
            bill.ReferenceCode = paymentMethod == 1 ? NormalizeOptional(model.ReferenceCode) : null;
            bill.Status = 0;
            bill.Note = NormalizeOptional(model.BillNote);
            bill.LastModifiedBy = currentUserId;
            bill.LastModifiedAt = now;

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return SuccessAction();
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            return FailAction("Không thể ghi nhận thanh toán lại. Dữ liệu có thể đã thay đổi, vui lòng thử lại.");
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            return FailAction("Không thể ghi nhận thanh toán lại. Vui lòng thử lại.");
        }
    }

    private static Dictionary<string, string> ValidatePaymentRecoveryModel(
        BookingPaymentRecoveryViewModel model)
    {
        var errors = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var code = model.GuestCode?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(code))
            errors[nameof(BookingPaymentRecoveryViewModel.GuestCode)] = "Mã khách hàng không được để trống.";
        else if (!IsValidAlphanumericCode(code))
            errors[nameof(BookingPaymentRecoveryViewModel.GuestCode)] = "Mã khách hàng chỉ được bao gồm chữ và số, không chứa dấu cách và ký tự đặc biệt.";
        else if (code.Length > 50)
            errors[nameof(BookingPaymentRecoveryViewModel.GuestCode)] = "Mã khách hàng không được vượt quá 50 ký tự.";

        var name = model.GuestName?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(name))
            errors[nameof(BookingPaymentRecoveryViewModel.GuestName)] = "Tên khách hàng là bắt buộc.";
        else if (name.Length > 100)
            errors[nameof(BookingPaymentRecoveryViewModel.GuestName)] = "Tên khách hàng không được vượt quá 100 ký tự.";

        if (!model.GuestDateOfBirth.HasValue)
            errors[nameof(BookingPaymentRecoveryViewModel.GuestDateOfBirth)] = "Ngày sinh là bắt buộc.";
        else if (model.GuestDateOfBirth.Value.Date > DateTime.Today)
            errors[nameof(BookingPaymentRecoveryViewModel.GuestDateOfBirth)] = "Ngày sinh không được lớn hơn ngày hiện tại.";

        if (model.GuestSex is not 0 and not 1 and not 2)
            errors[nameof(BookingPaymentRecoveryViewModel.GuestSex)] = "Giới tính là bắt buộc.";

        var address = model.GuestAddress?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(address))
            errors[nameof(BookingPaymentRecoveryViewModel.GuestAddress)] = "Địa chỉ là bắt buộc.";
        else if (address.Length > 255)
            errors[nameof(BookingPaymentRecoveryViewModel.GuestAddress)] = "Địa chỉ không được vượt quá 255 ký tự.";

        var phone = model.GuestPhoneNumber?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(phone))
            errors[nameof(BookingPaymentRecoveryViewModel.GuestPhoneNumber)] = "Số điện thoại là bắt buộc.";
        else if (phone.Length > 30 || !TryNormalizePhoneNumber(phone, out _))
            errors[nameof(BookingPaymentRecoveryViewModel.GuestPhoneNumber)] = "Số điện thoại không hợp lệ. Có thể nhập số quốc tế; sau khi chuẩn hóa phải có 7-15 chữ số.";

        var email = model.GuestEmail?.Trim() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(email) &&
            (email.Length > 254 || !new EmailAddressAttribute().IsValid(email)))
            errors[nameof(BookingPaymentRecoveryViewModel.GuestEmail)] = "Email không đúng định dạng hoặc vượt quá 254 ký tự.";

        ValidateIdentityNumber(
            model.GuestIdentityNumber,
            model.GuestIdentityType,
            isRequired: true,
            identityFieldName: nameof(BookingPaymentRecoveryViewModel.GuestIdentityNumber),
            identityTypeFieldName: nameof(BookingPaymentRecoveryViewModel.GuestIdentityType),
            errors);

        if (!string.IsNullOrWhiteSpace(model.GuestNote) && model.GuestNote.Trim().Length > 255)
            errors[nameof(BookingPaymentRecoveryViewModel.GuestNote)] = "Ghi chú khách hàng không được vượt quá 255 ký tự.";

        if (!string.IsNullOrWhiteSpace(model.BookingNote) && model.BookingNote.Trim().Length > 255)
            errors[nameof(BookingPaymentRecoveryViewModel.BookingNote)] = "Ghi chú Booking không được vượt quá 255 ký tự.";

        if (!model.PaymentReceived)
            errors[nameof(BookingPaymentRecoveryViewModel.PaymentReceived)] = "Bạn phải xác nhận đã nhận được tiền thanh toán.";

        if (model.PaymentMethod is not 0 and not 1)
            errors[nameof(BookingPaymentRecoveryViewModel.PaymentMethod)] = "Vui lòng chọn phương thức thanh toán.";

        var reference = model.ReferenceCode?.Trim() ?? string.Empty;
        if (reference.Length > 100)
            errors[nameof(BookingPaymentRecoveryViewModel.ReferenceCode)] = "Mã giao dịch không được vượt quá 100 ký tự.";
        else if (model.PaymentMethod == 1 && string.IsNullOrWhiteSpace(reference))
            errors[nameof(BookingPaymentRecoveryViewModel.ReferenceCode)] = "Mã giao dịch là bắt buộc khi thanh toán chuyển khoản.";

        if (!string.IsNullOrWhiteSpace(model.BillNote) && model.BillNote.Trim().Length > 255)
            errors[nameof(BookingPaymentRecoveryViewModel.BillNote)] = "Ghi chú hóa đơn không được vượt quá 255 ký tự.";

        return errors;
    }

    public async Task<BookingActionResult> CheckOutAsync(
        BookingCheckOutViewModel model,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        if (currentUserId == Guid.Empty)
        {
            return FailAction("Không xác định được người dùng đang đăng nhập.");
        }

        await using var transaction = await _context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        try
        {
            var booking = await _context.Bookings
                .FirstOrDefaultAsync(x => x.Id == model.BookingId && !x.IsDeleted, cancellationToken);

            if (booking is null)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                return FailAction("Lịch đặt phòng không tồn tại hoặc đã bị xóa.");
            }

            if (booking.Status != 2)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                return FailAction("Chỉ có lịch đặt phòng ở trạng thái Đang ở mới được check out.");
            }

            var validationNow = DateTime.Now;
            var validationStayStart = booking.ActualCheckInAt ?? booking.ExpectedCheckInAt;
            var modelErrors = ValidateCheckOutModel(model, validationStayStart, validationNow);
            if (modelErrors.Count > 0)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                return new BookingActionResult
                {
                    Succeeded = false,
                    ErrorMessage = "Vui lòng kiểm tra lại thông tin check out.",
                    FieldErrors = modelErrors
                };
            }

            var guest = booking.GuestId.HasValue
                ? await _context.Guests.FirstOrDefaultAsync(
                    x => x.Id == booking.GuestId.Value && !x.IsDeleted,
                    cancellationToken)
                : null;

            if (guest is null)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                return FailAction("Booking không có khách hàng hợp lệ để cập nhật.", "GuestCode");
            }

            var normalizedGuestCode = model.GuestCode.Trim();
            if (await _context.Guests.AnyAsync(
                    x => x.Id != guest.Id && !x.IsDeleted && x.GuestCode == normalizedGuestCode,
                    cancellationToken))
            {
                await transaction.RollbackAsync(CancellationToken.None);
                return FailAction("Mã khách hàng đã tồn tại.", "GuestCode");
            }

            var normalizedBillCode = model.BillCode.Trim();
            if (await _context.Bills.AnyAsync(
                    x => !x.IsDeleted && x.BillCode == normalizedBillCode,
                    cancellationToken))
            {
                await transaction.RollbackAsync(CancellationToken.None);
                return FailAction("Mã hóa đơn đã tồn tại.", "BillCode");
            }

            guest.GuestCode = normalizedGuestCode;
            guest.Name = model.GuestName.Trim();
            guest.DateOfBirth = model.GuestDateOfBirth!.Value.Date;
            guest.Sex = model.GuestSex;
            guest.Address = model.GuestAddress.Trim();
            guest.PhoneNumber = NormalizeOptional(model.GuestPhoneNumber);
            guest.Email = NormalizeOptional(model.GuestEmail);
            guest.IdentityNumber = model.GuestIdentityNumber.Trim();
            guest.IdentityType = model.GuestIdentityType;
            guest.Note = NormalizeOptional(model.GuestNote);
            guest.LastModifiedBy = currentUserId;
            guest.LastModifiedAt = validationNow;

            if (await _context.Bills.AnyAsync(x => x.BookingId == booking.Id && !x.IsDeleted, cancellationToken))
            {
                await transaction.RollbackAsync(CancellationToken.None);
                return FailAction("Lịch đặt phòng này đã có hóa đơn. Không thể tạo thêm hóa đơn.");
            }

            var bookingRooms = await _context.BookingRooms
                .Include(x => x.RoomType)
                .Include(x => x.Room)
                .Where(x => x.BookingId == booking.Id && !x.IsDeleted)
                .OrderBy(x => x.CreatedAt)
                .ToListAsync(cancellationToken);

            if (bookingRooms.Count == 0 || bookingRooms.Any(x => !x.RoomId.HasValue || x.Room is null))
            {
                await transaction.RollbackAsync(CancellationToken.None);
                return FailAction("Booking chưa được gán đầy đủ phòng. Không thể check out.");
            }

            var bookingRoomMap = bookingRooms.ToDictionary(x => x.Id);
            var submittedRoomIds = (model.Rooms ?? new List<BookingCheckOutRoomViewModel>())
                .Select(x => x.BookingRoomId)
                .ToArray();

            if (submittedRoomIds.Length != bookingRooms.Count ||
                submittedRoomIds.Distinct().Count() != bookingRooms.Count ||
                submittedRoomIds.Any(x => !bookingRoomMap.ContainsKey(x)))
            {
                await transaction.RollbackAsync(CancellationToken.None);
                return FailAction("Danh sách BookingRoom gửi lên không hợp lệ.");
            }

            var existingServices = await _context.BookingRoomServices
                .Where(x => bookingRoomMap.Keys.Contains(x.BookingRoomId) && !x.IsDeleted)
                .ToListAsync(cancellationToken);

            var existingFees = await _context.AdditionalFeeRooms
                .Where(x => bookingRoomMap.Keys.Contains(x.BookingRoomId) && !x.IsDeleted)
                .ToListAsync(cancellationToken);

            var existingServiceMap = existingServices.ToDictionary(x => x.Id);
            var existingFeeMap = existingFees.ToDictionary(x => x.Id);

            var requestedServiceIds = model.Rooms
                .SelectMany(x => x.ServiceInputs ?? new List<BookingCheckOutServiceInputViewModel>())
                .Select(x => x.ServiceId)
                .Where(x => x != Guid.Empty)
                .Distinct()
                .ToArray();

            var services = requestedServiceIds.Length == 0
                ? new List<Models.Service>()
                : await _context.Services
                    .Where(x => requestedServiceIds.Contains(x.Id) && !x.IsDeleted)
                    .ToListAsync(cancellationToken);

            var serviceMap = services.ToDictionary(x => x.Id);
            var now = DateTime.Now;
            var stayStart = booking.ActualCheckInAt ?? booking.ExpectedCheckInAt;
            var nights = Math.Max(1, (now.Date - stayStart.Date).Days);

            var roomChargeMap = bookingRooms.ToDictionary(
                x => x.Id,
                x => Math.Round(
                    (x.RoomType?.PricePerNight ?? 0m) * nights,
                    2,
                    MidpointRounding.AwayFromZero));

            decimal serviceTotal = 0m;
            decimal feeTotal = 0m;
            var serviceTotalByBookingRoom = bookingRooms.ToDictionary(x => x.Id, _ => 0m);
            var feeTotalByBookingRoom = bookingRooms.ToDictionary(x => x.Id, _ => 0m);

            foreach (var roomInput in model.Rooms)
            {
                var bookingRoom = bookingRoomMap[roomInput.BookingRoomId];
                bookingRoom.Note = NormalizeOptional(roomInput.Note);
                bookingRoom.LastModifiedBy = currentUserId;
                bookingRoom.LastModifiedAt = now;

                var submittedServiceIdsForRoom = new HashSet<Guid>();
                foreach (var serviceInput in roomInput.ServiceInputs ?? new List<BookingCheckOutServiceInputViewModel>())
                {
                    if (serviceInput.ServiceId == Guid.Empty || !serviceInput.UsedAt.HasValue || serviceInput.TotalPrice < 0)
                    {
                        await transaction.RollbackAsync(CancellationToken.None);
                        return FailAction("Dữ liệu dịch vụ sử dụng không hợp lệ.");
                    }

                    if (serviceInput.UsedAt.Value < stayStart || serviceInput.UsedAt.Value > now)
                    {
                        await transaction.RollbackAsync(CancellationToken.None);
                        return FailAction("Thời gian sử dụng dịch vụ phải nằm trong khoảng thời gian khách ở và không được lớn hơn thời điểm hiện tại.");
                    }

                    var isExisting = serviceInput.Id.HasValue;
                    Models.BookingRoomService entity;

                    if (isExisting)
                    {
                        if (!existingServiceMap.TryGetValue(serviceInput.Id!.Value, out entity!) ||
                            entity.BookingRoomId != bookingRoom.Id)
                        {
                            await transaction.RollbackAsync(CancellationToken.None);
                            return FailAction("Dịch vụ cũ không thuộc BookingRoom tương ứng.");
                        }

                        if (!submittedServiceIdsForRoom.Add(entity.Id))
                        {
                            await transaction.RollbackAsync(CancellationToken.None);
                            return FailAction("Không được gửi trùng cùng một dịch vụ đã sử dụng.");
                        }

                        if (!serviceMap.TryGetValue(serviceInput.ServiceId, out var selectedService))
                        {
                            // Cho phép giữ nguyên dịch vụ cũ đã ngừng kinh doanh, nhưng không cho đổi sang dịch vụ đã ngừng/xóa.
                            if (entity.ServiceId != serviceInput.ServiceId)
                            {
                                await transaction.RollbackAsync(CancellationToken.None);
                                return FailAction("Dịch vụ được chọn không còn kinh doanh hoặc đã bị xóa.");
                            }
                        }
                        else if (!selectedService.IsActive && entity.ServiceId != serviceInput.ServiceId)
                        {
                            await transaction.RollbackAsync(CancellationToken.None);
                            return FailAction("Dịch vụ được chọn hiện đã ngừng kinh doanh.");
                        }

                        entity.ServiceId = serviceInput.ServiceId;
                        entity.TotalPrice = Math.Round(serviceInput.TotalPrice, 2, MidpointRounding.AwayFromZero);
                        entity.UsedAt = serviceInput.UsedAt.Value;
                        entity.Note = NormalizeOptional(serviceInput.Note);
                        entity.LastModifiedBy = currentUserId;
                        entity.LastModifiedAt = now;
                    }
                    else
                    {
                        if (!serviceMap.TryGetValue(serviceInput.ServiceId, out var newService) || !newService.IsActive)
                        {
                            await transaction.RollbackAsync(CancellationToken.None);
                            return FailAction("Dịch vụ mới chọn phải đang kinh doanh và chưa bị xóa.");
                        }

                        entity = new Models.BookingRoomService
                        {
                            Id = Guid.NewGuid(),
                            BookingRoomId = bookingRoom.Id,
                            ServiceId = serviceInput.ServiceId,
                            TotalPrice = Math.Round(serviceInput.TotalPrice, 2, MidpointRounding.AwayFromZero),
                            UsedAt = serviceInput.UsedAt.Value,
                            Note = NormalizeOptional(serviceInput.Note),
                            IsDeleted = false,
                            CreatedBy = currentUserId,
                            CreatedAt = now,
                            LastModifiedBy = currentUserId,
                            LastModifiedAt = now
                        };

                        await _context.BookingRoomServices.AddAsync(entity, cancellationToken);
                        submittedServiceIdsForRoom.Add(entity.Id);
                    }

                    serviceTotal += entity.TotalPrice;
                    serviceTotalByBookingRoom[bookingRoom.Id] += entity.TotalPrice;
                }

                foreach (var existingService in existingServices.Where(x => x.BookingRoomId == bookingRoom.Id))
                {
                    if (!submittedServiceIdsForRoom.Contains(existingService.Id))
                    {
                        existingService.IsDeleted = true;
                        existingService.LastModifiedBy = currentUserId;
                        existingService.LastModifiedAt = now;
                    }
                }

                var submittedFeeIdsForRoom = new HashSet<Guid>();
                foreach (var feeInput in roomInput.FeeInputs ?? new List<BookingCheckOutFeeInputViewModel>())
                {
                    if (string.IsNullOrWhiteSpace(feeInput.FeeName) || feeInput.TotalFee <= 0 || feeInput.FeeType > 2)
                    {
                        await transaction.RollbackAsync(CancellationToken.None);
                        return FailAction("Dữ liệu phí phát sinh không hợp lệ.");
                    }

                    Models.AdditionalFeeRoom entity;
                    if (feeInput.Id.HasValue)
                    {
                        if (!existingFeeMap.TryGetValue(feeInput.Id.Value, out entity!) ||
                            entity.BookingRoomId != bookingRoom.Id)
                        {
                            await transaction.RollbackAsync(CancellationToken.None);
                            return FailAction("Phí phát sinh cũ không thuộc BookingRoom tương ứng.");
                        }

                        if (!submittedFeeIdsForRoom.Add(entity.Id))
                        {
                            await transaction.RollbackAsync(CancellationToken.None);
                            return FailAction("Không được gửi trùng cùng một phí phát sinh.");
                        }

                        entity.FeeName = feeInput.FeeName.Trim();
                        entity.FeeType = feeInput.FeeType;
                        entity.TotalFee = Math.Round(feeInput.TotalFee, 2, MidpointRounding.AwayFromZero);
                        entity.Decription = NormalizeOptional(feeInput.Description);
                        entity.LastModifiedBy = currentUserId;
                        entity.LastModifiedAt = now;
                    }
                    else
                    {
                        entity = new Models.AdditionalFeeRoom
                        {
                            Id = Guid.NewGuid(),
                            BookingRoomId = bookingRoom.Id,
                            FeeName = feeInput.FeeName.Trim(),
                            FeeType = feeInput.FeeType,
                            TotalFee = Math.Round(feeInput.TotalFee, 2, MidpointRounding.AwayFromZero),
                            Decription = NormalizeOptional(feeInput.Description),
                            IsDeleted = false,
                            CreatedBy = currentUserId,
                            CreatedAt = now,
                            LastModifiedBy = currentUserId,
                            LastModifiedAt = now
                        };

                        await _context.AdditionalFeeRooms.AddAsync(entity, cancellationToken);
                        submittedFeeIdsForRoom.Add(entity.Id);
                    }

                    feeTotal += entity.TotalFee;
                    feeTotalByBookingRoom[bookingRoom.Id] += entity.TotalFee;
                }

                foreach (var existingFee in existingFees.Where(x => x.BookingRoomId == bookingRoom.Id))
                {
                    if (!submittedFeeIdsForRoom.Contains(existingFee.Id))
                    {
                        existingFee.IsDeleted = true;
                        existingFee.LastModifiedBy = currentUserId;
                        existingFee.LastModifiedAt = now;
                    }
                }

                if (bookingRoom.Room is not null)
                {
                    bookingRoom.Room.Status = roomInput.NeedsMaintenance ? (byte)4 : (byte)2;
                    if (roomInput.NeedsMaintenance)
                    {
                        bookingRoom.Room.IsActive = false;
                    }

                    bookingRoom.Room.LastModifiedBy = currentUserId;
                    bookingRoom.Room.LastModifiedAt = now;
                }
            }

            var roomTotal = roomChargeMap.Values.Sum();
            var totalAmount = Math.Round(roomTotal + serviceTotal + feeTotal, 2, MidpointRounding.AwayFromZero);
            var finalAmount = Math.Max(
                0m,
                Math.Round(totalAmount - booking.DepositAmount, 2, MidpointRounding.AwayFromZero));

            var billStatus = model.PaymentReceived ? (byte)0 : (byte)1;
            var billPaymentMethod = model.PaymentMethod.HasValue
                ? model.PaymentMethod.Value
                : (booking.PaymentMethod ?? (byte)0);
            var billReference = model.PaymentReceived && billPaymentMethod == 1
                ? NormalizeOptional(model.ReferenceCode)
                : null;

            var bill = new Models.Bill
            {
                Id = Guid.NewGuid(),
                BillCode = normalizedBillCode,
                BookingId = booking.Id,
                TotalAmount = totalAmount,
                DepositAmount = booking.DepositAmount,
                FinalAmount = finalAmount,
                PaymentMethod = billPaymentMethod,
                PaymentDate = model.PaymentReceived ? now : (DateTime?)null,
                ReferenceCode = billReference,
                Status = billStatus,
                Note = NormalizeOptional(model.BillNote) ??
                    (model.PaymentReceived
                        ? null
                        : "Thanh toán thất bại: khách hàng từ chối thanh toán hoặc không thể liên hệ."),
                IsDeleted = false,
                CreatedBy = currentUserId,
                CreatedAt = now,
                LastModifiedBy = currentUserId,
                LastModifiedAt = now
            };

            await _context.Bills.AddAsync(bill, cancellationToken);

            foreach (var bookingRoom in bookingRooms)
            {
                await _context.BillDetails.AddAsync(new Models.BillDetail
                {
                    Id = Guid.NewGuid(),
                    BillId = bill.Id,
                    BookingRoomId = bookingRoom.Id,
                    TotalPrice = Math.Round(
                        roomChargeMap[bookingRoom.Id] +
                        serviceTotalByBookingRoom[bookingRoom.Id] +
                        feeTotalByBookingRoom[bookingRoom.Id],
                        2,
                        MidpointRounding.AwayFromZero),
                    Note = null,
                    IsDeleted = false,
                    CreatedBy = currentUserId,
                    CreatedAt = now,
                    LastModifiedBy = currentUserId,
                    LastModifiedAt = now
                }, cancellationToken);
            }

            booking.Note = NormalizeOptional(model.BookingNote);
            booking.ActualCheckOutAt = now;
            booking.Status = model.PaymentReceived ? (byte)3 : (byte)6;
            booking.LastModifiedBy = currentUserId;
            booking.LastModifiedAt = now;

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return SuccessAction();
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            return FailAction("Không thể tạo hóa đơn và hoàn tất check out. Vui lòng thử lại.");
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            return FailAction("Không thể thực hiện check out. Vui lòng thử lại.");
        }
    }

    private async Task ValidateEditServicesAsync(
        int roomIndex,
        Models.Booking booking,
        Models.BookingRoom bookingRoom,
        IReadOnlyList<BookingEditServiceItemViewModel> items,
        IDictionary<string, string> errors,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var postedIds = new HashSet<Guid>();
        var postedServiceIds = items
            .Where(x => x is not null && !x.IsDeleted && x.ServiceId != Guid.Empty)
            .Select(x => x.ServiceId)
            .Distinct()
            .ToArray();

        var services = postedServiceIds.Length == 0
            ? new Dictionary<Guid, Models.Service>()
            : await _context.Services
                .Where(x => postedServiceIds.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id, cancellationToken);

        var existingIds = items
            .Where(x => x is not null && x.Id.HasValue && x.Id.Value != Guid.Empty)
            .Select(x => x.Id!.Value)
            .Distinct()
            .ToArray();

        var existingEntities = existingIds.Length == 0
            ? new Dictionary<Guid, Models.BookingRoomService>()
            : await _context.BookingRoomServices
                .Where(x => existingIds.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id, cancellationToken);

        var lowerUsedAt = booking.Status == 2
            ? booking.ActualCheckInAt ?? booking.ExpectedCheckInAt
            : booking.CreatedAt;

        for (var serviceIndex = 0; serviceIndex < items.Count; serviceIndex++)
        {
            var item = items[serviceIndex];
            var prefix = $"Rooms[{roomIndex}].ServiceInputs[{serviceIndex}]";

            if (item is null)
            {
                errors[$"{prefix}.ServiceId"] = "Dòng dịch vụ không hợp lệ.";
                continue;
            }

            var hasId = item.Id.HasValue && item.Id.Value != Guid.Empty;

            if (hasId)
            {
                if (!postedIds.Add(item.Id!.Value))
                {
                    errors[$"{prefix}.Id"] = "Không được gửi trùng cùng một dịch vụ đã ghi nhận.";
                    continue;
                }

                if (!existingEntities.TryGetValue(item.Id.Value, out var entity)
                    || entity.BookingRoomId != bookingRoom.Id)
                {
                    errors[$"{prefix}.Id"] = "Dịch vụ không thuộc BookingRoom này hoặc không tồn tại.";
                    continue;
                }

                if (item.IsDeleted)
                {
                    continue;
                }

                if (entity.IsDeleted)
                {
                    errors[$"{prefix}.Id"] = "Dịch vụ này đã bị xóa và không thể chỉnh sửa lại.";
                    continue;
                }
            }
            else if (item.IsDeleted)
            {
                continue;
            }

            if (item.ServiceId == Guid.Empty)
            {
                errors[$"{prefix}.ServiceId"] = "Vui lòng chọn dịch vụ.";
            }
            else if (!services.TryGetValue(item.ServiceId, out var service) || service.IsDeleted)
            {
                errors[$"{prefix}.ServiceId"] = "Dịch vụ không tồn tại hoặc đã bị xóa.";
            }
            else if (!service.IsActive)
            {
                var isExistingSameService = hasId
                    && existingEntities.TryGetValue(item.Id!.Value, out var existingEntity)
                    && existingEntity.ServiceId == service.Id;

                if (!isExistingSameService)
                {
                    errors[$"{prefix}.ServiceId"] = "Chỉ được thêm hoặc đổi sang dịch vụ đang kinh doanh.";
                }
            }

            if (!item.UsedAt.HasValue)
            {
                errors[$"{prefix}.UsedAt"] = "Vui lòng chọn thời gian sử dụng dịch vụ.";
            }
            else
            {
                var isUnchangedExistingTime = hasId
                    && existingEntities.TryGetValue(item.Id!.Value, out var existingServiceEntityForTime)
                    && existingServiceEntityForTime.UsedAt == item.UsedAt.Value;

                if (!isUnchangedExistingTime && item.UsedAt.Value < lowerUsedAt)
                {
                    errors[$"{prefix}.UsedAt"] =
                        booking.Status == 2
                            ? "Thời gian sử dụng dịch vụ không được trước thời gian check in thực tế."
                            : "Thời gian sử dụng dịch vụ không được trước thời điểm tạo Booking.";
                }

                if (!isUnchangedExistingTime && item.UsedAt.Value > now)
                {
                    errors[$"{prefix}.UsedAt"] = "Thời gian sử dụng dịch vụ không được lớn hơn thời điểm lưu thay đổi.";
                }
            }

            if (item.TotalPrice < 0)
            {
                errors[$"{prefix}.TotalPrice"] = "Thành tiền dịch vụ không được âm.";
            }

            if (!string.IsNullOrWhiteSpace(item.Note) && item.Note.Trim().Length > 255)
            {
                errors[$"{prefix}.Note"] = "Ghi chú dịch vụ không được vượt quá 255 ký tự.";
            }
        }
    }

    private async Task ValidateEditFeesAsync(
        int roomIndex,
        Models.BookingRoom bookingRoom,
        IReadOnlyList<BookingEditFeeItemViewModel> items,
        IDictionary<string, string> errors,
        CancellationToken cancellationToken)
    {
        var existingIds = items
            .Where(x => x is not null && x.Id.HasValue && x.Id.Value != Guid.Empty)
            .Select(x => x.Id!.Value)
            .Distinct()
            .ToArray();

        var existingEntities = existingIds.Length == 0
            ? new Dictionary<Guid, Models.AdditionalFeeRoom>()
            : await _context.AdditionalFeeRooms
                .Where(x => existingIds.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id, cancellationToken);

        var postedIds = new HashSet<Guid>();

        for (var feeIndex = 0; feeIndex < items.Count; feeIndex++)
        {
            var item = items[feeIndex];
            var prefix = $"Rooms[{roomIndex}].FeeInputs[{feeIndex}]";

            if (item is null)
            {
                errors[$"{prefix}.FeeName"] = "Dòng phí phát sinh không hợp lệ.";
                continue;
            }

            var hasId = item.Id.HasValue && item.Id.Value != Guid.Empty;
            if (hasId)
            {
                if (!postedIds.Add(item.Id!.Value))
                {
                    errors[$"{prefix}.Id"] = "Không được gửi trùng cùng một phí phát sinh.";
                    continue;
                }

                if (!existingEntities.TryGetValue(item.Id.Value, out var entity)
                    || entity.BookingRoomId != bookingRoom.Id)
                {
                    errors[$"{prefix}.Id"] = "Phí phát sinh không thuộc BookingRoom này hoặc không tồn tại.";
                    continue;
                }

                if (item.IsDeleted)
                {
                    continue;
                }

                if (entity.IsDeleted)
                {
                    errors[$"{prefix}.Id"] = "Phí phát sinh này đã bị xóa và không thể chỉnh sửa lại.";
                    continue;
                }
            }
            else if (item.IsDeleted)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(item.FeeName))
            {
                errors[$"{prefix}.FeeName"] = "Tên phí phát sinh là bắt buộc.";
            }
            else if (item.FeeName.Trim().Length > 100)
            {
                errors[$"{prefix}.FeeName"] = "Tên phí phát sinh không được vượt quá 100 ký tự.";
            }

            if (item.FeeType > 2)
            {
                errors[$"{prefix}.FeeType"] = "Loại phí phát sinh không hợp lệ.";
            }

            if (item.TotalFee <= 0)
            {
                errors[$"{prefix}.TotalFee"] = "Thành tiền phí phát sinh phải lớn hơn 0.";
            }

            if (!string.IsNullOrWhiteSpace(item.Description) && item.Description.Trim().Length > 255)
            {
                errors[$"{prefix}.Description"] = "Mô tả phí phát sinh không được vượt quá 255 ký tự.";
            }
        }
    }

    private void ApplyEditServices(
        IReadOnlyList<BookingEditServiceItemViewModel> items,
        IReadOnlyList<Models.BookingRoomService> existingEntities,
        Guid bookingRoomId,
        Guid currentUserId,
        DateTime now)
    {
        var existingById = existingEntities.ToDictionary(x => x.Id);

        foreach (var item in items)
        {
            var hasId = item.Id.HasValue && item.Id.Value != Guid.Empty;

            if (hasId)
            {
                if (!existingById.TryGetValue(item.Id!.Value, out var entity))
                    continue;

                if (item.IsDeleted)
                {
                    if (!entity.IsDeleted)
                    {
                        entity.IsDeleted = true;
                        entity.LastModifiedBy = currentUserId;
                        entity.LastModifiedAt = now;
                    }

                    continue;
                }

                var changed = entity.ServiceId != item.ServiceId
                    || entity.UsedAt != item.UsedAt!.Value
                    || entity.TotalPrice != item.TotalPrice
                    || !string.Equals(entity.Note, NormalizeOptional(item.Note), StringComparison.Ordinal);

                entity.ServiceId = item.ServiceId;
                entity.UsedAt = item.UsedAt!.Value;
                entity.TotalPrice = item.TotalPrice;
                entity.Note = NormalizeOptional(item.Note);

                if (changed)
                {
                    entity.LastModifiedBy = currentUserId;
                    entity.LastModifiedAt = now;
                }

                continue;
            }

            if (item.IsDeleted)
                continue;

            _context.BookingRoomServices.Add(new Models.BookingRoomService
            {
                Id = Guid.NewGuid(),
                BookingRoomId = bookingRoomId,
                ServiceId = item.ServiceId,
                TotalPrice = item.TotalPrice,
                UsedAt = item.UsedAt!.Value,
                Note = NormalizeOptional(item.Note),
                IsDeleted = false,
                CreatedBy = currentUserId,
                CreatedAt = now,
                LastModifiedBy = currentUserId,
                LastModifiedAt = now
            });
        }
    }

    private void ApplyEditFees(
        IReadOnlyList<BookingEditFeeItemViewModel> items,
        IReadOnlyList<Models.AdditionalFeeRoom> existingEntities,
        Guid bookingRoomId,
        Guid currentUserId,
        DateTime now)
    {
        var existingById = existingEntities.ToDictionary(x => x.Id);

        foreach (var item in items)
        {
            var hasId = item.Id.HasValue && item.Id.Value != Guid.Empty;

            if (hasId)
            {
                if (!existingById.TryGetValue(item.Id!.Value, out var entity))
                    continue;

                if (item.IsDeleted)
                {
                    if (!entity.IsDeleted)
                    {
                        entity.IsDeleted = true;
                        entity.LastModifiedBy = currentUserId;
                        entity.LastModifiedAt = now;
                    }

                    continue;
                }

                var normalizedName = item.FeeName.Trim();
                var normalizedDescription = NormalizeOptional(item.Description);
                var changed = !string.Equals(entity.FeeName, normalizedName, StringComparison.Ordinal)
                    || entity.FeeType != item.FeeType
                    || entity.TotalFee != item.TotalFee
                    || !string.Equals(entity.Decription, normalizedDescription, StringComparison.Ordinal);

                entity.FeeName = normalizedName;
                entity.FeeType = item.FeeType;
                entity.TotalFee = item.TotalFee;
                entity.Decription = normalizedDescription;

                if (changed)
                {
                    entity.LastModifiedBy = currentUserId;
                    entity.LastModifiedAt = now;
                }

                continue;
            }

            if (item.IsDeleted)
                continue;

            _context.AdditionalFeeRooms.Add(new Models.AdditionalFeeRoom
            {
                Id = Guid.NewGuid(),
                BookingRoomId = bookingRoomId,
                FeeName = item.FeeName.Trim(),
                FeeType = item.FeeType,
                TotalFee = item.TotalFee,
                Decription = NormalizeOptional(item.Description),
                IsDeleted = false,
                CreatedBy = currentUserId,
                CreatedAt = now,
                LastModifiedBy = currentUserId,
                LastModifiedAt = now
            });
        }
    }

    private static Dictionary<string, string> ValidateCheckInModel(BookingCheckInViewModel? model)
    {
        var errors = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (model is null)
        {
            errors[string.Empty] = "Dữ liệu check in không hợp lệ.";
            return errors;
        }

        if (model.BookingId == Guid.Empty)
        {
            errors[nameof(BookingCheckInViewModel.BookingId)] = "Lịch đặt phòng không hợp lệ.";
        }

        // 1) Validate thông tin khách hàng trước. Nếu còn lỗi khách hàng,
        // không tiếp tục đánh giá phân người/chọn phòng trong cùng lượt submit.
        ValidateCheckInGuestFields(model, errors);
        if (string.IsNullOrWhiteSpace(model.GuestCode))
            errors[nameof(BookingCheckInViewModel.GuestCode)] = "Mã khách hàng không được để trống.";
        else if (model.GuestCode.Any(char.IsWhiteSpace))
            errors[nameof(BookingCheckInViewModel.GuestCode)] = "Mã khách hàng không được chứa dấu cách hoặc khoảng trắng.";
        else if (model.GuestCode.Trim().Length > 50)
            errors[nameof(BookingCheckInViewModel.GuestCode)] = "Mã khách hàng không được vượt quá 50 ký tự.";

        if (errors.Count > 1 || (errors.Count == 1 && !errors.ContainsKey(nameof(BookingCheckInViewModel.BookingId))))
        {
            return errors;
        }

        if (model.TotalAdults < 1)
        {
            errors[nameof(BookingCheckInViewModel.TotalAdults)] = "Tổng người lớn phải lớn hơn 0.";
        }

        if (model.TotalChildren < 0)
        {
            errors[nameof(BookingCheckInViewModel.TotalChildren)] = "Tổng trẻ em phải lớn hơn hoặc bằng 0.";
        }

        if (!string.IsNullOrWhiteSpace(model.BookingNote) && model.BookingNote.Trim().Length > 255)
        {
            errors[nameof(BookingCheckInViewModel.BookingNote)] = "Ghi chú Booking không được vượt quá 255 ký tự.";
        }

        if (model.Rooms is null || model.Rooms.Count == 0)
        {
            errors[nameof(BookingCheckInViewModel.Rooms)] = "Booking chưa có phòng để check in.";
            return errors;
        }

        var duplicateRooms = model.Rooms
            .Where(x => x.RoomId.HasValue && x.RoomId.Value != Guid.Empty)
            .GroupBy(x => x.RoomId!.Value)
            .Where(x => x.Count() > 1)
            .ToList();

        if (duplicateRooms.Count > 0)
        {
            errors[string.Empty] = "Không được chọn trùng cùng một phòng cho nhiều BookingRoom.";
        }

        var adultSum = 0L;
        var childSum = 0L;
        foreach (var room in model.Rooms)
        {
            var index = model.Rooms.IndexOf(room);
            adultSum += room.Adults;
            childSum += room.Children;

            if (room.RoomId is null || room.RoomId == Guid.Empty)
            {
                errors[$"Rooms[{index}].RoomId"] = "Vui lòng chọn phòng.";
            }
            if (room.Adults < 0)
            {
                errors[$"Rooms[{index}].Adults"] = "Người lớn không được âm.";
            }
            if (room.Children < 0)
            {
                errors[$"Rooms[{index}].Children"] = "Trẻ em không được âm.";
            }
            if (!string.IsNullOrWhiteSpace(room.Note) && room.Note.Trim().Length > 255)
            {
                errors[$"Rooms[{index}].Note"] = "Ghi chú phòng không được vượt quá 255 ký tự.";
            }
        }

        if (adultSum != model.TotalAdults || childSum != model.TotalChildren)
        {
            errors[string.Empty] =
                $"Phân bổ người chưa khớp: tổng người lớn đã phân bổ {adultSum}/{model.TotalAdults}, tổng trẻ em đã phân bổ {childSum}/{model.TotalChildren}.";

            errors.Remove(nameof(BookingCheckInViewModel.TotalAdults));
            errors.Remove(nameof(BookingCheckInViewModel.TotalChildren));
        }

        return errors;
    }

    private static void ValidateEditGuestFields(
        BookingEditGuestViewModel? guestModel,
        bool requireFullDetails,
        Dictionary<string, string> errors)
    {
        if (guestModel is null)
        {
            errors["Guest"] = "Thông tin khách hàng không hợp lệ.";
            return;
        }

        var prefix = "Guest.";

        var code = guestModel.GuestCode?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(code))
        {
            errors[$"{prefix}GuestCode"] = "Mã khách hàng không được để trống.";
        }
        else if (!IsValidAlphanumericCode(code))
        {
            errors[$"{prefix}GuestCode"] =
                "Mã khách hàng chỉ được bao gồm chữ hoa, chữ thường và số, không chứa dấu cách và ký tự đặc biệt.";
        }
        else if (code.Length > 50)
        {
            errors[$"{prefix}GuestCode"] = "Mã khách hàng không được vượt quá 50 ký tự.";
        }

        var name = guestModel.Name?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(name))
        {
            errors[$"{prefix}Name"] = "Tên khách hàng không được để trống.";
        }
        else if (name.Length > 100)
        {
            errors[$"{prefix}Name"] = "Tên khách hàng không được vượt quá 100 ký tự.";
        }

        if (guestModel.DateOfBirth.HasValue)
        {
            if (guestModel.DateOfBirth.Value.Date > DateTime.Today)
            {
                errors[$"{prefix}DateOfBirth"] = "Ngày sinh không được lớn hơn ngày hiện tại.";
            }
        }
        else if (requireFullDetails)
        {
            errors[$"{prefix}DateOfBirth"] =
                "Ngày sinh là bắt buộc khi Booking đang ở trạng thái Đang ở.";
        }

        if (guestModel.Sex.HasValue)
        {
            if (guestModel.Sex is not 0 and not 1 and not 2)
            {
                errors[$"{prefix}Sex"] = "Giới tính khách hàng không hợp lệ.";
            }
        }
        else if (requireFullDetails)
        {
            errors[$"{prefix}Sex"] =
                "Giới tính là bắt buộc khi Booking đang ở trạng thái Đang ở.";
        }

        var address = guestModel.Address?.Trim();
        if (requireFullDetails && string.IsNullOrWhiteSpace(address))
        {
            errors[$"{prefix}Address"] =
                "Địa chỉ là bắt buộc khi Booking đang ở trạng thái Đang ở.";
        }
        else if (!string.IsNullOrWhiteSpace(address) && address.Length > 255)
        {
            errors[$"{prefix}Address"] = "Địa chỉ không được vượt quá 255 ký tự.";
        }

        var phone = guestModel.PhoneNumber?.Trim();
        var email = guestModel.Email?.Trim();

        if (requireFullDetails)
        {
            // Booking đang ở trạng thái Đang ở:
            // Số điện thoại bắt buộc, Email không bắt buộc.
            if (string.IsNullOrWhiteSpace(phone))
            {
                errors[$"{prefix}PhoneNumber"] =
                    "Số điện thoại là bắt buộc khi Booking đang ở trạng thái Đang ở.";
            }
        }
        else
        {
            // Chờ xác nhận / Chờ check in:
            // Phải có ít nhất Số điện thoại hoặc Email.
            if (string.IsNullOrWhiteSpace(phone) &&
                string.IsNullOrWhiteSpace(email))
            {
                errors[$"{prefix}PhoneNumber"] =
                    "Khách hàng phải có ít nhất số điện thoại hoặc email.";

                errors[$"{prefix}Email"] =
                    "Khách hàng phải có ít nhất số điện thoại hoặc email.";
            }
        }

        // Nếu có số điện thoại thì luôn validate định dạng.
        if (!string.IsNullOrWhiteSpace(phone))
        {
            if (phone.Length > 30)
            {
                errors[$"{prefix}PhoneNumber"] =
                    "Số điện thoại nhập vào không được vượt quá 30 ký tự.";
            }
            else if (!TryNormalizePhoneNumber(phone, out _))
            {
                errors[$"{prefix}PhoneNumber"] =
                    "Số điện thoại không hợp lệ. Có thể nhập số quốc tế; sau khi chuẩn hóa phải có 7-15 chữ số.";
            }
        }

        // Email luôn là trường tùy chọn.
        // Nếu có nhập thì bắt buộc đúng định dạng.
        if (!string.IsNullOrWhiteSpace(email)
            && (email.Length > 254 || !new EmailAddressAttribute().IsValid(email)))
        {
            errors[$"{prefix}Email"] =
                "Email không đúng định dạng hoặc vượt quá 254 ký tự.";
        }

        ValidateIdentityNumber(
            guestModel.IdentityNumber,
            guestModel.IdentityType,
            isRequired: requireFullDetails,
            identityFieldName: $"{prefix}IdentityNumber",
            identityTypeFieldName: $"{prefix}IdentityType",
            errors);

        var note = guestModel.Note?.Trim();
        if (!string.IsNullOrWhiteSpace(note) && note.Length > 255)
        {
            errors[$"{prefix}Note"] = "Ghi chú khách hàng không được vượt quá 255 ký tự.";
        }
    }

    private static void ApplyEditGuestFields(
        Models.Guest guest,
        BookingEditGuestViewModel guestModel,
        Guid currentUserId,
        DateTime now)
    {
        guest.GuestCode = guestModel.GuestCode.Trim();
        guest.Name = guestModel.Name.Trim();
        guest.DateOfBirth = guestModel.DateOfBirth?.Date;
        guest.Sex = guestModel.Sex;
        guest.Address = NormalizeOptional(guestModel.Address);
        guest.PhoneNumber = NormalizePhoneNumberForStorage(guestModel.PhoneNumber);
        guest.Email = NormalizeOptional(guestModel.Email);
        guest.IdentityNumber = NormalizeOptional(guestModel.IdentityNumber);
        guest.IdentityType = guestModel.IdentityType;
        guest.Note = NormalizeOptional(guestModel.Note);
        guest.LastModifiedBy = currentUserId;
        guest.LastModifiedAt = now;
    }

    private static void ValidateCheckInGuestFields(
        BookingCheckInViewModel model,
        Dictionary<string, string> errors)
    {
        if (string.IsNullOrWhiteSpace(model.GuestName) || model.GuestName.Trim().Length > 100)
            errors[nameof(BookingCheckInViewModel.GuestName)] = "Tên khách hàng bắt buộc và tối đa 100 ký tự.";

        if (!model.GuestDateOfBirth.HasValue)
            errors[nameof(BookingCheckInViewModel.GuestDateOfBirth)] = "Ngày sinh là bắt buộc.";
        else if (model.GuestDateOfBirth.Value.Date > DateTime.Today)
            errors[nameof(BookingCheckInViewModel.GuestDateOfBirth)] = "Ngày sinh không được lớn hơn ngày hiện tại.";

        if (model.GuestSex is not 0 and not 1 and not 2)
            errors[nameof(BookingCheckInViewModel.GuestSex)] = "Giới tính là bắt buộc.";

        if (string.IsNullOrWhiteSpace(model.GuestAddress) || model.GuestAddress.Trim().Length > 255)
            errors[nameof(BookingCheckInViewModel.GuestAddress)] = "Địa chỉ bắt buộc và tối đa 255 ký tự.";

        if (string.IsNullOrWhiteSpace(model.GuestPhoneNumber) || model.GuestPhoneNumber.Trim().Length > 20)
            errors[nameof(BookingCheckInViewModel.GuestPhoneNumber)] = "Số điện thoại bắt buộc và tối đa 20 ký tự.";

        if (!string.IsNullOrWhiteSpace(model.GuestEmail) &&
            (model.GuestEmail.Trim().Length > 254 || !new EmailAddressAttribute().IsValid(model.GuestEmail.Trim())))
            errors[nameof(BookingCheckInViewModel.GuestEmail)] = "Email không hợp lệ.";

        if (string.IsNullOrWhiteSpace(model.GuestIdentityNumber) || model.GuestIdentityNumber.Trim().Length > 50)
            errors[nameof(BookingCheckInViewModel.GuestIdentityNumber)] = "Số giấy tờ bắt buộc và tối đa 50 ký tự.";

        if (model.GuestIdentityType is not 0 and not 1 and not 2)
            errors[nameof(BookingCheckInViewModel.GuestIdentityType)] = "Loại giấy tờ là bắt buộc.";

        if (!string.IsNullOrWhiteSpace(model.GuestNote) && model.GuestNote.Trim().Length > 255)
            errors[nameof(BookingCheckInViewModel.GuestNote)] = "Ghi chú khách hàng không được vượt quá 255 ký tự.";
    }

    private static Dictionary<string, string> ValidateCheckInAgainstDatabase(
        BookingCheckInViewModel model,
        Models.Booking booking,
        List<Models.BookingRoom> bookingRooms)
    {
        var errors = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (bookingRooms.Count != model.Rooms.Count)
        {
            errors[string.Empty] = "Số lượng BookingRoom đã thay đổi. Vui lòng tải lại màn hình check in.";
            return errors;
        }

        var byId = model.Rooms.GroupBy(x => x.BookingRoomId).ToDictionary(x => x.Key, x => x.First());
        if (byId.Count != model.Rooms.Count || bookingRooms.Any(x => !byId.ContainsKey(x.Id)))
        {
            errors[string.Empty] = "Danh sách BookingRoom không còn đồng bộ. Vui lòng tải lại màn hình check in.";
            return errors;
        }

        foreach (var bookingRoom in bookingRooms)
        {
            var input = byId[bookingRoom.Id]!;
            if (!input.RoomId.HasValue)
                continue;

            if (input.Adults > bookingRoom.RoomType.MaxAdults)
                errors[$"Rooms[{model.Rooms.IndexOf(input)}].Adults"] = $"Số người lớn vượt sức chứa tối đa của loại phòng {bookingRoom.RoomType.RoomTypeCode}.";
            if (input.Children > bookingRoom.RoomType.MaxChildren)
                errors[$"Rooms[{model.Rooms.IndexOf(input)}].Children"] = $"Số trẻ em vượt sức chứa tối đa của loại phòng {bookingRoom.RoomType.RoomTypeCode}.";
        }

        return errors;
    }

    private static Dictionary<string, string> ValidateCheckOutModel(
        BookingCheckOutViewModel? model,
        DateTime stayStart,
        DateTime now)
    {
        var errors = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (model is null || model.BookingId == Guid.Empty)
        {
            errors[string.Empty] = "Dữ liệu check out không hợp lệ.";
            return errors;
        }

        if (model.PaymentReceived)
        {
            if (model.PaymentMethod is not 0 and not 1)
                errors[nameof(BookingCheckOutViewModel.PaymentMethod)] = "Vui lòng chọn phương thức thanh toán.";
            if (model.PaymentMethod == 1 && string.IsNullOrWhiteSpace(model.ReferenceCode))
                errors[nameof(BookingCheckOutViewModel.ReferenceCode)] = "Mã giao dịch là bắt buộc khi thanh toán chuyển khoản.";
        }

        if (!string.IsNullOrWhiteSpace(model.ReferenceCode) && model.ReferenceCode.Trim().Length > 100)
            errors[nameof(BookingCheckOutViewModel.ReferenceCode)] = "Mã giao dịch không được vượt quá 100 ký tự.";

        var guestCode = model.GuestCode?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(guestCode))
            errors[nameof(BookingCheckOutViewModel.GuestCode)] = "Mã khách hàng không được để trống.";
        else if (model.GuestCode!.Any(char.IsWhiteSpace))
            errors[nameof(BookingCheckOutViewModel.GuestCode)] = "Mã khách hàng không được chứa dấu cách hoặc khoảng trắng.";
        else if (guestCode.Length > 50)
            errors[nameof(BookingCheckOutViewModel.GuestCode)] = "Mã khách hàng không được vượt quá 50 ký tự.";

        if (string.IsNullOrWhiteSpace(model.GuestName) || model.GuestName.Trim().Length > 100)
            errors[nameof(BookingCheckOutViewModel.GuestName)] = "Tên khách hàng bắt buộc và tối đa 100 ký tự.";

        if (!model.GuestDateOfBirth.HasValue)
            errors[nameof(BookingCheckOutViewModel.GuestDateOfBirth)] = "Ngày sinh là bắt buộc.";
        else if (model.GuestDateOfBirth.Value.Date > DateTime.Today)
            errors[nameof(BookingCheckOutViewModel.GuestDateOfBirth)] = "Ngày sinh không được lớn hơn ngày hiện tại.";

        if (model.GuestSex is not 0 and not 1 and not 2)
            errors[nameof(BookingCheckOutViewModel.GuestSex)] = "Giới tính là bắt buộc.";

        if (string.IsNullOrWhiteSpace(model.GuestAddress) || model.GuestAddress.Trim().Length > 255)
            errors[nameof(BookingCheckOutViewModel.GuestAddress)] = "Địa chỉ bắt buộc và tối đa 255 ký tự.";

        var guestPhone = model.GuestPhoneNumber?.Trim();
        var guestEmail = model.GuestEmail?.Trim();
        if (string.IsNullOrWhiteSpace(guestPhone) && string.IsNullOrWhiteSpace(guestEmail))
        {
            errors[nameof(BookingCheckOutViewModel.GuestPhoneNumber)] = "Phải nhập ít nhất số điện thoại hoặc email.";
            errors[nameof(BookingCheckOutViewModel.GuestEmail)] = "Phải nhập ít nhất số điện thoại hoặc email.";
        }
        if (!string.IsNullOrWhiteSpace(guestPhone) && guestPhone.Length > 20)
            errors[nameof(BookingCheckOutViewModel.GuestPhoneNumber)] = "Số điện thoại không được vượt quá 20 ký tự.";
        if (!string.IsNullOrWhiteSpace(guestEmail) &&
            (guestEmail.Length > 4000 || !new EmailAddressAttribute().IsValid(guestEmail)))
            errors[nameof(BookingCheckOutViewModel.GuestEmail)] = "Email không hợp lệ.";

        if (string.IsNullOrWhiteSpace(model.GuestIdentityNumber) || model.GuestIdentityNumber.Trim().Length > 50)
            errors[nameof(BookingCheckOutViewModel.GuestIdentityNumber)] = "Số giấy tờ bắt buộc và tối đa 50 ký tự.";
        if (model.GuestIdentityType is not 0 and not 1 and not 2)
            errors[nameof(BookingCheckOutViewModel.GuestIdentityType)] = "Loại giấy tờ là bắt buộc.";
        if (!string.IsNullOrWhiteSpace(model.GuestNote) && model.GuestNote.Trim().Length > 255)
            errors[nameof(BookingCheckOutViewModel.GuestNote)] = "Ghi chú khách hàng không được vượt quá 255 ký tự.";

        var billCode = model.BillCode?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(billCode))
            errors[nameof(BookingCheckOutViewModel.BillCode)] = "Mã hóa đơn không được để trống.";
        else if (model.BillCode!.Any(char.IsWhiteSpace))
            errors[nameof(BookingCheckOutViewModel.BillCode)] = "Mã hóa đơn không được chứa dấu cách hoặc khoảng trắng.";
        else if (billCode.Length > 50)
            errors[nameof(BookingCheckOutViewModel.BillCode)] = "Mã hóa đơn không được vượt quá 50 ký tự.";

        if (!string.IsNullOrWhiteSpace(model.BillNote) && model.BillNote.Trim().Length > 255)
            errors[nameof(BookingCheckOutViewModel.BillNote)] = "Ghi chú hóa đơn không được vượt quá 255 ký tự.";

        if (!string.IsNullOrWhiteSpace(model.BookingNote) && model.BookingNote.Trim().Length > 255)
            errors[nameof(BookingCheckOutViewModel.BookingNote)] = "Ghi chú Booking không được vượt quá 255 ký tự.";

        if (model.Rooms is null || model.Rooms.Count == 0)
        {
            errors[string.Empty] = "Không có BookingRoom để check out.";
            return errors;
        }

        for (var i = 0; i < model.Rooms.Count; i++)
        {
            var room = model.Rooms[i];
            foreach (var service in room.ServiceInputs)
            {
                if (service.ServiceId == Guid.Empty)
                    errors[$"Rooms[{i}].ServiceInputs"] = "Dịch vụ được chọn không hợp lệ.";
                if (!service.UsedAt.HasValue)
                    errors[$"Rooms[{i}].ServiceInputs"] = "Thời gian sử dụng dịch vụ là bắt buộc.";
                else if (service.UsedAt.Value < stayStart)
                    errors[$"Rooms[{i}].ServiceInputs"] = "Thời gian sử dụng dịch vụ không được nhỏ hơn thời gian check in thực tế.";
                else if (service.UsedAt.Value > now)
                    errors[$"Rooms[{i}].ServiceInputs"] = "Thời gian sử dụng dịch vụ không được lớn hơn thời điểm submit.";
                if (service.TotalPrice < 0)
                    errors[$"Rooms[{i}].ServiceInputs"] = "Thành tiền dịch vụ không được âm.";
                if (!string.IsNullOrWhiteSpace(service.Note) && service.Note.Trim().Length > 255)
                    errors[$"Rooms[{i}].ServiceInputs"] = "Ghi chú dịch vụ không được vượt quá 255 ký tự.";
            }

            foreach (var fee in room.FeeInputs)
            {
                if (string.IsNullOrWhiteSpace(fee.FeeName))
                    errors[$"Rooms[{i}].FeeInputs"] = "Tên phí phát sinh là bắt buộc.";
                else if (fee.FeeName.Trim().Length > 100)
                    errors[$"Rooms[{i}].FeeInputs"] = "Tên phí phát sinh không được vượt quá 100 ký tự.";
                if (fee.FeeType > 2)
                    errors[$"Rooms[{i}].FeeInputs"] = "Loại phí phát sinh không hợp lệ.";
                if (fee.TotalFee <= 0)
                    errors[$"Rooms[{i}].FeeInputs"] = "Thành tiền phí phát sinh phải lớn hơn 0.";
                if (!string.IsNullOrWhiteSpace(fee.Description) && fee.Description.Trim().Length > 255)
                    errors[$"Rooms[{i}].FeeInputs"] = "Mô tả phí phát sinh không được vượt quá 255 ký tự.";
            }
        }

        return errors;
    }

    private static BookingActionResult SuccessAction() => new() { Succeeded = true };
    private static bool IsValidAlphanumericCode(string value)
    {
        return !string.IsNullOrWhiteSpace(value) &&
               Regex.IsMatch(value, "^[A-Za-z0-9]+$");
    }

    private static void ValidateCodeValue(
        string? raw,
        string label,
        string fieldName,
        Dictionary<string, string> errors)
    {
        var value = raw?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(value))
        {
            errors[fieldName] = $"{label} không được để trống.";
        }
        else if (!IsValidAlphanumericCode(value))
        {
            errors[fieldName] =
                $"{label} chỉ được bao gồm chữ hoa, chữ thường và số, không chứa dấu cách và ký tự đặc biệt.";
        }
    }

    private static bool TryNormalizePhoneNumber(string raw, out string normalized)
    {
        normalized = string.Empty;
        var value = raw?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        value = Regex.Replace(value, @"[\s\-\.\(\)]", string.Empty);

        if (value.StartsWith("00", StringComparison.Ordinal))
        {
            value = "+" + value[2..];
        }

        if (value.StartsWith("+", StringComparison.Ordinal))
        {
            var digits = value[1..];
            if (!Regex.IsMatch(digits, @"^[0-9]+$") ||
                digits.Length < 7 ||
                digits.Length > 15 ||
                digits.StartsWith("0", StringComparison.Ordinal))
            {
                return false;
            }

            normalized = "+" + digits;
            return true;
        }

        if (!Regex.IsMatch(value, @"^[0-9]+$") || value.Length < 7 || value.Length > 15)
        {
            return false;
        }

        normalized = value;
        return true;
    }

    private static string? NormalizePhoneNumberForStorage(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        return TryNormalizePhoneNumber(raw, out var normalized)
            ? normalized
            : raw.Trim();
    }

    private static void ValidateIdentityNumber(
        string? rawNumber,
        byte? identityType,
        bool isRequired,
        string identityFieldName,
        string identityTypeFieldName,
        Dictionary<string, string> errors)
    {
        var number = rawNumber?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(number))
        {
            if (isRequired)
            {
                errors[identityFieldName] = "Số giấy tờ là bắt buộc.";
            }
            return;
        }

        if (number.Length > 50)
        {
            errors[identityFieldName] = "Số giấy tờ không được vượt quá 50 ký tự.";
            return;
        }

        if (identityType is not 0 and not 1 and not 2)
        {
            errors[identityTypeFieldName] = "Vui lòng chọn loại giấy tờ khi nhập số giấy tờ.";
            return;
        }

        switch (identityType)
        {
            case 0:
                if (!Regex.IsMatch(number, @"^\d{12}$"))
                {
                    errors[identityFieldName] = "CCCD phải gồm đúng 12 chữ số.";
                }
                break;
            case 1:
                if (!Regex.IsMatch(number, @"^\d{12}$"))
                {
                    errors[identityFieldName] = "Giấy phép lái xe phải gồm đúng 12 chữ số.";
                }
                break;
            case 2:
                if (!Regex.IsMatch(number, @"^[A-Za-z0-9]{5,20}$"))
                {
                    errors[identityFieldName] =
                        "Hộ chiếu phải gồm 5-20 ký tự chữ và số, không chứa dấu cách hoặc ký tự đặc biệt.";
                }
                break;
        }
    }


    private static BookingActionResult FailAction(string message, string? field = null)
    {
        return new BookingActionResult
        {
            Succeeded = false,
            ErrorMessage = message,
            FieldErrors = string.IsNullOrWhiteSpace(field)
                ? new Dictionary<string, string>()
                : new Dictionary<string, string> { [field!] = message }
        };
    }

    private async Task<string> GetNextBillCodeAsync(CancellationToken cancellationToken)
    {
        var codes = await _context.Bills
            .AsNoTracking()
            .Select(x => x.BillCode)
            .ToListAsync(cancellationToken);

        var maxNumber = codes
            .Select(code => code?.Trim() ?? string.Empty)
            .Where(code => code.StartsWith("HD", StringComparison.OrdinalIgnoreCase))
            .Select(code => code.Length > 2 && int.TryParse(code[2..], out var value) ? value : 0)
            .DefaultIfEmpty(0)
            .Max();

        return $"HD{maxNumber + 1}";
    }

    public async Task<BookingCreateViewModel> GetCreateModelAsync(
        CancellationToken cancellationToken = default)
    {
        var settings = await GetHotelSettingAsync(cancellationToken);
        var today = DateTime.Today;
        var model = new BookingCreateViewModel
        {
            BookingType = 0,
            CheckInDate = today,
            CheckOutDate = today.AddDays(1)
        };

        model = await NormalizeCreateModelAsync(model, settings, cancellationToken);
        model.Step = 1;
        await PopulateCreateReferenceDataAsync(model, cancellationToken);
        return model;
    }

    public async Task<BookingCreateValidationResult> ValidateCreateStepAsync(
        BookingCreateViewModel model,
        CancellationToken cancellationToken = default)
    {
        var errors = new Dictionary<string, string>();

        if (model is null)
        {
            return new BookingCreateValidationResult
            {
                Succeeded = false,
                ErrorMessage = "Dữ liệu bước 1 không hợp lệ.",
                Model = await GetCreateModelAsync(cancellationToken)
            };
        }

        var settings = await GetHotelSettingAsync(cancellationToken);
        var normalized = await NormalizeCreateModelAsync(model, settings, cancellationToken);

        if (normalized.CheckInDate == default || normalized.CheckOutDate == default)
        {
            errors[nameof(BookingCreateViewModel.CheckInDate)] = "Vui lòng nhập ngày check in dự kiến.";
            errors[nameof(BookingCreateViewModel.CheckOutDate)] = "Vui lòng nhập ngày check out dự kiến.";
        }
        else
        {
            var today = DateTime.Today;

            if (normalized.CheckInDate.Date < today)
            {
                errors[nameof(BookingCreateViewModel.CheckInDate)] = "Ngày check in dự kiến không được trước ngày hiện tại.";
            }

            if (normalized.CheckOutDate.Date <= normalized.CheckInDate.Date)
            {
                errors[nameof(BookingCreateViewModel.CheckOutDate)] = "Ngày check out phải sau ngày check in.";
            }

        }

        var selections = normalized.RoomSelections.ToList();
        var selected = selections
            .Select((x, index) => new { Item = x, Index = index })
            .Where(x => x.Item.Quantity > 0)
            .ToList();

        if (selected.Count == 0)
        {
            errors[string.Empty] = "Vui lòng chọn ít nhất một loại phòng và số lượng phòng lớn hơn 0.";
        }

        if (normalized.TotalAdults < 1)
        {
            errors[nameof(BookingCreateViewModel.TotalAdults)] = "Tổng người lớn phải lớn hơn 0.";
        }

        if (normalized.TotalChildren < 0)
        {
            errors[nameof(BookingCreateViewModel.TotalChildren)] = "Tổng trẻ em phải lớn hơn hoặc bằng 0.";
        }

        var availabilityIssues = new List<string>();

        foreach (var row in selections.Select((item, index) => new { item, index }))
        {
            if (row.item.Quantity < 0)
            {
                errors[$"RoomSelections[{row.index}].Quantity"] = "Số lượng phòng không được âm.";
                continue;
            }

            if (row.item.Quantity <= 0)
            {
                continue;
            }

            if (row.item.Quantity > row.item.AvailableRoomCount)
            {
                var detail =
                    $"Loại phòng {row.item.RoomTypeCode}-{row.item.RoomTypeName} chỉ còn {row.item.AvailableRoomCount} phòng có thể đặt trong khoảng thời gian này";

                availabilityIssues.Add(detail);
                errors[$"RoomSelections[{row.index}].Quantity"] = $"{detail}.";
            }
        }

        if (availabilityIssues.Count > 0)
        {
            errors[string.Empty] = string.Join("; ", availabilityIssues) + ".";
        }

        if (errors.Count == 0)
        {
            var maxAdultsTotal = selections
                .Where(x => x.Quantity > 0)
                .Sum(x => (long)x.MaxAdults * x.Quantity);

            var maxChildrenTotal = selections
                .Where(x => x.Quantity > 0)
                .Sum(x => (long)x.MaxChildren * x.Quantity);

            if (normalized.TotalAdults > maxAdultsTotal)
            {
                errors[nameof(BookingCreateViewModel.TotalAdults)] =
                    $"Tổng người lớn vượt sức chứa tối đa của số phòng đã chọn ({maxAdultsTotal} người).";
            }

            if (normalized.TotalChildren > maxChildrenTotal)
            {
                errors[nameof(BookingCreateViewModel.TotalChildren)] =
                    $"Tổng trẻ em vượt sức chứa tối đa của số phòng đã chọn ({maxChildrenTotal} trẻ em).";
            }
        }

        var availabilitySucceeded = errors.Count == 0;

        if (availabilitySucceeded)
        {
            normalized.Step = 2;
            normalized.AvailabilitySucceeded = true;
            normalized.AvailabilityMessage =
                $"Có thể tạo lịch đặt phòng cho {selected.Sum(x => x.Item.Quantity)} phòng trong {normalized.Nights:N0} đêm.";
        }
        else
        {
            normalized.Step = 1;
            normalized.AvailabilitySucceeded = false;
            normalized.AvailabilityMessage = availabilityIssues.Count > 0
                ? string.Join("; ", availabilityIssues) + "."
                : "";
        }

        await PopulateCreateReferenceDataAsync(normalized, cancellationToken);

        return new BookingCreateValidationResult
        {
            Succeeded = availabilitySucceeded,
            ErrorMessage = null,
            FieldErrors = errors,
            Model = normalized
        };
    }

    public async Task<BookingCreateValidationResult> CreateAsync(
        BookingCreateViewModel model,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        var errors = new Dictionary<string, string>();

        if (currentUserId == Guid.Empty)
        {
            return new BookingCreateValidationResult
            {
                Succeeded = false,
                ErrorMessage = "Không xác định được người dùng đang đăng nhập.",
                Model = model ?? await GetCreateModelAsync(cancellationToken)
            };
        }

        if (model is null)
        {
            return new BookingCreateValidationResult
            {
                Succeeded = false,
                ErrorMessage = "Dữ liệu đặt phòng không hợp lệ.",
                Model = await GetCreateModelAsync(cancellationToken)
            };
        }

        var settings = await GetHotelSettingAsync(cancellationToken);
        var normalized = await NormalizeCreateModelAsync(model, settings, cancellationToken);
        normalized.Step = 2;

        AddStepTwoValidationErrors(normalized, errors);

        if (errors.Count > 0)
        {
            await PopulateCreateReferenceDataAsync(normalized, cancellationToken);
            return new BookingCreateValidationResult
            {
                Succeeded = false,
                ErrorMessage = "Vui lòng kiểm tra lại các thông tin chưa hợp lệ.",
                FieldErrors = errors,
                Model = normalized
            };
        }

        await using var transaction = await _context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        try
        {
            // Tính lại lần cuối trong cùng transaction để chống thay đổi tồn phòng/code
            // giữa lúc nhân viên xem bước 2 và lúc bấm xác nhận.
            var finalCheck = await NormalizeCreateModelAsync(normalized, settings, cancellationToken);
            finalCheck.Step = 2;

            // Kiểm tra lại toàn bộ điều kiện Bước 1 trước khi phân bổ người.
            // Không phân bổ trước validation để tránh overflow nếu dữ liệu POST bị giả mạo.
            var finalStepErrors = new Dictionary<string, string>();
            ValidateAvailabilityOnly(finalCheck, finalStepErrors);

            if (finalStepErrors.Count > 0)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                finalCheck.AvailabilitySucceeded = false;
                finalCheck.AvailabilityMessage = "Dữ liệu phòng đã thay đổi; lịch đặt phòng không còn khả dụng.";
                await PopulateCreateReferenceDataAsync(finalCheck, cancellationToken);
                return new BookingCreateValidationResult
                {
                    Succeeded = false,
                    ErrorMessage = "Không thể tạo lịch đặt phòng vì khả năng phòng đã thay đổi. Vui lòng kiểm tra lại bước 1.",
                    FieldErrors = finalStepErrors,
                    Model = finalCheck
                };
            }

            var bookingCode = finalCheck.BookingCode.Trim();
            if (await _context.Bookings.AnyAsync(
                    x => x.BookingCode == bookingCode,
                    cancellationToken))
            {
                errors[nameof(BookingCreateViewModel.BookingCode)] = "Mã đặt phòng đã tồn tại.";
                await transaction.RollbackAsync(CancellationToken.None);
                await PopulateCreateReferenceDataAsync(finalCheck, cancellationToken);
                return new BookingCreateValidationResult
                {
                    Succeeded = false,
                    ErrorMessage = "Mã đặt phòng đã tồn tại. Vui lòng dùng mã khác.",
                    FieldErrors = errors,
                    Model = finalCheck
                };
            }

            var guestCode = finalCheck.GuestCode.Trim();
            if (await _context.Guests.AnyAsync(
                    x => !x.IsDeleted && x.GuestCode == guestCode,
                    cancellationToken))
            {
                errors[nameof(BookingCreateViewModel.GuestCode)] = "Mã khách hàng đã tồn tại.";
            }

            Models.Guest? guest = null;
            if (errors.Count == 0)
            {
                // Quy ước hiện tại: mỗi Booking mới luôn tạo một Guest mới.
                guest = CreateGuest(finalCheck, guestCode, currentUserId);
                await _context.Guests.AddAsync(guest, cancellationToken);
            }

            if (errors.Count > 0 || guest is null)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                await PopulateCreateReferenceDataAsync(finalCheck, cancellationToken);
                return new BookingCreateValidationResult
                {
                    Succeeded = false,
                    ErrorMessage = "Không thể lưu thông tin khách hàng. Vui lòng kiểm tra lại.",
                    FieldErrors = errors,
                    Model = finalCheck
                };
            }

            var now = DateTime.Now;
            var booking = new Models.Booking
            {
                Id = Guid.NewGuid(),
                BookingCode = bookingCode,
                GuestId = guest.Id,
                ExpectedCheckInAt = CombineDateAndTime(finalCheck.CheckInDate, finalCheck.DefaultCheckInTime),
                ExpectedCheckOutAt = CombineDateAndTime(finalCheck.CheckOutDate, finalCheck.DefaultCheckOutTime),
                TotalAdults = finalCheck.TotalAdults,
                TotalChildren = finalCheck.TotalChildren,
                DepositAmount = finalCheck.DepositAmount,
                PaymentMethod = finalCheck.DepositAmount > 0 ? finalCheck.PaymentMethod : null,
                PaymentDate = finalCheck.DepositAmount > 0 ? finalCheck.PaymentDate : null,
                ReferenceCode = finalCheck.DepositAmount > 0 && finalCheck.PaymentMethod == 1
                    ? finalCheck.ReferenceCode?.Trim()
                    : null,
                BookingType = finalCheck.BookingType,
                Status = finalCheck.BookingType == 1 ? (byte)1 : (byte)0,
                Note = NormalizeOptional(finalCheck.BookingNote),
                IsDeleted = false,
                CreatedBy = currentUserId,
                CreatedAt = now,
                LastModifiedBy = currentUserId,
                LastModifiedAt = now
            };

            await _context.Bookings.AddAsync(booking, cancellationToken);

            foreach (var selection in finalCheck.RoomSelections.Where(x => x.Quantity > 0))
            {
                for (var i = 0; i < selection.Quantity; i++)
                {
                    var bookingRoom = new Models.BookingRoom
                    {
                        Id = Guid.NewGuid(),
                        BookingId = booking.Id,
                        RoomTypeId = selection.RoomTypeId,
                        RoomId = null,
                        Adults = null,
                        Children = null,
                        Note = null,
                        IsDeleted = false,
                        CreatedBy = currentUserId,
                        CreatedAt = now,
                        LastModifiedBy = currentUserId,
                        LastModifiedAt = now
                    };

                    await _context.BookingRooms.AddAsync(bookingRoom, cancellationToken);
                }
            }

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            finalCheck.BookingCode = booking.BookingCode;
            finalCheck.BookingId = booking.Id;
            finalCheck.GuestId = guest.Id;
            finalCheck.Step = 2;

            return new BookingCreateValidationResult
            {
                Succeeded = true,
                ErrorMessage = null,
                FieldErrors = new Dictionary<string, string>(),
                Model = finalCheck
            };
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            return new BookingCreateValidationResult
            {
                Succeeded = false,
                ErrorMessage = "Không thể lưu lịch đặt phòng. Có thể mã đặt phòng/mã khách hàng đã được sử dụng hoặc dữ liệu đã thay đổi. Vui lòng thử lại.",
                FieldErrors = new Dictionary<string, string>(),
                Model = await EnsureCreateReferencesAsync(normalized, cancellationToken)
            };
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            return new BookingCreateValidationResult
            {
                Succeeded = false,
                ErrorMessage = "Không thể tạo lịch đặt phòng. Vui lòng thử lại.",
                FieldErrors = new Dictionary<string, string>(),
                Model = await EnsureCreateReferencesAsync(normalized, cancellationToken)
            };
        }
    }

    private async Task<BookingDirectCreateViewModel> BuildDirectCreateModelAsync(
        BookingDirectCreateViewModel source,
        Models.HotelSetting settings,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var checkoutDate = source.CheckOutDate == default
            ? DateTime.Today.AddDays(1)
            : source.CheckOutDate.Date;

        var expectedCheckout = checkoutDate.Add(settings.CheckOutTime.ToTimeSpan());

        var roomTypeRows = await _context.RoomTypes
            .AsNoTracking()
            .Where(x => x.IsActive && !x.IsDeleted)
            .OrderBy(x => x.RoomTypeCode)
            .Select(x => new
            {
                x.Id,
                x.RoomTypeCode,
                x.RoomTypeName,
                x.MaxAdults,
                x.MaxChildren,
                x.PricePerNight,
                x.DepositPercent
            })
            .ToListAsync(cancellationToken);

        var candidateRooms = await _context.Rooms
            .AsNoTracking()
            .Where(x => x.IsActive && !x.IsDeleted && x.Status == 0)
            .Select(x => new CheckInRoomOptionViewModel
            {
                Id = x.Id,
                RoomTypeId = x.RoomTypeId,
                RoomCode = x.RoomCode,
                RoomName = x.RoomName,
                RoomTypeCode = x.RoomType != null ? x.RoomType.RoomTypeCode : string.Empty,
                RoomTypeName = x.RoomType != null ? x.RoomType.RoomTypeName : string.Empty,
                Status = x.Status
            })
            .ToListAsync(cancellationToken);

        var bookedRoomIds = await GetOverlappingBookedRoomIdsAsync(
            now,
            expectedCheckout,
            cancellationToken);

        var availableByType = candidateRooms
            .Where(x => !bookedRoomIds.Contains(x.Id))
            .GroupBy(x => x.RoomTypeId)
            .ToDictionary(x => x.Key, x => x.OrderBy(r => r.RoomCode).ToList());

        var incomingTypes = (source.RoomTypes ?? new List<BookingDirectRoomTypeViewModel>())
            .GroupBy(x => x.RoomTypeId)
            .ToDictionary(group => group.Key, group => group.Sum(item => Math.Max(0, item.Quantity)));

        var roomTypes = roomTypeRows.Select(x =>
        {
            availableByType.TryGetValue(x.Id, out var rooms);
            return new BookingDirectRoomTypeViewModel
            {
                RoomTypeId = x.Id,
                RoomTypeCode = x.RoomTypeCode,
                RoomTypeName = x.RoomTypeName,
                MaxAdults = x.MaxAdults,
                MaxChildren = x.MaxChildren,
                PricePerNight = x.PricePerNight,
                DepositPercent = x.DepositPercent,
                AvailableRoomCount = rooms?.Count ?? 0,
                AvailableRooms = rooms ?? new List<CheckInRoomOptionViewModel>(),
                Quantity = incomingTypes.TryGetValue(x.Id, out var q) ? Math.Clamp(q, 0, 999) : 0
            };
        }).ToList();

        var incomingAssignments = (source.RoomAssignments ?? new List<BookingDirectRoomAssignmentViewModel>())
            .Select(x => new BookingDirectRoomAssignmentViewModel
            {
                RoomTypeId = x.RoomTypeId,
                RoomId = x.RoomId,
                Adults = Math.Max(0, x.Adults),
                Children = Math.Max(0, x.Children),
                Note = x.Note
            })
            .ToList();

        var assignments = new List<BookingDirectRoomAssignmentViewModel>();
        foreach (var roomType in roomTypes)
        {
            var existing = incomingAssignments.Where(x => x.RoomTypeId == roomType.RoomTypeId).ToList();
            for (var i = 0; i < roomType.Quantity; i++)
            {
                var input = i < existing.Count ? existing[i] : null;
                assignments.Add(new BookingDirectRoomAssignmentViewModel
                {
                    RoomTypeId = roomType.RoomTypeId,
                    RoomTypeCode = roomType.RoomTypeCode,
                    RoomTypeName = roomType.RoomTypeName,
                    MaxAdults = roomType.MaxAdults,
                    MaxChildren = roomType.MaxChildren,
                    RoomId = input?.RoomId,
                    Adults = input?.Adults ?? 0,
                    Children = input?.Children ?? 0,
                    Note = input?.Note,
                    AvailableRooms = roomType.AvailableRooms
                });
            }
        }

        var nights = checkoutDate > DateTime.Today ? (checkoutDate - DateTime.Today).Days : 0;
        var autoGeneratedGuestCode = await GetNextGuestCodeAsync(cancellationToken);
        var roomAmount = roomTypes
            .Where(x => x.Quantity > 0)
            .Sum(x => x.Quantity * x.PricePerNight * nights);
        var deposit = roomTypes
            .Where(x => x.Quantity > 0)
            .Sum(x => x.Quantity * x.PricePerNight * nights * x.DepositPercent / 100m);

        return new BookingDirectCreateViewModel
        {
            BookingType = 1,
            CheckInDate = DateTime.Today,
            CheckOutDate = checkoutDate,
            DefaultCheckInTime = settings.CheckInTime,
            DefaultCheckOutTime = settings.CheckOutTime,
            ExpectedCheckInAt = DateTime.Today.Date.Add(settings.CheckInTime.ToTimeSpan()),
            ExpectedCheckOutAt = expectedCheckout,
            BookingCode = string.IsNullOrWhiteSpace(source.BookingCode)
                ? await GetNextBookingCodeAsync(cancellationToken)
                : source.BookingCode.Trim(),
            BookingNote = source.BookingNote,
            TotalAdults = source.TotalAdults,
            TotalChildren = source.TotalChildren,
            GuestId = source.GuestId,
            GuestCode = string.IsNullOrWhiteSpace(source.GuestCode)
                ? autoGeneratedGuestCode
                : source.GuestCode.Trim(),
            AutoGeneratedGuestCode = autoGeneratedGuestCode,
            GuestName = source.GuestName,
            GuestDateOfBirth = source.GuestDateOfBirth,
            GuestSex = source.GuestSex,
            GuestAddress = source.GuestAddress,
            GuestPhoneNumber = source.GuestPhoneNumber,
            GuestEmail = source.GuestEmail,
            GuestIdentityNumber = source.GuestIdentityNumber,
            GuestIdentityType = source.GuestIdentityType,
            GuestNote = source.GuestNote,
            Nights = nights,
            EstimatedRoomAmount = Math.Round(roomAmount, 2, MidpointRounding.AwayFromZero),
            DepositAmount = Math.Round(deposit, 2, MidpointRounding.AwayFromZero),
            PaymentMethod = source.PaymentMethod,
            PaymentDate = source.PaymentDate,
            ReferenceCode = source.ReferenceCode,
            DepositConfirmed = source.DepositConfirmed,
            RoomTypes = roomTypes,
            RoomAssignments = assignments
        };
    }

    private Dictionary<string, string> ValidateDirectCreateModel(
        BookingDirectCreateViewModel model,
        DateTime now)
    {
        var errors = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (model.CheckOutDate.Date <= DateTime.Today)
        {
            errors[nameof(BookingDirectCreateViewModel.CheckOutDate)] =
                "Ngày check out phải sau ngày hiện tại.";
        }

        ValidateCodeValue(
            model.BookingCode,
            "Mã đặt phòng",
            nameof(BookingDirectCreateViewModel.BookingCode),
            errors);

        if (model.BookingCode.Trim().Length > 50)
        {
            errors[nameof(BookingDirectCreateViewModel.BookingCode)] =
                "Mã đặt phòng không được vượt quá 50 ký tự.";
        }

        if (model.TotalAdults < 1)
        {
            errors[nameof(BookingDirectCreateViewModel.TotalAdults)] =
                "Tổng người lớn phải lớn hơn 0.";
        }

        if (model.TotalChildren < 0)
        {
            errors[nameof(BookingDirectCreateViewModel.TotalChildren)] =
                "Tổng trẻ em phải lớn hơn hoặc bằng 0.";
        }

        ValidateDirectGuest(model, errors);

        var selectedTypes = model.RoomTypes.Where(x => x.Quantity > 0).ToList();
        if (selectedTypes.Count == 0)
        {
            errors[string.Empty] = "Vui lòng chọn ít nhất một loại phòng và số phòng.";
        }

        foreach (var type in selectedTypes)
        {
            if (type.Quantity > type.AvailableRoomCount)
            {
                errors[string.Empty] =
                    $"Loại phòng {type.RoomTypeCode}-{type.RoomTypeName} chỉ còn {type.AvailableRoomCount} phòng có thể sử dụng để check in trực tiếp hôm nay.";
                break;
            }
        }

        var assignmentsByType = model.RoomAssignments
            .GroupBy(x => x.RoomTypeId)
            .ToDictionary(x => x.Key, x => x.ToList());

        foreach (var type in selectedTypes)
        {
            assignmentsByType.TryGetValue(type.RoomTypeId, out var list);
            if (list is null || list.Count != type.Quantity)
            {
                errors[string.Empty] =
                    $"Loại phòng {type.RoomTypeCode}-{type.RoomTypeName} chưa được phân đủ {type.Quantity} BookingRoom.";
                continue;
            }

            foreach (var row in list.Select((value, index) => new { value, index }))
            {
                var modelIndex = model.RoomAssignments.IndexOf(row.value);
                if (row.value.Adults < 0 || row.value.Adults > type.MaxAdults)
                {
                    errors[$"RoomAssignments[{modelIndex}].Adults"] =
                        $"Số người lớn trong phòng loại {type.RoomTypeCode} phải từ 0 đến {type.MaxAdults}.";
                }

                if (row.value.Children < 0 || row.value.Children > type.MaxChildren)
                {
                    errors[$"RoomAssignments[{modelIndex}].Children"] =
                        $"Số trẻ em trong phòng loại {type.RoomTypeCode} phải từ 0 đến {type.MaxChildren}.";
                }

                if (!row.value.RoomId.HasValue || row.value.RoomId.Value == Guid.Empty)
                {
                    errors[$"RoomAssignments[{modelIndex}].RoomId"] =
                        "Vui lòng chọn phòng thực tế.";
                }

                if (!string.IsNullOrWhiteSpace(row.value.Note) && row.value.Note.Trim().Length > 255)
                {
                    errors[$"RoomAssignments[{modelIndex}].Note"] =
                        "Ghi chú BookingRoom không được vượt quá 255 ký tự.";
                }
            }
        }

        if (model.RoomAssignments.Count > 0)
        {
            if (model.RoomAssignments.GroupBy(x => x.RoomId)
                .Any(g => g.Key.HasValue && g.Key.Value != Guid.Empty && g.Count() > 1))
            {
                errors[string.Empty] = "Không thể chọn cùng một phòng cho nhiều BookingRoom.";
            }

            var assignedAdults = model.RoomAssignments.Sum(x => x.Adults);
            var assignedChildren = model.RoomAssignments.Sum(x => x.Children);
            if (assignedAdults != model.TotalAdults || assignedChildren != model.TotalChildren)
            {
                errors[string.Empty] =
                    $"Phân bổ người chưa khớp: tổng người lớn đã phân bổ {assignedAdults}/{model.TotalAdults}, tổng trẻ em đã phân bổ {assignedChildren}/{model.TotalChildren}.";
            }
        }

        if (model.DepositAmount > 0)
        {
            if (model.PaymentMethod is not 0 and not 1)
            {
                errors[nameof(BookingDirectCreateViewModel.PaymentMethod)] =
                    "Vui lòng chọn phương thức thanh toán tiền cọc.";
            }

            if (!model.PaymentDate.HasValue)
            {
                errors[nameof(BookingDirectCreateViewModel.PaymentDate)] =
                    "Vui lòng chọn thời gian thanh toán tiền cọc.";
            }
            else if (model.PaymentDate.Value > now)
            {
                errors[nameof(BookingDirectCreateViewModel.PaymentDate)] =
                    "Thời gian thanh toán tiền cọc không được lớn hơn thời điểm hiện tại.";
            }

            if (!model.DepositConfirmed)
            {
                errors[nameof(BookingDirectCreateViewModel.DepositConfirmed)] =
                    "Vui lòng xác nhận đã nhận được tiền cọc.";
            }

            if (model.PaymentMethod == 1 && string.IsNullOrWhiteSpace(model.ReferenceCode))
            {
                errors[nameof(BookingDirectCreateViewModel.ReferenceCode)] =
                    "Mã giao dịch là bắt buộc khi thanh toán chuyển khoản.";
            }
            else if (!string.IsNullOrWhiteSpace(model.ReferenceCode) && model.ReferenceCode.Trim().Length > 100)
            {
                errors[nameof(BookingDirectCreateViewModel.ReferenceCode)] =
                    "Mã giao dịch không được vượt quá 100 ký tự.";
            }
        }

        return errors;
    }

    private static void ValidateDirectGuest(
        BookingDirectCreateViewModel model,
        Dictionary<string, string> errors)
    {
        ValidateCodeValue(
            model.GuestCode,
            "Mã khách hàng",
            nameof(BookingDirectCreateViewModel.GuestCode),
            errors);

        if (model.GuestCode.Trim().Length > 50)
        {
            errors[nameof(BookingDirectCreateViewModel.GuestCode)] =
                "Mã khách hàng không được vượt quá 50 ký tự.";
        }

        var name = model.GuestName?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(name))
        {
            errors[nameof(BookingDirectCreateViewModel.GuestName)] =
                "Tên khách hàng là bắt buộc khi tạo trực tiếp tại quầy.";
        }
        else if (name.Length > 100)
        {
            errors[nameof(BookingDirectCreateViewModel.GuestName)] =
                "Tên khách hàng không được vượt quá 100 ký tự.";
        }

        if (!model.GuestDateOfBirth.HasValue)
        {
            errors[nameof(BookingDirectCreateViewModel.GuestDateOfBirth)] =
                "Ngày sinh là bắt buộc khi tạo trực tiếp tại quầy.";
        }
        else if (model.GuestDateOfBirth.Value.Date > DateTime.Today)
        {
            errors[nameof(BookingDirectCreateViewModel.GuestDateOfBirth)] =
                "Ngày sinh không được lớn hơn ngày hiện tại.";
        }

        if (!model.GuestSex.HasValue)
        {
            errors[nameof(BookingDirectCreateViewModel.GuestSex)] =
                "Giới tính là bắt buộc khi tạo trực tiếp tại quầy.";
        }
        else if (model.GuestSex is not 0 and not 1 and not 2)
        {
            errors[nameof(BookingDirectCreateViewModel.GuestSex)] =
                "Giới tính khách hàng không hợp lệ.";
        }

        var address = model.GuestAddress?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(address))
        {
            errors[nameof(BookingDirectCreateViewModel.GuestAddress)] =
                "Địa chỉ là bắt buộc khi tạo trực tiếp tại quầy.";
        }
        else if (address.Length > 255)
        {
            errors[nameof(BookingDirectCreateViewModel.GuestAddress)] =
                "Địa chỉ không được vượt quá 255 ký tự.";
        }

        var phone = model.GuestPhoneNumber?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(phone))
        {
            errors[nameof(BookingDirectCreateViewModel.GuestPhoneNumber)] =
                "Số điện thoại là bắt buộc khi tạo trực tiếp tại quầy.";
        }
        else if (!TryNormalizePhoneNumber(phone, out _))
        {
            errors[nameof(BookingDirectCreateViewModel.GuestPhoneNumber)] =
                "Số điện thoại không hợp lệ. Có thể nhập số quốc tế dạng +mã quốc gia + số thuê bao, tối đa 15 chữ số sau khi chuẩn hóa.";
        }
        else if (phone.Length > 30)
        {
            errors[nameof(BookingDirectCreateViewModel.GuestPhoneNumber)] =
                "Số điện thoại nhập vào không được vượt quá 30 ký tự.";
        }

        var email = model.GuestEmail?.Trim() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(email) &&
            (email.Length > 254 || !new EmailAddressAttribute().IsValid(email)))
        {
            errors[nameof(BookingDirectCreateViewModel.GuestEmail)] =
                "Email không đúng định dạng hoặc vượt quá 254 ký tự.";
        }

        ValidateIdentityNumber(
            model.GuestIdentityNumber,
            model.GuestIdentityType,
            isRequired: true,
            identityFieldName: nameof(BookingDirectCreateViewModel.GuestIdentityNumber),
            identityTypeFieldName: nameof(BookingDirectCreateViewModel.GuestIdentityType),
            errors);

        if (!string.IsNullOrWhiteSpace(model.GuestNote) && model.GuestNote.Trim().Length > 255)
        {
            errors[nameof(BookingDirectCreateViewModel.GuestNote)] =
                "Ghi chú khách hàng không được vượt quá 255 ký tự.";
        }
    }

    private async Task ValidateDirectRoomAssignmentsLiveAsync(
        BookingDirectCreateViewModel model,
        DateTime now,
        CancellationToken cancellationToken,
        Dictionary<string, string> errors)
    {
        var assignments = model.RoomAssignments;
        var selectedIds = assignments
            .Where(x => x.RoomId.HasValue)
            .Select(x => x.RoomId!.Value)
            .Distinct()
            .ToArray();

        if (selectedIds.Length == 0)
        {
            return;
        }

        var expectedCheckout = model.ExpectedCheckOutAt;
        if (expectedCheckout <= now)
        {
            errors[nameof(BookingDirectCreateViewModel.CheckOutDate)] = "Ngày check out dự kiến phải sau thời điểm check in trực tiếp.";
            return;
        }

        var rooms = await _context.Rooms
            .AsNoTracking()
            .Where(x => selectedIds.Contains(x.Id))
            .ToListAsync(cancellationToken);

        var byId = rooms.ToDictionary(x => x.Id);
        var overlapping = await GetOverlappingBookedRoomIdsAsync(now, expectedCheckout, cancellationToken);

        for (var i = 0; i < assignments.Count; i++)
        {
            var assignment = assignments[i];
            if (!assignment.RoomId.HasValue)
            {
                continue;
            }

            var key = $"RoomAssignments[{i}].RoomId";
            if (!byId.TryGetValue(assignment.RoomId.Value, out var room))
            {
                errors[key] = "Phòng được chọn không tồn tại.";
                continue;
            }

            if (room.IsDeleted || !room.IsActive || room.Status != 0)
            {
                errors[key] = $"Phòng {room.RoomCode} không còn trống để check in.";
                continue;
            }

            if (room.RoomTypeId != assignment.RoomTypeId)
            {
                errors[key] = $"Phòng {room.RoomCode} không thuộc loại phòng {assignment.RoomTypeCode}.";
                continue;
            }

            if (overlapping.Contains(room.Id))
            {
                errors[key] = $"Phòng {room.RoomCode} đã có lịch đặt/đang được sử dụng trong khoảng thời gian này.";
            }
        }
    }

    private async Task<HashSet<Guid>> GetOverlappingBookedRoomIdsAsync(
        DateTime start,
        DateTime end,
        CancellationToken cancellationToken)
    {
        var ids = await _context.BookingRooms
            .AsNoTracking()
            .Where(br => !br.IsDeleted && br.RoomId.HasValue &&
                         ReservedBookingStatuses.Contains(br.Booking.Status) &&
                         !br.Booking.IsDeleted &&
                         br.Booking.ExpectedCheckInAt < end &&
                         br.Booking.ExpectedCheckOutAt > start)
            .Select(br => br.RoomId!.Value)
            .Distinct()
            .ToListAsync(cancellationToken);

        return ids.ToHashSet();
    }

    private static void ApplyDirectGuestFields(
        Models.Guest guest,
        BookingDirectCreateViewModel model,
        Guid currentUserId)
    {
        guest.GuestCode = model.GuestCode.Trim();
        guest.Name = model.GuestName.Trim();
        guest.DateOfBirth = model.GuestDateOfBirth?.Date;
        guest.Sex = model.GuestSex;
        guest.Address = model.GuestAddress.Trim();
        guest.PhoneNumber = NormalizePhoneNumberForStorage(model.GuestPhoneNumber);
        guest.Email = NormalizeOptional(model.GuestEmail);
        guest.IdentityNumber = model.GuestIdentityNumber.Trim();
        guest.IdentityType = model.GuestIdentityType;
        guest.Note = NormalizeOptional(model.GuestNote);
        guest.LastModifiedBy = currentUserId;
        guest.LastModifiedAt = DateTime.Now;
    }

    private async Task AddAvailabilitySnapshotAsync(
        BookingCreateViewModel model,
        CancellationToken cancellationToken)
    {
        var ids = model.RoomSelections
            .Select(x => x.RoomTypeId)
            .Where(x => x != Guid.Empty)
            .Distinct()
            .ToArray();

        if (ids.Length == 0)
        {
            return;
        }

        var start = CombineDateAndTime(model.CheckInDate, model.DefaultCheckInTime);
        var end = CombineDateAndTime(model.CheckOutDate, model.DefaultCheckOutTime);

        var roomCounts = await _context.Rooms
            .AsNoTracking()
            .Where(x =>
                ids.Contains(x.RoomTypeId) &&
                x.IsActive &&
                !x.IsDeleted &&
                x.Status != 4)
            .GroupBy(x => x.RoomTypeId)
            .Select(x => new { RoomTypeId = x.Key, Count = x.Count() })
            .ToDictionaryAsync(x => x.RoomTypeId, x => x.Count, cancellationToken);

        var reservedCounts = start < end
            ? await _context.BookingRooms
                .AsNoTracking()
                .Where(x =>
                    ids.Contains(x.RoomTypeId) &&
                    !x.IsDeleted &&
                    x.Booking != null &&
                    !x.Booking.IsDeleted &&
                    ReservedBookingStatuses.Contains(x.Booking.Status) &&
                    x.Booking.ExpectedCheckInAt < end &&
                    x.Booking.ExpectedCheckOutAt > start)
                .GroupBy(x => x.RoomTypeId)
                .Select(x => new { RoomTypeId = x.Key, Count = x.Count() })
                .ToDictionaryAsync(x => x.RoomTypeId, x => x.Count, cancellationToken)
            : new Dictionary<Guid, int>();

        foreach (var row in model.RoomSelections)
        {
            roomCounts.TryGetValue(row.RoomTypeId, out var totalRooms);
            reservedCounts.TryGetValue(row.RoomTypeId, out var reservedRooms);
            row.AvailableRoomCount = Math.Max(0, totalRooms - reservedRooms);
        }

        foreach (var row in model.RoomTypes)
        {
            roomCounts.TryGetValue(row.Id, out var totalRooms);
            reservedCounts.TryGetValue(row.Id, out var reservedRooms);
            row.AvailableRoomCount = Math.Max(0, totalRooms - reservedRooms);
        }
    }

    private async Task<BookingCreateViewModel> NormalizeCreateModelAsync(
        BookingCreateViewModel source,
        Models.HotelSetting settings,
        CancellationToken cancellationToken)
    {
        var model = CopyCreateModel(source);

        model.DefaultCheckInTime = settings.CheckInTime;
        model.DefaultCheckOutTime = settings.CheckOutTime;

        if (model.CheckInDate == default)
        {
            model.CheckInDate = DateTime.Today;
        }

        if (model.CheckOutDate == default)
        {
            model.CheckOutDate = model.CheckInDate.Date.AddDays(1);
        }

        var roomTypes = await _context.RoomTypes
            .AsNoTracking()
            .Where(x => x.IsActive && !x.IsDeleted)
            .OrderBy(x => x.RoomTypeCode)
            .Select(x => new RoomTypeCreateOptionViewModel
            {
                Id = x.Id,
                Code = x.RoomTypeCode,
                Name = x.RoomTypeName,
                MaxAdults = x.MaxAdults,
                MaxChildren = x.MaxChildren,
                PricePerNight = x.PricePerNight,
                DepositPercent = x.DepositPercent
            })
            .ToListAsync(cancellationToken);

        var incoming = (source.RoomSelections ?? new List<BookingRoomSelectionViewModel>())
            .GroupBy(x => x.RoomTypeId)
            .ToDictionary(x => x.Key, x => x.First());

        model.RoomTypes = roomTypes;
        model.RoomSelections = roomTypes
            .Select(rt =>
            {
                incoming.TryGetValue(rt.Id, out var input);
                return new BookingRoomSelectionViewModel
                {
                    RoomTypeId = rt.Id,
                    RoomTypeCode = rt.Code,
                    RoomTypeName = rt.Name,
                    MaxAdults = rt.MaxAdults,
                    MaxChildren = rt.MaxChildren,
                    PricePerNight = rt.PricePerNight,
                    DepositPercent = rt.DepositPercent,
                    // Đồng bộ với giới hạn UI và chặn dữ liệu POST cực lớn trước khi tính tiền.
                    Quantity = Math.Min(999, Math.Max(0, input?.Quantity ?? 0))
                };
            })
            .ToList();

        await AddAvailabilitySnapshotAsync(model, cancellationToken);

        var checkIn = model.CheckInDate.Date;
        var checkOut = model.CheckOutDate.Date;
        model.Nights = checkOut > checkIn
            ? (checkOut - checkIn).Days
            : 0;

        model.EstimatedRoomAmount = Math.Round(
            model.RoomSelections
                .Where(x => x.Quantity > 0)
                .Sum(x => x.Quantity * x.PricePerNight * model.Nights),
            2,
            MidpointRounding.AwayFromZero);

        model.DepositAmount = Math.Round(
            model.RoomSelections
                .Where(x => x.Quantity > 0)
                .Sum(x => x.Quantity * x.PricePerNight * model.Nights * x.DepositPercent / 100m),
            2,
            MidpointRounding.AwayFromZero);

        if (model.DepositAmount <= 0)
        {
            model.PaymentMethod = null;
            model.PaymentDate = null;
            model.ReferenceCode = null;
            model.DepositConfirmed = false;
        }


        if (string.IsNullOrWhiteSpace(model.BookingCode))
        {
            model.BookingCode = await GetNextBookingCodeAsync(cancellationToken);
        }

        model.GuestCode = string.IsNullOrWhiteSpace(model.GuestCode)
            ? await GetNextGuestCodeAsync(cancellationToken)
            : model.GuestCode.Trim();

        return model;
    }

    private void AddStepTwoValidationErrors(
        BookingCreateViewModel model,
        Dictionary<string, string> errors)
    {
        if (model.BookingType is not 0 and not 1)
        {
            errors[nameof(BookingCreateViewModel.BookingType)] = "Vui lòng chọn loại đặt phòng hợp lệ.";
        }

        ValidateCodeValue(
            model.BookingCode,
            "Mã đặt phòng",
            nameof(BookingCreateViewModel.BookingCode),
            errors);

        if (model.BookingCode.Trim().Length > 50)
        {
            errors[nameof(BookingCreateViewModel.BookingCode)] = "Mã đặt phòng không được vượt quá 50 ký tự.";
        }

        if (!string.IsNullOrWhiteSpace(model.BookingNote) && model.BookingNote.Trim().Length > 255)
        {
            errors[nameof(BookingCreateViewModel.BookingNote)] = "Ghi chú Booking không được vượt quá 255 ký tự.";
        }

        // Mỗi Booking mới luôn tạo Guest mới; không hỗ trợ chọn/gán Guest cũ.
        ValidateGuestFields(model, errors);

        if (model.DepositAmount > 0)
        {
            if (model.PaymentMethod is not 0 and not 1)
            {
                errors[nameof(BookingCreateViewModel.PaymentMethod)] =
                    "Vui lòng chọn phương thức thanh toán tiền cọc.";
            }

            if (!model.PaymentDate.HasValue)
            {
                errors[nameof(BookingCreateViewModel.PaymentDate)] =
                    "Vui lòng chọn thời gian thanh toán tiền cọc.";
            }
            else if (model.Step2StartedAt.HasValue && model.PaymentDate.Value < model.Step2StartedAt.Value)
            {
                errors[nameof(BookingCreateViewModel.PaymentDate)] =
                    "Thời gian thanh toán tiền cọc không được trước thời điểm bắt đầu bước 2.";
            }
            else if (model.PaymentDate.Value > DateTime.Now)
            {
                errors[nameof(BookingCreateViewModel.PaymentDate)] =
                    "Thời gian thanh toán tiền cọc không được lớn hơn thời điểm submit bước 2.";
            }

            if (!model.DepositConfirmed)
            {
                errors[nameof(BookingCreateViewModel.DepositConfirmed)] =
                    "Vui lòng xác nhận đã nhận được tiền cọc.";
            }

            if (model.PaymentMethod == 1 && string.IsNullOrWhiteSpace(model.ReferenceCode))
            {
                errors[nameof(BookingCreateViewModel.ReferenceCode)] =
                    "Mã giao dịch là bắt buộc khi thanh toán chuyển khoản.";
            }
            else if (!string.IsNullOrWhiteSpace(model.ReferenceCode) && model.ReferenceCode.Trim().Length > 100)
            {
                errors[nameof(BookingCreateViewModel.ReferenceCode)] =
                    "Mã giao dịch không được vượt quá 100 ký tự.";
            }
        }
    }

    private void ValidateExistingGuestEditableFields(
        BookingCreateViewModel model,
        Dictionary<string, string> errors)
    {
        var direct = model.BookingType == 1;

        ValidateCodeValue(
            model.GuestCode,
            "Mã khách hàng",
            nameof(BookingCreateViewModel.GuestCode),
            errors);

        if (model.GuestCode.Trim().Length > 50)
        {
            errors[nameof(BookingCreateViewModel.GuestCode)] = "Mã khách hàng không được vượt quá 50 ký tự.";
        }

        if (string.IsNullOrWhiteSpace(model.GuestName))
        {
            errors[nameof(BookingCreateViewModel.GuestName)] = "Tên khách hàng không được để trống.";
        }
        else if (model.GuestName.Trim().Length > 100)
        {
            errors[nameof(BookingCreateViewModel.GuestName)] = "Tên khách hàng không được vượt quá 100 ký tự.";
        }

        if (!model.GuestDateOfBirth.HasValue && direct)
        {
            errors[nameof(BookingCreateViewModel.GuestDateOfBirth)] = "Ngày sinh là bắt buộc khi tạo lịch trực tiếp tại quầy.";
        }
        else if (model.GuestDateOfBirth.HasValue && model.GuestDateOfBirth.Value.Date > DateTime.Today)
        {
            errors[nameof(BookingCreateViewModel.GuestDateOfBirth)] = "Ngày sinh không được lớn hơn ngày hiện tại.";
        }

        if (!model.GuestSex.HasValue && direct)
        {
            errors[nameof(BookingCreateViewModel.GuestSex)] = "Giới tính là bắt buộc khi tạo lịch trực tiếp tại quầy.";
        }
        else if (model.GuestSex.HasValue && model.GuestSex is not 0 and not 1 and not 2)
        {
            errors[nameof(BookingCreateViewModel.GuestSex)] = "Giới tính khách hàng không hợp lệ.";
        }

        if (string.IsNullOrWhiteSpace(model.GuestAddress) && direct)
        {
            errors[nameof(BookingCreateViewModel.GuestAddress)] = "Địa chỉ là bắt buộc khi tạo lịch trực tiếp tại quầy.";
        }
        else if (!string.IsNullOrWhiteSpace(model.GuestAddress) && model.GuestAddress.Trim().Length > 255)
        {
            errors[nameof(BookingCreateViewModel.GuestAddress)] = "Địa chỉ không được vượt quá 255 ký tự.";
        }

        var phone = model.GuestPhoneNumber?.Trim();
        var email = model.GuestEmail?.Trim();
        if (string.IsNullOrWhiteSpace(phone))
        {
            if (direct)
            {
                errors[nameof(BookingCreateViewModel.GuestPhoneNumber)] =
                    "Số điện thoại là bắt buộc khi tạo lịch trực tiếp tại quầy.";
            }
        }
        else if (!TryNormalizePhoneNumber(phone, out _))
        {
            errors[nameof(BookingCreateViewModel.GuestPhoneNumber)] =
                "Số điện thoại không hợp lệ. Có thể nhập số quốc tế dạng +mã quốc gia + số thuê bao, tối đa 15 chữ số sau khi chuẩn hóa.";
        }
        else if (phone.Length > 30)
        {
            errors[nameof(BookingCreateViewModel.GuestPhoneNumber)] = "Số điện thoại nhập vào không được vượt quá 30 ký tự.";
        }

        if (!string.IsNullOrWhiteSpace(email) &&
            (email.Length > 254 || !new EmailAddressAttribute().IsValid(email)))
        {
            errors[nameof(BookingCreateViewModel.GuestEmail)] = "Email không hợp lệ.";
        }

        ValidateIdentityNumber(
            model.GuestIdentityNumber,
            model.GuestIdentityType,
            isRequired: direct,
            identityFieldName: nameof(BookingCreateViewModel.GuestIdentityNumber),
            identityTypeFieldName: nameof(BookingCreateViewModel.GuestIdentityType),
            errors);

        if (!string.IsNullOrWhiteSpace(model.GuestNote) && model.GuestNote.Trim().Length > 255)
        {
            errors[nameof(BookingCreateViewModel.GuestNote)] = "Ghi chú khách hàng không được vượt quá 255 ký tự.";
        }
    }

    private void ValidateGuestFields(
        BookingCreateViewModel model,
        Dictionary<string, string> errors)
    {
        var direct = model.BookingType == 1;
        ValidateCodeValue(
            model.GuestCode,
            "Mã khách hàng",
            nameof(BookingCreateViewModel.GuestCode),
            errors);

        if (model.GuestCode.Trim().Length > 50)
        {
            errors[nameof(BookingCreateViewModel.GuestCode)] = "Mã khách hàng không được vượt quá 50 ký tự.";
        }

        if (string.IsNullOrWhiteSpace(model.GuestName))
        {
            errors[nameof(BookingCreateViewModel.GuestName)] = "Tên khách hàng không được để trống.";
        }
        else if (model.GuestName.Trim().Length > 100)
        {
            errors[nameof(BookingCreateViewModel.GuestName)] = "Tên khách hàng không được vượt quá 100 ký tự.";
        }

        if (!model.GuestDateOfBirth.HasValue && direct)
        {
            errors[nameof(BookingCreateViewModel.GuestDateOfBirth)] = "Ngày sinh là bắt buộc khi tạo lịch trực tiếp tại quầy.";
        }
        else if (model.GuestDateOfBirth.HasValue && model.GuestDateOfBirth.Value.Date > DateTime.Today)
        {
            errors[nameof(BookingCreateViewModel.GuestDateOfBirth)] = "Ngày sinh không được lớn hơn ngày hiện tại.";
        }

        if (!model.GuestSex.HasValue && direct)
        {
            errors[nameof(BookingCreateViewModel.GuestSex)] = "Giới tính là bắt buộc khi tạo lịch trực tiếp tại quầy.";
        }
        else if (model.GuestSex.HasValue && model.GuestSex is not 0 and not 1 and not 2)
        {
            errors[nameof(BookingCreateViewModel.GuestSex)] = "Giới tính khách hàng không hợp lệ.";
        }

        if (string.IsNullOrWhiteSpace(model.GuestAddress) && direct)
        {
            errors[nameof(BookingCreateViewModel.GuestAddress)] = "Địa chỉ là bắt buộc khi tạo lịch trực tiếp tại quầy.";
        }
        else if (!string.IsNullOrWhiteSpace(model.GuestAddress) && model.GuestAddress.Trim().Length > 255)
        {
            errors[nameof(BookingCreateViewModel.GuestAddress)] = "Địa chỉ không được vượt quá 255 ký tự.";
        }

        var phone = model.GuestPhoneNumber?.Trim();
        var email = model.GuestEmail?.Trim();
        if (string.IsNullOrWhiteSpace(phone) && string.IsNullOrWhiteSpace(email))
        {
            // Đây là lỗi của nhóm thông tin liên hệ. Gắn vào GuestPhoneNumber
            // để Razor hiển thị lỗi ngay dưới field, thay vì đẩy lên lỗi tổng quát.
            errors[nameof(BookingCreateViewModel.GuestPhoneNumber)] =
                "Khách hàng phải có ít nhất số điện thoại hoặc email.";
        }

        if (string.IsNullOrWhiteSpace(phone))
        {
            if (direct)
            {
                errors[nameof(BookingCreateViewModel.GuestPhoneNumber)] =
                    "Số điện thoại là bắt buộc khi tạo lịch trực tiếp tại quầy.";
            }
        }
        else if (!TryNormalizePhoneNumber(phone, out _))
        {
            errors[nameof(BookingCreateViewModel.GuestPhoneNumber)] =
                "Số điện thoại không hợp lệ. Có thể nhập số quốc tế dạng +mã quốc gia + số thuê bao, tối đa 15 chữ số sau khi chuẩn hóa.";
        }
        else if (phone.Length > 30)
        {
            errors[nameof(BookingCreateViewModel.GuestPhoneNumber)] = "Số điện thoại nhập vào không được vượt quá 30 ký tự.";
        }

        if (!string.IsNullOrWhiteSpace(email))
        {
            if (email.Length > 254 || !new EmailAddressAttribute().IsValid(email))
            {
                errors[nameof(BookingCreateViewModel.GuestEmail)] = "Email không hợp lệ.";
            }
        }

        if (!string.IsNullOrWhiteSpace(model.GuestNote) && model.GuestNote.Trim().Length > 255)
        {
            errors[nameof(BookingCreateViewModel.GuestNote)] = "Ghi chú khách hàng không được vượt quá 255 ký tự.";
        }

        ValidateIdentityNumber(
            model.GuestIdentityNumber,
            model.GuestIdentityType,
            isRequired: direct,
            identityFieldName: nameof(BookingCreateViewModel.GuestIdentityNumber),
            identityTypeFieldName: nameof(BookingCreateViewModel.GuestIdentityType),
            errors);
    }

    private void ValidateAvailabilityOnly(
        BookingCreateViewModel model,
        Dictionary<string, string> errors)
    {
        if (model.CheckInDate.Date < DateTime.Today)
        {
            errors[nameof(BookingCreateViewModel.CheckInDate)] = "Ngày check in không còn hợp lệ.";
        }

        if (model.CheckOutDate.Date <= model.CheckInDate.Date)
        {
            errors[nameof(BookingCreateViewModel.CheckOutDate)] = "Ngày check out phải sau ngày check in.";
        }

        if (!model.RoomSelections.Any(x => x.Quantity > 0))
        {
            errors[string.Empty] = "Vui lòng chọn ít nhất một loại phòng và số lượng phòng lớn hơn 0.";
        }

        if (model.TotalAdults < 1)
        {
            errors[nameof(BookingCreateViewModel.TotalAdults)] = "Tổng người lớn phải lớn hơn 0.";
        }

        if (model.TotalChildren < 0)
        {
            errors[nameof(BookingCreateViewModel.TotalChildren)] = "Tổng trẻ em phải lớn hơn hoặc bằng 0.";
        }

        var availabilityIssues = new List<string>();

        foreach (var row in model.RoomSelections.Select((item, index) => new { item, index }))
        {
            if (row.item.Quantity < 0)
            {
                errors[$"RoomSelections[{row.index}].Quantity"] = "Số lượng phòng không được âm.";
                continue;
            }

            if (row.item.Quantity <= 0)
            {
                continue;
            }

            if (row.item.Quantity > row.item.AvailableRoomCount)
            {
                var detail = $"Loại phòng {row.item.RoomTypeCode}-{row.item.RoomTypeName} chỉ còn {row.item.AvailableRoomCount} phòng có thể đặt trong khoảng thời gian này";
                availabilityIssues.Add(detail);
                errors[$"RoomSelections[{row.index}].Quantity"] = $"{detail}.";
            }
        }

        if (availabilityIssues.Count > 0)
        {
            errors[string.Empty] = string.Join("; ", availabilityIssues) + ".";
        }

        if (errors.Count == 0)
        {
            var maxAdultsTotal = model.RoomSelections.Where(x => x.Quantity > 0)
                .Sum(x => (long)x.MaxAdults * x.Quantity);
            var maxChildrenTotal = model.RoomSelections.Where(x => x.Quantity > 0)
                .Sum(x => (long)x.MaxChildren * x.Quantity);

            if (model.TotalAdults > maxAdultsTotal)
            {
                errors[nameof(BookingCreateViewModel.TotalAdults)] =
                    $"Tổng người lớn vượt sức chứa tối đa của số phòng đã chọn ({maxAdultsTotal} người).";
            }

            if (model.TotalChildren > maxChildrenTotal)
            {
                errors[nameof(BookingCreateViewModel.TotalChildren)] =
                    $"Tổng trẻ em vượt sức chứa tối đa của số phòng đã chọn ({maxChildrenTotal} trẻ em).";
            }
        }
    }

    private async Task PopulateCreateReferenceDataAsync(
        BookingCreateViewModel model,
        CancellationToken cancellationToken)
    {
        var settings = await GetHotelSettingAsync(cancellationToken);
        model.DefaultCheckInTime = settings.CheckInTime;
        model.DefaultCheckOutTime = settings.CheckOutTime;

    }

    private async Task<BookingCreateViewModel> EnsureCreateReferencesAsync(
        BookingCreateViewModel model,
        CancellationToken cancellationToken)
    {
        await PopulateCreateReferenceDataAsync(model, cancellationToken);
        return model;
    }

    private async Task<Models.HotelSetting> GetHotelSettingAsync(
        CancellationToken cancellationToken)
    {
        return await _context.HotelSettings
            .AsNoTracking()
            .OrderBy(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken)
            ?? new Models.HotelSetting
            {
                CheckInTime = new TimeOnly(14, 0),
                CheckOutTime = new TimeOnly(12, 0)
            };
    }

    public async Task<string> GetNextBookingCodeAsync(
        CancellationToken cancellationToken)
    {
        var codes = await _context.Bookings
            .AsNoTracking()
            .Where(x => x.BookingCode.StartsWith("DP"))
            .Select(x => x.BookingCode)
            .ToListAsync(cancellationToken);

        var max = GetMaxNumericSuffix(codes, "DP");
        return $"DP{max + 1}";
    }

    public async Task<string> GetNextGuestCodeAsync(
        CancellationToken cancellationToken)
    {
        var codes = await _context.Guests
            .AsNoTracking()
            .Where(x => x.GuestCode.StartsWith("KH"))
            .Select(x => x.GuestCode)
            .ToListAsync(cancellationToken);

        var max = GetMaxNumericSuffix(codes, "KH");
        return $"KH{max + 1}";
    }

    private static int GetMaxNumericSuffix(
        IEnumerable<string> codes,
        string prefix)
    {
        var max = 0;
        foreach (var code in codes)
        {
            if (!code.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var suffix = code[prefix.Length..];
            if (int.TryParse(suffix, out var number) && number > max)
            {
                max = number;
            }
        }

        return max;
    }

    private static DateTime CombineDateAndTime(DateTime date, TimeOnly time)
    {
        return date.Date.Add(time.ToTimeSpan());
    }

    private static void ApplyGuestFields(
        Models.Guest guest,
        BookingCreateViewModel model,
        Guid currentUserId)
    {
        guest.GuestCode = model.GuestCode.Trim();
        guest.Name = model.GuestName.Trim();
        guest.DateOfBirth = model.GuestDateOfBirth?.Date;
        guest.Sex = model.GuestSex;
        guest.Address = NormalizeOptional(model.GuestAddress);
        guest.PhoneNumber = NormalizeOptional(model.GuestPhoneNumber);
        guest.Email = NormalizeOptional(model.GuestEmail);
        guest.IdentityNumber = NormalizeOptional(model.GuestIdentityNumber);
        guest.IdentityType = model.GuestIdentityType;
        guest.Note = NormalizeOptional(model.GuestNote);
        guest.LastModifiedBy = currentUserId;
        guest.LastModifiedAt = DateTime.Now;
    }

    private static Models.Guest CreateGuest(
        BookingCreateViewModel model,
        string guestCode,
        Guid currentUserId)
    {
        var now = DateTime.Now;
        return new Models.Guest
        {
            Id = Guid.NewGuid(),
            GuestCode = guestCode,
            Name = model.GuestName.Trim(),
            DateOfBirth = model.GuestDateOfBirth?.Date,
            Sex = model.GuestSex,
            Address = NormalizeOptional(model.GuestAddress),
            PhoneNumber = NormalizePhoneNumberForStorage(model.GuestPhoneNumber),
            Email = NormalizeOptional(model.GuestEmail),
            IdentityNumber = NormalizeOptional(model.GuestIdentityNumber),
            IdentityType = model.GuestIdentityType,
            Note = NormalizeOptional(model.GuestNote),
            IsDeleted = false,
            CreatedBy = currentUserId,
            CreatedAt = now,
            LastModifiedBy = currentUserId,
            LastModifiedAt = now
        };
    }

    private static BookingCreateViewModel CopyCreateModel(BookingCreateViewModel source)
    {
        return new BookingCreateViewModel
        {
            DraftId = source.DraftId,
            Step2StartedAt = source.Step2StartedAt,
            BookingId = source.BookingId,
            Step = source.Step,
            CheckInDate = source.CheckInDate,
            CheckOutDate = source.CheckOutDate,
            DefaultCheckInTime = source.DefaultCheckInTime,
            DefaultCheckOutTime = source.DefaultCheckOutTime,
            BookingType = source.BookingType,
            BookingCode = source.BookingCode?.Trim() ?? string.Empty,
            BookingNote = source.BookingNote,
            GuestId = source.GuestId,
            GuestCode = source.GuestCode?.Trim() ?? string.Empty,
            GuestName = source.GuestName,
            GuestDateOfBirth = source.GuestDateOfBirth,
            GuestSex = source.GuestSex,
            GuestAddress = source.GuestAddress,
            GuestPhoneNumber = source.GuestPhoneNumber,
            GuestEmail = source.GuestEmail,
            GuestIdentityNumber = source.GuestIdentityNumber,
            GuestIdentityType = source.GuestIdentityType,
            GuestNote = source.GuestNote,
            Nights = source.Nights,
            EstimatedRoomAmount = source.EstimatedRoomAmount,
            DepositAmount = source.DepositAmount,
            TotalAdults = source.TotalAdults,
            TotalChildren = source.TotalChildren,
            PaymentMethod = source.PaymentMethod,
            PaymentDate = source.PaymentDate,
            ReferenceCode = source.ReferenceCode,
            DepositConfirmed = source.DepositConfirmed,
            AvailabilityMessage = source.AvailabilityMessage,
            AvailabilitySucceeded = source.AvailabilitySucceeded
        };
    }

    private static string? NormalizeOptional(string? value)
    {
        var text = value?.Trim();
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    private static BookingCreateViewModel MapStep1ToLegacy(
        BookingCreateStep1ViewModel source)
    {
        return new BookingCreateViewModel
        {
            Step = 1,
            Step2StartedAt = source.Step2StartedAt,
            CheckInDate = source.CheckInDate,
            CheckOutDate = source.CheckOutDate,
            DefaultCheckInTime = source.DefaultCheckInTime,
            DefaultCheckOutTime = source.DefaultCheckOutTime,
            Nights = source.Nights,
            EstimatedRoomAmount = source.EstimatedRoomAmount,
            DepositAmount = source.DepositAmount,
            TotalAdults = source.TotalAdults,
            TotalChildren = source.TotalChildren,
            AvailabilityMessage = source.AvailabilityMessage,
            AvailabilitySucceeded = source.AvailabilitySucceeded,
            RoomSelections = source.RoomSelections?.ToList() ?? new List<BookingRoomSelectionViewModel>()
        };
    }

    private static BookingCreateViewModel MergeStep1AndStep2(
        BookingCreateStep1ViewModel step1,
        BookingCreateStep2ViewModel step2)
    {
        return new BookingCreateViewModel
        {
            Step = 2,
            Step2StartedAt = step2.Step2StartedAt,
            BookingType = step2.BookingType,
            BookingId = step2.BookingId,
            CheckInDate = step1.CheckInDate,
            CheckOutDate = step1.CheckOutDate,
            DefaultCheckInTime = step1.DefaultCheckInTime,
            DefaultCheckOutTime = step1.DefaultCheckOutTime,
            Nights = step1.Nights,
            EstimatedRoomAmount = step1.EstimatedRoomAmount,
            TotalAdults = step1.TotalAdults,
            TotalChildren = step1.TotalChildren,
            AvailabilityMessage = step1.AvailabilityMessage,
            AvailabilitySucceeded = step1.AvailabilitySucceeded,
            RoomSelections = step1.RoomSelections?.ToList() ?? new List<BookingRoomSelectionViewModel>(),
            BookingCode = step2.BookingCode ?? string.Empty,
            BookingNote = step2.BookingNote,
            GuestId = step2.GuestId,
            GuestCode = step2.GuestCode ?? string.Empty,
            GuestName = step2.GuestName ?? string.Empty,
            GuestDateOfBirth = step2.GuestDateOfBirth,
            GuestSex = step2.GuestSex,
            GuestAddress = step2.GuestAddress ?? string.Empty,
            GuestPhoneNumber = step2.GuestPhoneNumber,
            GuestEmail = step2.GuestEmail,
            GuestIdentityNumber = step2.GuestIdentityNumber,
            GuestIdentityType = step2.GuestIdentityType,
            GuestNote = step2.GuestNote,
            PaymentMethod = step2.PaymentMethod,
            PaymentDate = step2.PaymentDate,
            ReferenceCode = step2.ReferenceCode,
            DepositConfirmed = step2.DepositConfirmed,
            DepositAmount = step2.DepositAmount
        };
    }

    private static BookingCreateStep1ViewModel MapLegacyToStep1(
        BookingCreateViewModel source,
        string? draftId)
    {
        return new BookingCreateStep1ViewModel
        {
            DraftId = draftId,
            CheckInDate = source.CheckInDate,
            CheckOutDate = source.CheckOutDate,
            DefaultCheckInTime = source.DefaultCheckInTime,
            DefaultCheckOutTime = source.DefaultCheckOutTime,
            TotalAdults = source.TotalAdults,
            TotalChildren = source.TotalChildren,
            Nights = source.Nights,
            EstimatedRoomAmount = source.EstimatedRoomAmount,
            DepositAmount = source.DepositAmount,
            AvailabilityMessage = source.AvailabilityMessage,
            AvailabilitySucceeded = source.AvailabilitySucceeded,
            RoomSelections = source.RoomSelections?.ToList() ?? new List<BookingRoomSelectionViewModel>()
        };
    }

    private static BookingCreateStep2ViewModel MapLegacyToStep2(
        BookingCreateViewModel source,
        string draftId)
    {
        return new BookingCreateStep2ViewModel
        {
            DraftId = draftId,
            Step2StartedAt = source.Step2StartedAt,
            BookingId = source.BookingId,
            BookingType = source.BookingType,
            CheckInDate = source.CheckInDate,
            CheckOutDate = source.CheckOutDate,
            DefaultCheckInTime = source.DefaultCheckInTime,
            DefaultCheckOutTime = source.DefaultCheckOutTime,
            TotalAdults = source.TotalAdults,
            TotalChildren = source.TotalChildren,
            Nights = source.Nights,
            EstimatedRoomAmount = source.EstimatedRoomAmount,
            DepositAmount = source.DepositAmount,
            AvailabilityMessage = source.AvailabilityMessage,
            AvailabilitySucceeded = source.AvailabilitySucceeded,
            RoomSelections = source.RoomSelections?.ToList() ?? new List<BookingRoomSelectionViewModel>(),
            BookingCode = source.BookingCode,
            BookingNote = source.BookingNote,
            GuestId = source.GuestId,
            GuestCode = source.GuestCode,
            GuestName = source.GuestName,
            GuestDateOfBirth = source.GuestDateOfBirth,
            GuestSex = source.GuestSex,
            GuestAddress = source.GuestAddress,
            GuestPhoneNumber = source.GuestPhoneNumber,
            GuestEmail = source.GuestEmail,
            GuestIdentityNumber = source.GuestIdentityNumber,
            GuestIdentityType = source.GuestIdentityType,
            GuestNote = source.GuestNote,
            PaymentMethod = source.PaymentMethod,
            PaymentDate = source.PaymentDate,
            ReferenceCode = source.ReferenceCode,
            DepositConfirmed = source.DepositConfirmed,
            Guests = source.Guests ?? Array.Empty<GuestOptionViewModel>()
        };
    }

    private static BookingSortOption NormalizeSort(BookingSortOption sort)
    {
        return Enum.IsDefined(typeof(BookingSortOption), sort)
            ? sort
            : BookingSortOption.LastModifiedDesc;
    }

    private static byte? NormalizeStatus(byte? status)
    {
        return status.HasValue && status.Value <= 9
            ? status
            : null;
    }

    private static IOrderedQueryable<Models.Booking> ApplySort(
        IQueryable<Models.Booking> query,
        BookingSortOption sort)
    {
        return sort switch
        {
            BookingSortOption.BookingCodeAsc => query.OrderBy(x => x.BookingCode).ThenByDescending(x => x.LastModifiedAt),
            BookingSortOption.BookingCodeDesc => query.OrderByDescending(x => x.BookingCode).ThenByDescending(x => x.LastModifiedAt),
            BookingSortOption.CreatedAtDesc => query.OrderByDescending(x => x.CreatedAt).ThenBy(x => x.BookingCode),
            BookingSortOption.CreatedAtAsc => query.OrderBy(x => x.CreatedAt).ThenBy(x => x.BookingCode),
            BookingSortOption.LastModifiedAsc => query.OrderBy(x => x.LastModifiedAt).ThenBy(x => x.BookingCode),
            BookingSortOption.ExpectedCheckInDesc => query.OrderByDescending(x => x.ExpectedCheckInAt).ThenBy(x => x.BookingCode),
            BookingSortOption.ExpectedCheckInAsc => query.OrderBy(x => x.ExpectedCheckInAt).ThenBy(x => x.BookingCode),
            BookingSortOption.ExpectedCheckOutDesc => query.OrderByDescending(x => x.ExpectedCheckOutAt).ThenBy(x => x.BookingCode),
            BookingSortOption.ExpectedCheckOutAsc => query.OrderBy(x => x.ExpectedCheckOutAt).ThenBy(x => x.BookingCode),
            BookingSortOption.TotalAdultsAsc => query.OrderBy(x => x.TotalAdults).ThenBy(x => x.BookingCode),
            BookingSortOption.TotalAdultsDesc => query.OrderByDescending(x => x.TotalAdults).ThenBy(x => x.BookingCode),
            _ => query.OrderByDescending(x => x.LastModifiedAt).ThenBy(x => x.BookingCode)
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

    private static int? CalculateAge(DateTime? dateOfBirth)
    {
        if (!dateOfBirth.HasValue)
        {
            return null;
        }

        var birthDate = dateOfBirth.Value.Date;
        var today = DateTime.Today;
        var age = today.Year - birthDate.Year;
        if (birthDate > today.AddYears(-age))
        {
            age--;
        }

        return age < 0 ? null : age;
    }

    private static string GetBookingTypeName(byte bookingType) => bookingType switch
    {
        0 => "Đặt trước",
        1 => "Trực tiếp tại quầy",
        _ => "Không xác định"
    };

    private static string GetBookingTypeBadgeClass(byte bookingType) => bookingType switch
    {
        0 => "text-bg-primary",
        1 => "text-bg-info",
        _ => "text-bg-secondary"
    };

    private static string GetSexName(byte? sex) => sex switch
    {
        0 => "Nam",
        1 => "Nữ",
        2 => "Khác",
        _ => "Không xác định"
    };

    private static string GetIdentityTypeName(byte? identityType) => identityType switch
    {
        0 => "CCCD",
        1 => "Giấy phép lái xe",
        2 => "Hộ chiếu",
        _ => "Không xác định"
    };

    private static string GetPaymentMethodName(byte? paymentMethod) => paymentMethod switch
    {
        0 => "Tiền mặt",
        1 => "Chuyển khoản",
        _ => "Không có"
    };

    private static string GetBookingStatusName(byte status) => status switch
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

    private static string GetBookingStatusBadgeClass(byte status) => status switch
    {
        0 => "text-bg-warning",
        1 => "text-bg-info",
        2 => "text-bg-primary",
        3 => "text-bg-success",
        4 or 5 or 6 => "text-bg-danger",
        7 or 8 => "text-bg-secondary",
        9 => "text-bg-dark",
        _ => "text-bg-secondary"
    };

    private static string GetFeeTypeName(byte feeType) => feeType switch
    {
        0 => "Phí mua đồ hộ",
        1 => "Phí hỏng hóc/mất mát",
        2 => "Khác",
        _ => "Không xác định"
    };

    private static string GetBillStatusName(byte status) => status switch
    {
        0 => "Thanh toán thành công",
        1 => "Thanh toán thất bại",
        _ => "Không xác định"
    };

    private static string GetBillStatusBadgeClass(byte status) => status switch
    {
        0 => "text-bg-success",
        1 => "text-bg-danger",
        _ => "text-bg-secondary"
    };
}
