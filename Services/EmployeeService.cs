using Hotel_Management_Website.IServices;
using Hotel_Management_Website.Models;
using Hotel_Management_Website.Models.ViewModels.Employee;
using Hotel_Management_Website.Models.ViewModels.UI;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;
using System.Data;

namespace Hotel_Management_Website.Services;

public sealed class EmployeeService : IEmployeeService
{
    private readonly AppDbContext _context;

    public EmployeeService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<EmployeeIndexViewModel> GetIndexAsync(
        EmployeeListFilterViewModel receptionFilter,
        EmployeeListFilterViewModel housekeepingFilter,
        CancellationToken cancellationToken = default)
    {
        var reception = await GetSectionAsync(
            position: 0,
            receptionFilter ?? new EmployeeListFilterViewModel(),
            cancellationToken);

        var housekeeping = await GetSectionAsync(
            position: 1,
            housekeepingFilter ?? new EmployeeListFilterViewModel(),
            cancellationToken);

        return new EmployeeIndexViewModel
        {
            Reception = reception,
            Housekeeping = housekeeping
        };
    }

    public async Task<EmployeeListSectionViewModel> GetSectionAsync(
        byte position,
        EmployeeListFilterViewModel filter,
        CancellationToken cancellationToken = default)
    {
        filter ??= new EmployeeListFilterViewModel();

        var normalizedSort = NormalizeSort(filter.Sort);
        var normalizedSex = NormalizeSex(filter.Sex);
        var normalizedStatus = NormalizeStatus(filter.Status);
        var search = filter.Search?.Trim() ?? string.Empty;
        var pageSize = NormalizePageSize(filter.PageSize);
        var requestedPage = Math.Max(filter.CurrentPage, 1);

        var query = _context.Employees
            .AsNoTracking()
            .Where(employee =>
                !employee.IsDeleted &&
                employee.Position == position);

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(employee =>
                employee.EmployeeCode.Contains(search) ||
                employee.EmployeeName.Contains(search));
        }

        if (normalizedSex.HasValue)
        {
            query = query.Where(employee => employee.Sex == normalizedSex.Value);
        }

        if (normalizedStatus.HasValue)
        {
            query = query.Where(employee => employee.Status == normalizedStatus.Value);
        }

        // Đếm tổng số bản ghi sau search/filter nhưng trước phân trang.
        var totalCount = await query.CountAsync(cancellationToken);

        var totalPages = totalCount == 0
            ? 1
            : (int)Math.Ceiling(totalCount / (double)pageSize);

        // Nếu URL yêu cầu trang vượt quá số trang hiện tại, đưa về trang cuối.
        var currentPage = Math.Min(requestedPage, totalPages);

        var employees = await ApplySort(query, normalizedSort)
            .Skip((currentPage - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var filterModel = new EmployeeListFilterViewModel
        {
            Search = search,
            Sort = normalizedSort,
            Sex = normalizedSex,
            Status = normalizedStatus,
            CurrentPage = currentPage,
            PageSize = pageSize
        };

        return new EmployeeListSectionViewModel
        {
            Position = position,
            Title = position == 0 ? "Lễ Tân" : "Buồng Phòng",
            Description = position == 0
                ? "Nhân viên thuộc bộ phận lễ tân."
                : "Nhân viên thuộc bộ phận buồng phòng.",
            Filter = filterModel,
            Employees = employees.Select(MapToListItem).ToList(),
            TotalCount = totalCount,
            Pagination = new PaginationViewModel
            {
                CurrentPage = currentPage,
                PageSize = pageSize,
                TotalItems = totalCount,
                PageParam = position == 0
                    ? "Reception.CurrentPage"
                    : "Housekeeping.CurrentPage",
                PageSizeParam = position == 0
                    ? "Reception.PageSize"
                    : "Housekeeping.PageSize",
                BaseUrl = "/Employee/Index"
            }
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

    private static EmployeeSortOption NormalizeSort(EmployeeSortOption sort)
    {
        return Enum.IsDefined(typeof(EmployeeSortOption), sort)
            ? sort
            : EmployeeSortOption.LastModifiedDesc;
    }

    private static byte? NormalizeSex(byte? sex)
    {
        return sex is 0 or 1 or 2 ? sex : null;
    }

    private static byte? NormalizeStatus(byte? status)
    {
        return status is 0 or 1 ? status : null;
    }

    private static IOrderedQueryable<Employee> ApplySort(
        IQueryable<Employee> query,
        EmployeeSortOption sort)
    {
        return sort switch
        {
            EmployeeSortOption.LastModifiedAsc =>
                query.OrderBy(employee => employee.LastModifiedAt)
                     .ThenBy(employee => employee.EmployeeCode),

            EmployeeSortOption.EmployeeCodeAsc =>
                query.OrderBy(employee => employee.EmployeeCode)
                     .ThenByDescending(employee => employee.LastModifiedAt),

            EmployeeSortOption.EmployeeCodeDesc =>
                query.OrderByDescending(employee => employee.EmployeeCode)
                     .ThenByDescending(employee => employee.LastModifiedAt),

            EmployeeSortOption.EmployeeNameAsc =>
                query.OrderBy(employee => employee.EmployeeName)
                     .ThenBy(employee => employee.EmployeeCode),

            EmployeeSortOption.EmployeeNameDesc =>
                query.OrderByDescending(employee => employee.EmployeeName)
                     .ThenBy(employee => employee.EmployeeCode),

            // Sinh gần hiện tại hơn => ngày sinh lớn hơn => trẻ hơn.
            EmployeeSortOption.DateOfBirthDesc =>
                query.OrderByDescending(employee => employee.DateOfBirth)
                     .ThenBy(employee => employee.EmployeeCode),

            // Sinh càng lâu => ngày sinh càng nhỏ => lớn tuổi hơn.
            EmployeeSortOption.DateOfBirthAsc =>
                query.OrderBy(employee => employee.DateOfBirth)
                     .ThenBy(employee => employee.EmployeeCode),

            EmployeeSortOption.StartDateDesc =>
                query.OrderByDescending(employee => employee.StartDate)
                     .ThenBy(employee => employee.EmployeeCode),

            EmployeeSortOption.StartDateAsc =>
                query.OrderBy(employee => employee.StartDate)
                     .ThenBy(employee => employee.EmployeeCode),

            _ =>
                query.OrderByDescending(employee => employee.LastModifiedAt)
                     .ThenBy(employee => employee.EmployeeCode)
        };
    }

    private static EmployeeListItemViewModel MapToListItem(Employee employee)
    {
        return new EmployeeListItemViewModel
        {
            Id = employee.Id,
            EmployeeCode = employee.EmployeeCode,
            EmployeeName = employee.EmployeeName,
            DateOfBirth = FormatDate(employee.DateOfBirth),
            Sex = employee.Sex,
            SexName = MapSex(employee.Sex),
            Position = employee.Position,
            PositionName = employee.Position == 0 ? "Lễ Tân" : "Buồng Phòng",
            StartDate = FormatDate(employee.StartDate),
            EndDate = FormatOptionalDate(employee.EndDate),
            Status = employee.Status,
            StatusName = employee.Status == 1 ? "Đang làm" : "Đã nghỉ",
            StatusBadgeClass = employee.Status == 1
                ? "text-bg-success"
                : "text-bg-secondary",
            LastModifiedAt = FormatDateTime(employee.LastModifiedAt)
        };
    }

    private static string MapSex(byte sex)
    {
        return sex switch
        {
            0 => "Nam",
            1 => "Nữ",
            2 => "Khác",
            _ => "Không xác định"
        };
    }

    private static string FormatDate(object? value)
    {
        return value switch
        {
            DateOnly dateOnly => dateOnly.ToString("dd/MM/yyyy"),
            DateTime dateTime => dateTime.ToString("dd/MM/yyyy"),
            _ => string.Empty
        };
    }

    private static string? FormatOptionalDate(object? value)
    {
        if (value is null)
        {
            return null;
        }

        var formatted = FormatDate(value);
        return string.IsNullOrWhiteSpace(formatted) ? null : formatted;
    }

    private static string FormatDateTime(object? value)
    {
        return value switch
        {
            DateTime dateTime => dateTime.ToString("dd/MM/yyyy HH:mm"),
            DateTimeOffset dateTimeOffset => dateTimeOffset.ToString("dd/MM/yyyy HH:mm"),
            _ => string.Empty
        };
    }

    public async Task<EmployeeDetailsViewModel?> GetDetailsAsync(
    Guid id,
    CancellationToken cancellationToken = default)
    {
        var employee = await _context.Employees
            .AsNoTracking()
            .FirstOrDefaultAsync(
                employee =>
                    employee.Id == id &&
                    !employee.IsDeleted,
                cancellationToken);

        if (employee is null)
        {
            return null;
        }

        return new EmployeeDetailsViewModel
        {
            Id = employee.Id,
            EmployeeCode = employee.EmployeeCode,
            EmployeeName = employee.EmployeeName,
            DateOfBirth = FormatDate(employee.DateOfBirth),
            Age = CalculateAge(employee.DateOfBirth),
            Sex = employee.Sex,
            SexName = MapSex(employee.Sex),
            Position = employee.Position,
            PositionName = employee.Position == 0
                ? "Lễ Tân"
                : "Buồng Phòng",
            StartDate = FormatDate(employee.StartDate),
            EndDate = FormatOptionalDate(employee.EndDate),
            Status = employee.Status,
            StatusName = employee.Status == 1
                ? "Đang làm"
                : "Đã nghỉ",
            StatusBadgeClass = employee.Status == 1
                ? "text-bg-success"
                : "text-bg-secondary",
            Note = employee.Note,
            IsDeleted = employee.IsDeleted,
            DataStatusBadgeClass = employee.IsDeleted
                ? "text-bg-danger"
                : "text-bg-success",
            LastModifiedAt = FormatDateTime(employee.LastModifiedAt),
            CreatedAt = FormatDateTime(employee.CreatedAt)
        };
    }

    private static int? CalculateAge(object? value)
    {
        DateOnly? birthDate = value switch
        {
            DateOnly dateOnly => dateOnly,
            DateTime dateTime => DateOnly.FromDateTime(dateTime),
            _ => null
        };

        if (!birthDate.HasValue)
        {
            return null;
        }

        var today = DateOnly.FromDateTime(DateTime.Today);
        var age = today.Year - birthDate.Value.Year;

        if (birthDate.Value > today.AddYears(-age))
        {
            age--;
        }

        return age < 0 ? null : age;
    }

    private static readonly Regex AutoEmployeeCodeRegex = new(
    @"^NV(?<number>[0-9]{3,})$",
    RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    public async Task<EmployeeCreateViewModel> GetCreateModelAsync(
        byte position,
        CancellationToken cancellationToken = default)
    {
        position = position is 1 ? (byte)1 : (byte)0;

        return new EmployeeCreateViewModel
        {
            EmployeeCode = await GetNextEmployeeCodeAsync(cancellationToken),
            Position = position,
            Status = 1
        };
    }

    public async Task<string> GetNextEmployeeCodeAsync(
        CancellationToken cancellationToken = default)
    {
        // Xét toàn bộ mã nhân viên hiện có, kể cả bản ghi đã soft-delete,
        // để không tái sử dụng mã cũ.
        var codes = await _context.Employees
            .AsNoTracking()
            .Where(employee => employee.EmployeeCode.StartsWith("NV"))
            .Select(employee => employee.EmployeeCode)
            .ToListAsync(cancellationToken);

        var maxNumber = 0;
        var width = 3;

        foreach (var rawCode in codes)
        {
            var code = rawCode?.Trim().ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(code))
            {
                continue;
            }

            var match = AutoEmployeeCodeRegex.Match(code);
            if (!match.Success)
            {
                continue;
            }

            if (!int.TryParse(
                    match.Groups["number"].Value,
                    out var number))
            {
                continue;
            }

            if (number > maxNumber)
            {
                maxNumber = number;
            }

            width = Math.Max(
                width,
                match.Groups["number"].Value.Length);
        }

        var nextNumber = maxNumber + 1;
        width = Math.Max(width, nextNumber.ToString().Length);

        return $"NV{nextNumber.ToString($"D{width}")}";
    }

    public async Task<(bool Succeeded, string? ErrorMessage)> CreateAsync(
        EmployeeCreateViewModel model,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        if (model is null)
        {
            return (false, "Dữ liệu nhân viên không hợp lệ.");
        }

        if (currentUserId == Guid.Empty)
        {
            return (false, "Không xác định được người dùng đang đăng nhập.");
        }

        var employeeCode = (model.EmployeeCode ?? string.Empty)
            .Trim()
            .ToUpperInvariant();

        if (string.IsNullOrWhiteSpace(employeeCode))
        {
            employeeCode = await GetNextEmployeeCodeAsync(cancellationToken);
        }

        if (employeeCode.Any(char.IsWhiteSpace))
        {
            return (false, "Mã nhân viên không được chứa dấu cách hoặc khoảng trắng.");
        }

        if (employeeCode.Length > 50)
        {
            return (false, "Mã nhân viên không được vượt quá 50 ký tự.");
        }

        if (model.Position is not 0 and not 1)
        {
            return (false, "Chức vụ không hợp lệ.");
        }

        if (model.Sex is not 0 and not 1 and not 2)
        {
            return (false, "Giới tính không hợp lệ.");
        }

        if (model.Status is not 0 and not 1)
        {
            return (false, "Trạng thái không hợp lệ.");
        }

        if (string.IsNullOrWhiteSpace(model.EmployeeName))
        {
            return (false, "Tên nhân viên không được để trống.");
        }

        model.EmployeeCode = (model.EmployeeCode ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(model.EmployeeCode))
        {
            return (false, "Mã nhân viên không được để trống.");
        }

        if (model.EmployeeCode.Any(char.IsWhiteSpace))
        {
            return (false, "Mã nhân viên không được chứa dấu cách hoặc khoảng trắng.");
        }

        if (model.EmployeeCode.Length > 50)
        {
            return (false, "Mã nhân viên không được vượt quá 50 ký tự.");
        }

        // -------------------------------------------------------------
        // Serializable để giảm nguy cơ hai người cùng sinh một mã NVxxx.
        // -------------------------------------------------------------
        await using var transaction = await _context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        try
        {
            var duplicateCode = await _context.Employees
                .AsNoTracking()
                .AnyAsync(
                    employee => employee.EmployeeCode == employeeCode,
                    cancellationToken);

            if (duplicateCode)
            {
                return (
                    false,
                    $"Mã nhân viên {employeeCode} đã tồn tại. Vui lòng chọn mã khác.");
            }

            var employee = new Employee
            {
                Id = Guid.NewGuid(),
                EmployeeCode = employeeCode,
                EmployeeName = model.EmployeeName.Trim(),
                DateOfBirth = model.DateOfBirth!.Value,
                Sex = model.Sex!.Value,
                Position = model.Position!.Value,
                StartDate = model.StartDate!.Value,
                EndDate = model.EndDate,
                Status = model.Status!.Value,
                Note = string.IsNullOrWhiteSpace(model.Note)
                    ? null
                    : model.Note.Trim(),
                IsDeleted = false,
                CreatedBy = currentUserId,
                CreatedAt = DateTime.Now,
                LastModifiedBy = currentUserId,
                LastModifiedAt = DateTime.Now
            };

            _context.Employees.Add(employee);

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
            await transaction.RollbackAsync(CancellationToken.None);
            return (
                false,
                "Không thể tạo nhân viên. Vui lòng kiểm tra dữ liệu và thử lại.");
        }
    }

    public async Task<EmployeeEditViewModel?> GetEditModelAsync(
    Guid id,
    string? returnUrl = null,
    CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
        {
            return null;
        }

        var employee = await _context.Employees
            .AsNoTracking()
            .FirstOrDefaultAsync(
                employee =>
                    employee.Id == id &&
                    !employee.IsDeleted,
                cancellationToken);

        if (employee is null)
        {
            return null;
        }

        return new EmployeeEditViewModel
        {
            Id = employee.Id,
            EmployeeCode = employee.EmployeeCode,
            EmployeeName = employee.EmployeeName,
            DateOfBirth = employee.DateOfBirth,
            Sex = employee.Sex,
            Position = employee.Position,
            StartDate = employee.StartDate,
            EndDate = employee.EndDate,
            Status = employee.Status,
            Note = employee.Note,
            ReturnUrl = returnUrl
        };
    }

    public async Task<(bool Succeeded, string? ErrorMessage)> UpdateAsync(
        EmployeeEditViewModel model,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        if (model is null || model.Id == Guid.Empty)
        {
            return (false, "Dữ liệu nhân viên không hợp lệ.");
        }

        if (currentUserId == Guid.Empty)
        {
            return (false, "Không xác định được người dùng đang đăng nhập.");
        }

        if (string.IsNullOrWhiteSpace(model.EmployeeName))
        {
            return (false, "Tên nhân viên không được để trống.");
        }

        if (model.Sex is not 0 and not 1 and not 2)
        {
            return (false, "Giới tính không hợp lệ.");
        }

        if (model.Position is not 0 and not 1)
        {
            return (false, "Chức vụ không hợp lệ.");
        }

        if (!model.DateOfBirth.HasValue)
        {
            return (false, "Ngày sinh không được để trống.");
        }

        if (!model.StartDate.HasValue)
        {
            return (false, "Ngày vào làm không được để trống.");
        }

        var today = DateOnly.FromDateTime(DateTime.Today);

        if (model.DateOfBirth.Value > today)
        {
            return (false, "Ngày sinh không được lớn hơn ngày hiện tại.");
        }

        if (model.DateOfBirth.Value >= model.StartDate.Value)
        {
            return (false, "Ngày vào làm phải sau ngày sinh.");
        }

        await using var transaction = await _context.Database.BeginTransactionAsync(
            cancellationToken);

        try
        {
            var employee = await _context.Employees
                .FirstOrDefaultAsync(
                    employee =>
                        employee.Id == model.Id &&
                        !employee.IsDeleted,
                    cancellationToken);

            if (employee is null)
            {
                return (false, "Không tìm thấy nhân viên cần sửa.");
            }

            var duplicateCode = await _context.Employees
                .AsNoTracking()
                .AnyAsync(
                    item => item.EmployeeCode == model.EmployeeCode &&
                           item.Id != employee.Id,
                    cancellationToken);

            if (duplicateCode)
            {
                return (false, "Mã nhân viên đã tồn tại.");
            }

            var originalStatus = employee.Status;

            // Status không được chỉnh sửa ở màn hình này.
            // Dùng trạng thái thực tế trong DB để kiểm tra EndDate.
            if (originalStatus == 0 && !model.EndDate.HasValue)
            {
                return (
                    false,
                    "Nhân viên đã nghỉ việc phải có ngày nghỉ việc.");
            }

            if (originalStatus == 1 && model.EndDate.HasValue)
            {
                return (
                    false,
                    "Nhân viên đang làm không được có ngày nghỉ việc.");
            }

            if (model.EndDate.HasValue &&
                model.EndDate.Value < model.StartDate.Value)
            {
                return (
                    false,
                    "Ngày nghỉ việc không được trước ngày vào làm.");
            }

            var oldPosition = employee.Position;
            var newPosition = model.Position.Value;

            employee.EmployeeCode = model.EmployeeCode;
            employee.EmployeeName = model.EmployeeName.Trim();
            employee.DateOfBirth = model.DateOfBirth.Value;
            employee.Sex = model.Sex.Value;
            employee.Position = newPosition;
            employee.StartDate = model.StartDate.Value;
            employee.EndDate = model.EndDate;
            employee.Note = string.IsNullOrWhiteSpace(model.Note)
                ? null
                : model.Note.Trim();
            employee.LastModifiedBy = currentUserId;
            employee.LastModifiedAt = DateTime.Now;

            // =========================================================
            // Đồng bộ Role của tài khoản gắn với nhân viên khi đổi
            // Position.
            //
            // Employee.Position:
            //   0 = Lễ Tân      -> Users.Role = 1
            //   1 = Buồng Phòng  -> Users.Role = 2
            //
            // Không đụng vào tài khoản Admin (Role = 0).
            // =========================================================

            if (oldPosition != newPosition)
            {
                var newUserRole = newPosition == 0
                    ? (byte)1
                    : (byte)2;

                var linkedUser = await _context.Users
                .SingleOrDefaultAsync(
                    user => user.EmployeeId == employee.Id && !user.IsDeleted
                    && (user.Role == 1 || user.Role == 2),
                    cancellationToken);

                if (linkedUser is not null)
                {
                    linkedUser.Role = newUserRole;
                    linkedUser.LastModifiedBy = currentUserId;
                    linkedUser.LastModifiedAt = DateTime.Now;
                }
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

            return (
                false,
                "Không thể lưu thay đổi nhân viên. Vui lòng kiểm tra dữ liệu và thử lại.");
        }
        catch (Exception)
        {
            await transaction.RollbackAsync(CancellationToken.None);

            return (
                false,
                "Không thể lưu thay đổi nhân viên. Vui lòng thử lại.");
        }
    }

    public async Task<(bool Succeeded, string? ErrorMessage, int DeactivatedUserCount)> LeaveAsync(
    Guid id,
    Guid currentUserId,
    CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
        {
            return (false, "Nhân viên không hợp lệ.", 0);
        }

        if (currentUserId == Guid.Empty)
        {
            return (false, "Không xác định được người dùng đang đăng nhập.", 0);
        }

        await using var transaction = await _context.Database.BeginTransactionAsync(
            cancellationToken);

        try
        {
            var employee = await _context.Employees
                .FirstOrDefaultAsync(
                    employee =>
                        employee.Id == id &&
                        !employee.IsDeleted,
                    cancellationToken);

            if (employee is null)
            {
                return (false, "Không tìm thấy nhân viên.", 0);
            }

            // Chỉ cho phép thực hiện với nhân viên đang làm.
            if (employee.Status != 1)
            {
                return (false, "Nhân viên này đã nghỉ việc hoặc không còn ở trạng thái đang làm.", 0);
            }

            var now = DateTime.Now;
            var today = DateOnly.FromDateTime(now);

            // =========================================================
            // 1. Cập nhật hồ sơ nhân viên
            // =========================================================

            employee.Status = 0;
            employee.EndDate = today;
            employee.LastModifiedBy = currentUserId;
            employee.LastModifiedAt = now;

            // =========================================================
            // 2. Dừng hoạt động tài khoản User gắn với
            //    Employee này.
            //
            //    Không xóa tài khoản và không thay đổi Role.
            //    Chỉ IsActive = 0.
            // =========================================================

            var linkedUser = await _context.Users
                .SingleOrDefaultAsync(
                    user => user.EmployeeId == employee.Id && !user.IsDeleted
                    && user.IsActive,
                    cancellationToken);


            if (linkedUser is not null)
            {
                linkedUser.IsActive = false;
                linkedUser.LastModifiedBy = currentUserId;
                linkedUser.LastModifiedAt = now;
            }

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return (true, null, linkedUser is not null ? 1 : 0);
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
                "Không thể cho nhân viên nghỉ việc. Vui lòng kiểm tra dữ liệu và thử lại.",
                0);
        }
        catch (Exception)
        {
            await transaction.RollbackAsync(CancellationToken.None);

            return (
                false,
                "Không thể cho nhân viên nghỉ việc. Vui lòng thử lại.",
                0);
        }
    }

    public async Task<(bool Succeeded, string? ErrorMessage, int ActivatedUserCount)> ReturnToWorkAsync(
    Guid id,
    Guid currentUserId,
    CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
        {
            return (false, "Nhân viên không hợp lệ.", 0);
        }

        if (currentUserId == Guid.Empty)
        {
            return (false, "Không xác định được người dùng đang đăng nhập.", 0);
        }

        await using var transaction = await _context.Database.BeginTransactionAsync(
            cancellationToken);

        try
        {
            var employee = await _context.Employees
                .FirstOrDefaultAsync(
                    employee =>
                        employee.Id == id &&
                        !employee.IsDeleted,
                    cancellationToken);

            if (employee is null)
            {
                return (false, "Không tìm thấy nhân viên.", 0);
            }

            // Chỉ cho phép thực hiện với nhân viên đang ở trạng thái đã nghỉ.
            if (employee.Status != 0)
            {
                return (false, "Nhân viên này hiện không ở trạng thái đã nghỉ việc.", 0);
            }

            var now = DateTime.Now;

            // =========================================================
            // 1. Khôi phục hồ sơ nhân viên về trạng thái đang làm
            // =========================================================

            employee.Status = 1;
            employee.EndDate = null;
            employee.LastModifiedBy = currentUserId;
            employee.LastModifiedAt = now;

            // =========================================================
            // 2. Kích hoạt tài khoản User gắn với Employee này
            //
            // Không tạo mới tài khoản.
            // Không thay đổi Role.
            // Chỉ IsActive = 1.
            // =========================================================

            var linkedUser = await _context.Users
                .SingleOrDefaultAsync(
                    user => user.EmployeeId == employee.Id && !user.IsDeleted
                    && !user.IsActive,
                    cancellationToken);

            if (linkedUser is not null)
            {
                linkedUser.IsActive = true;
                linkedUser.LastModifiedBy = currentUserId;
                linkedUser.LastModifiedAt = now;
            }

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return (true, null, linkedUser is not null ? 1 : 0);
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
                "Không thể cho nhân viên trở lại làm việc. Vui lòng kiểm tra dữ liệu và thử lại.",
                0);
        }
        catch (Exception)
        {
            await transaction.RollbackAsync(CancellationToken.None);

            return (
                false,
                "Không thể cho nhân viên trở lại làm việc. Vui lòng thử lại.",
                0);
        }
    }

    public async Task<(bool Succeeded, string? ErrorMessage, int DeactivatedUserCount)> DeleteAsync(
    Guid id,
    Guid currentUserId,
    CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
        {
            return (false, "Nhân viên không hợp lệ.", 0);
        }

        if (currentUserId == Guid.Empty)
        {
            return (false, "Không xác định được người dùng đang đăng nhập.", 0);
        }

        await using var transaction = await _context.Database.BeginTransactionAsync(
            cancellationToken);

        try
        {
            var employee = await _context.Employees
                .FirstOrDefaultAsync(
                    employee =>
                        employee.Id == id &&
                        !employee.IsDeleted,
                    cancellationToken);

            if (employee is null)
            {
                return (false, "Không tìm thấy nhân viên.", 0);
            }

            // =========================================================
            // CHỈ CHO PHÉP XÓA NHÂN VIÊN ĐÃ NGHỈ VIỆC
            // =========================================================

            if (employee.Status != 0)
            {
                return (
                    false,
                    "Không thể xóa nhân viên đang làm việc. Vui lòng cho nhân viên nghỉ việc trước.",
                    0);
            }

            var now = DateTime.Now;

            // =========================================================
            // 1. SOFT DELETE EMPLOYEE
            // =========================================================
            // Không dùng _context.Employees.Remove(employee).
            // Giữ lại hồ sơ để bảo toàn lịch sử và quan hệ dữ liệu.

            employee.IsDeleted = true;
            employee.LastModifiedBy = currentUserId;
            employee.LastModifiedAt = now;

            // =========================================================
            // 2. VÔ HIỆU HÓA TÀI KHOẢN ĐANG GẮN VỚI EMPLOYEE
            // =========================================================
            // Không xóa User và không thay đổi Role.
            // Chỉ dừng đăng nhập: IsActive = false.

            var linkedUser = await _context.Users
                .SingleOrDefaultAsync(
                    user => user.EmployeeId == employee.Id && !user.IsDeleted,
                    cancellationToken);

            if (linkedUser is not null)
            {
                linkedUser.EmployeeId = null; // Ngắt liên kết với Employee đã xóa
                linkedUser.IsActive = false;
                linkedUser.LastModifiedBy = currentUserId;
                linkedUser.LastModifiedAt = now;
            }

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return (true, null, linkedUser is not null ? 1 : 0);
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
                "Không thể xóa nhân viên. Vui lòng kiểm tra dữ liệu và thử lại.",
                0);
        }
        catch (Exception)
        {
            await transaction.RollbackAsync(CancellationToken.None);

            return (
                false,
                "Không thể xóa nhân viên. Vui lòng thử lại.",
                0);
        }
    }
}
