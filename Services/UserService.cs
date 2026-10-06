using Hotel_Management_Website.IServices;
using Hotel_Management_Website.Models;
using Hotel_Management_Website.Models.ViewModels.User;
using Hotel_Management_Website.Models.ViewModels.UI;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Hotel_Management_Website.Services;

public sealed class UserService : IUserService
{
    private readonly AppDbContext _context;
    private readonly IPasswordHasher<User> _passwordHasher;

    public UserService(
        AppDbContext context,
        IPasswordHasher<User> passwordHasher)
    {
        _context = context;
        _passwordHasher = passwordHasher;
    }

    public async Task<byte?> GetUserRoleCodeAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
            return null;

        return await _context.Users
            .AsNoTracking()
            .Where(x => x.Id == userId && !x.IsDeleted)
            .Select(x => (byte?)x.Role)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<UserIndexViewModel> GetIndexAsync(
        Guid currentUserId,
        byte currentUserRole,
        UserListFilterViewModel adminFilter,
        UserListFilterViewModel receptionFilter,
        UserListFilterViewModel housekeepingFilter,
        CancellationToken cancellationToken = default)
    {
        var isFullAdmin = currentUserRole == 4;

        var adminUsers = isFullAdmin
            ? await GetSectionAsync(
                role: 0,
                currentUserId: currentUserId,
                filter: adminFilter,
                includeEmployeeCode: false,
                showCreate: true,
                title: "Admin",
                description: "Tài khoản quản trị hệ thống.",
                icon: "bi-person-gear",
                cancellationToken: cancellationToken)
            : CreateEmptySection(
                role: 0,
                title: "Admin",
                description: "Tài khoản quản trị hệ thống.",
                icon: "bi-person-gear");

        var receptionUsers = await GetSectionAsync(
            role: 1,
            currentUserId: currentUserId,
            filter: receptionFilter,
            includeEmployeeCode: true,
            showCreate: true,
            title: "Lễ Tân",
            description: "Tài khoản dành cho bộ phận lễ tân.",
            icon: "bi-person-vcard",
            cancellationToken: cancellationToken);

        var housekeepingUsers = await GetSectionAsync(
            role: 2,
            currentUserId: currentUserId,
            filter: housekeepingFilter,
            includeEmployeeCode: true,
            showCreate: true,
            title: "Buồng Phòng",
            description: "Tài khoản dành cho bộ phận buồng phòng.",
            icon: "bi-person-check",
            cancellationToken: cancellationToken);

        return new UserIndexViewModel
        {
            IsFullAdmin = isFullAdmin,
            AdminUsers = adminUsers,
            ReceptionUsers = receptionUsers,
            HousekeepingUsers = housekeepingUsers
        };
    }

    public async Task<IReadOnlyList<UserEmployeeOptionViewModel>> GetEligibleEmployeesAsync(
        CancellationToken cancellationToken = default)
    {
        return await _context.Employees
            .AsNoTracking()
            .Where(x => !x.IsDeleted && x.Status == 1 && (x.Position == 0 || x.Position == 1))
            .OrderBy(x => x.Position)
            .ThenBy(x => x.EmployeeCode)
            .Select(x => new UserEmployeeOptionViewModel
            {
                Id = x.Id,
                EmployeeCode = x.EmployeeCode,
                EmployeeName = x.EmployeeName,
                Position = x.Position,
                IsActive = x.Status == 1,
                IsAssigned = _context.Users.Any(user =>
                    !user.IsDeleted &&
                    user.EmployeeId == x.Id)
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<string?> GetEmployeeDisplayNameAsync(
        Guid employeeId,
        CancellationToken cancellationToken = default)
    {
        if (employeeId == Guid.Empty)
            return null;

        return await _context.Employees
            .AsNoTracking()
            .Where(x => x.Id == employeeId && !x.IsDeleted)
            .Select(x => x.EmployeeName)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<UserEmployeeOptionViewModel>> GetEmployeesForEditAsync(
        byte role,
        Guid? currentEmployeeId,
        CancellationToken cancellationToken = default)
    {
        _ = role;

        var employees = await _context.Employees
            .AsNoTracking()
            .Where(x => !x.IsDeleted &&
                        (x.Status == 1 || (currentEmployeeId.HasValue && x.Id == currentEmployeeId.Value)) &&
                        (x.Position == 0 || x.Position == 1))
            .Select(x => new UserEmployeeOptionViewModel
            {
                Id = x.Id,
                EmployeeCode = x.EmployeeCode,
                EmployeeName = x.EmployeeName,
                Position = x.Position,
                IsActive = x.Status == 1,
                IsAssigned = _context.Users.Any(user =>
                    !user.IsDeleted &&
                    user.EmployeeId == x.Id)
            })
            .ToListAsync(cancellationToken);

        // Đảm bảo nhân viên hiện tại vẫn xuất hiện khi tài khoản đang gán
        // cho một nhân viên đã nghỉ, để chỉnh sửa ghi chú/mật khẩu không vô tình gỡ gán.
        if (currentEmployeeId.HasValue &&
            currentEmployeeId.Value != Guid.Empty &&
            employees.All(x => x.Id != currentEmployeeId.Value))
        {
            var currentEmployee = await _context.Employees
                .AsNoTracking()
                .Where(x => x.Id == currentEmployeeId.Value && !x.IsDeleted)
                .Select(x => new UserEmployeeOptionViewModel
                {
                    Id = x.Id,
                    EmployeeCode = x.EmployeeCode,
                    EmployeeName = x.EmployeeName,
                    Position = x.Position,
                    IsActive = x.Status == 1,
                    IsAssigned = _context.Users.Any(user =>
                        !user.IsDeleted &&
                        user.EmployeeId == x.Id)
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (currentEmployee is not null)
                employees.Add(currentEmployee);
        }

        return employees
            .OrderBy(x => x.Position)
            .ThenBy(x => x.EmployeeCode)
            .ToArray();
    }

    public async Task<UserDetailsViewModel?> GetDetailsAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
            return null;

        var user = await _context.Users
            .AsNoTracking()
            .Include(x => x.Employee)
            .FirstOrDefaultAsync(
                x => x.Id == id && !x.IsDeleted,
                cancellationToken);

        if (user is null)
            return null;

        var roleName = user.Role switch
        {
            0 => "Admin",
            1 => "Lễ Tân",
            2 => "Buồng Phòng",
            4 => "Admin toàn quyền",
            _ => "Không xác định"
        };

        return new UserDetailsViewModel
        {
            Id = user.Id,
            UserCode = user.UserCode,
            UserName = user.UserName,
            Role = user.Role,
            RoleName = roleName,
            IsActive = user.IsActive,
            StatusName = user.IsActive ? "Đang hoạt động" : "Dừng hoạt động",
            StatusBadgeClass = user.IsActive ? "text-bg-success" : "text-bg-secondary",
            EmployeeCode = user.Employee?.EmployeeCode,
            EmployeeName = user.Employee?.EmployeeName,
            Note = user.Note,
            CreatedAt = FormatDateTime(user.CreatedAt),
            LastModifiedAt = FormatDateTime(user.LastModifiedAt)
        };
    }

    private static string FormatDateTime(DateTime value)
    {
        return value.ToString("dd/MM/yyyy HH:mm:ss");
    }

    public async Task<UserEditViewModel?> GetEditModelAsync(
        Guid id,
        Guid currentUserId,
        byte currentUserRole,
        string? returnUrl,
        CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty || currentUserId == Guid.Empty || id == currentUserId)
            return null;

        if (currentUserRole is not 0 and not 4)
            return null;

        var user = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.Id == id &&
                     !x.IsDeleted &&
                     x.Role != 4,
                cancellationToken);

        if (user is null)
            return null;

        if (user.Role == 0 && currentUserRole != 4)
            return null;

        return new UserEditViewModel
        {
            Id = user.Id,
            UserCode = user.UserCode,
            UserName = user.UserName,
            Role = user.Role,
            EmployeeId = user.EmployeeId,
            IsActive = user.IsActive,
            Note = user.Note,
            CanEditAdmin = currentUserRole == 4,
            ReturnUrl = returnUrl,
            Employees = await GetEmployeesForEditAsync(
                user.Role,
                user.EmployeeId,
                cancellationToken)
        };
    }

    public async Task<UserCreateViewModel> PrepareCreateAsync(
        byte currentUserRole,
        byte requestedRole,
        CancellationToken cancellationToken = default)
    {
        var allowedRole = NormalizeRequestedRole(currentUserRole, requestedRole);

        return new UserCreateViewModel
        {
            Role = allowedRole,
            IsActive = true,
            UserCode = await GenerateNextUserCodeAsync(cancellationToken),
            CanCreateAdmin = currentUserRole == 4,
            Employees = await GetEmployeesForEditAsync(
                role: allowedRole,
                currentEmployeeId: null,
                cancellationToken: cancellationToken)
        };
    }

    public async Task<(bool Succeeded, string? ErrorMessage, string? UserCode)> CreateAsync(
        UserCreateViewModel model,
        Guid currentUserId,
        byte currentUserRole,
        CancellationToken cancellationToken = default)
    {
        if (currentUserId == Guid.Empty)
            return (false, "Không xác định được người dùng đang đăng nhập.", null);

        if (currentUserRole is not 0 and not 4)
            return (false, "Bạn không có quyền tạo tài khoản.", null);

        var role = model.Role;
        if (role is not 0 and not 1 and not 2)
            return (false, "Vai trò tài khoản không hợp lệ.", null);

        if (role == 0 && currentUserRole != 4)
            return (false, "Chỉ Admin toàn quyền mới được tạo tài khoản Admin.", null);

        var userCode = (model.UserCode ?? string.Empty).Trim().ToUpperInvariant();

        if (string.IsNullOrWhiteSpace(userCode))
            userCode = await GenerateNextUserCodeAsync(cancellationToken);

        if (!IsValidUserCode(userCode))
            return (false, "Mã tài khoản không được chứa dấu cách hoặc khoảng trắng.", null);

        if (userCode.Length > 50)
            return (false, "Mã tài khoản không được vượt quá 50 ký tự.", null);

        var duplicateCode = await _context.Users
            .AsNoTracking()
            .AnyAsync(x => x.UserCode == userCode, cancellationToken);

        if (duplicateCode)
            return (false, "Mã tài khoản đã tồn tại.", null);

        var userName = (model.UserName ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(userName))
            return (false, "Vui lòng nhập tên đăng nhập.", null);

        var duplicateName = await _context.Users
            .AsNoTracking()
            .AnyAsync(x => x.UserName == userName, cancellationToken);

        if (duplicateName)
            return (false, "Tên đăng nhập đã tồn tại.", null);

        if (string.IsNullOrEmpty(model.Password) || model.Password.Length < 5)
            return (false, "Mật khẩu phải có ít nhất 5 ký tự.", null);

        Guid? employeeId = null;

        if (role == 0)
        {
            if (model.EmployeeId.HasValue && model.EmployeeId.Value != Guid.Empty)
                return (false, "Tài khoản Admin không được gán nhân viên.", null);
        }
        else if (model.EmployeeId.HasValue && model.EmployeeId.Value != Guid.Empty)
        {
            // Gán nhân viên là tùy chọn. Nếu có chọn, hệ thống vẫn kiểm tra
            // nhân viên tồn tại, đang làm việc và đúng bộ phận với vai trò tài khoản.
            var employee = await _context.Employees
                .FirstOrDefaultAsync(
                    x => x.Id == model.EmployeeId.Value && !x.IsDeleted,
                    cancellationToken);

            if (employee is null)
                return (false, "Nhân viên được chọn không tồn tại hoặc đã bị xóa.", null);

            if (employee.Status != 1)
                return (false, "Chỉ nhân viên đang làm việc mới được gán tài khoản.", null);

            var assignedToOtherUser = await _context.Users
                .AsNoTracking()
                .AnyAsync(
                    x => !x.IsDeleted && x.EmployeeId == employee.Id,
                    cancellationToken);

            if (assignedToOtherUser)
                return (false, "Nhân viên này đã được gán cho một tài khoản khác.", null);

            var expectedPosition = role == 1 ? (byte)0 : (byte)1;
            if (employee.Position != expectedPosition)
            {
                return (false,
                    role == 1
                        ? "Tài khoản Lễ Tân chỉ được gán cho nhân viên thuộc bộ phận Lễ Tân."
                        : "Tài khoản Buồng Phòng chỉ được gán cho nhân viên thuộc bộ phận Buồng Phòng.",
                    null);
            }

            employeeId = employee.Id;
        }

        var now = DateTime.Now;
        var user = new User
        {
            Id = Guid.NewGuid(),
            UserCode = userCode,
            UserName = userName,
            Role = role,
            IsActive = model.IsActive,
            EmployeeId = employeeId,
            Note = string.IsNullOrWhiteSpace(model.Note)
                ? null
                : model.Note.Trim(),
            IsDeleted = false,
            CreatedBy = currentUserId,
            CreatedAt = now,
            LastModifiedBy = currentUserId,
            LastModifiedAt = now
        };

        user.Password = _passwordHasher.HashPassword(user, model.Password);

        try
        {
            _context.Users.Add(user);
            await _context.SaveChangesAsync(cancellationToken);
            return (true, null, userCode);
        }
        catch (DbUpdateException)
        {
            if (await _context.Users.AnyAsync(x => x.UserCode == userCode, cancellationToken))
                return (false, "Mã tài khoản vừa được sử dụng. Vui lòng thử lại.", null);

            if (await _context.Users.AnyAsync(x => x.UserName == userName, cancellationToken))
                return (false, "Tên đăng nhập vừa được sử dụng. Vui lòng thử lại.", null);

            return (false, "Không thể tạo tài khoản. Vui lòng thử lại.", null);
        }
        catch
        {
            return (false, "Không thể tạo tài khoản. Vui lòng thử lại.", null);
        }
    }

    public async Task<(bool Succeeded, string? ErrorMessage)> UpdateAsync(
        UserEditViewModel model,
        Guid currentUserId,
        byte currentUserRole,
        CancellationToken cancellationToken = default)
    {
        if (currentUserId == Guid.Empty)
            return (false, "Không xác định được người dùng đang đăng nhập.");

        if (currentUserRole is not 0 and not 4)
            return (false, "Bạn không có quyền sửa tài khoản.");

        if (model.Id == Guid.Empty || model.Id == currentUserId)
            return (false, "Tài khoản không hợp lệ.");

        var user = await _context.Users
            .FirstOrDefaultAsync(
                x => x.Id == model.Id &&
                     !x.IsDeleted &&
                     x.Role != 4,
                cancellationToken);

        if (user is null)
            return (false, "Tài khoản không tồn tại hoặc đã bị xóa.");

        if (user.Role == 0 && currentUserRole != 4)
            return (false, "Chỉ Admin toàn quyền mới được sửa tài khoản Admin.");

        var userCode = (model.UserCode ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(userCode))
            return (false, "Mã tài khoản không được để trống.");

        if (userCode.Length > 50)
            return (false, "Mã tài khoản không được vượt quá 50 ký tự.");

        if (userCode.Any(char.IsWhiteSpace))
            return (false, "Mã tài khoản không được chứa dấu cách hoặc khoảng trắng.");

        var duplicateCode = await _context.Users
            .AsNoTracking()
            .AnyAsync(
                x => x.UserCode == userCode && x.Id != user.Id,
                cancellationToken);

        if (duplicateCode)
            return (false, "Mã tài khoản đã tồn tại.");

        if (model.Role is not 0 and not 1 and not 2)
            return (false, "Vai trò tài khoản không hợp lệ.");

        if (model.Role == 0 && currentUserRole != 4)
            return (false, "Chỉ Admin toàn quyền mới được gán vai trò Admin.");

        var userName = (model.UserName ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(userName))
            return (false, "Vui lòng nhập tên đăng nhập.");

        if (userName.Length > 100)
            return (false, "Tên đăng nhập không được vượt quá 100 ký tự.");

        var duplicateName = await _context.Users
            .AsNoTracking()
            .AnyAsync(
                x => !x.IsDeleted &&
                     x.Id != user.Id &&
                     x.UserName == userName,
                cancellationToken);

        if (duplicateName)
            return (false, "Tên đăng nhập đã tồn tại.");

        if (!string.IsNullOrEmpty(model.Password) && model.Password.Length < 5)
            return (false, "Mật khẩu mới phải có ít nhất 5 ký tự.");

        Guid? employeeId = null;

        if (model.Role != 0 && model.EmployeeId.HasValue && model.EmployeeId.Value != Guid.Empty)
        {
            var employee = await _context.Employees
                .FirstOrDefaultAsync(
                    x => x.Id == model.EmployeeId.Value && !x.IsDeleted,
                    cancellationToken);

            if (employee is null)
                return (false, "Nhân viên được chọn không tồn tại hoặc đã bị xóa.");

            var expectedPosition = model.Role == 1 ? (byte)0 : (byte)1;
            if (employee.Position != expectedPosition)
            {
                return (false,
                    model.Role == 1
                        ? "Tài khoản Lễ Tân chỉ được gán cho nhân viên thuộc bộ phận Lễ Tân."
                        : "Tài khoản Buồng Phòng chỉ được gán cho nhân viên thuộc bộ phận Buồng Phòng.");
            }

            var isKeepingCurrentEmployee = user.EmployeeId.HasValue &&
                                           user.EmployeeId.Value == employee.Id;

            if (employee.Status != 1 && !isKeepingCurrentEmployee)
                return (false, "Chỉ nhân viên đang làm việc mới được gán cho tài khoản.");

            var assignedToOtherUser = await _context.Users
                .AsNoTracking()
                .AnyAsync(
                    x => !x.IsDeleted &&
                         x.Id != user.Id &&
                         x.EmployeeId == employee.Id,
                    cancellationToken);

            if (assignedToOtherUser)
                return (false, "Nhân viên này đã được gán cho một tài khoản khác.");

            employeeId = employee.Id;
        }

        user.UserCode = userCode;
        user.UserName = userName;
        user.Role = model.Role;
        user.EmployeeId = model.Role == 0 ? null : employeeId;
        user.Note = string.IsNullOrWhiteSpace(model.Note)
            ? null
            : model.Note.Trim();
        user.LastModifiedBy = currentUserId;
        user.LastModifiedAt = DateTime.Now;

        if (!string.IsNullOrEmpty(model.Password))
            user.Password = _passwordHasher.HashPassword(user, model.Password);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            return (true, null);
        }
        catch (DbUpdateException)
        {
            if (await _context.Users.AnyAsync(x => !x.IsDeleted && x.Id != user.Id && x.UserName == userName, cancellationToken))
                return (false, "Tên đăng nhập vừa được sử dụng. Vui lòng thử lại.");

            return (false, "Không thể lưu thay đổi tài khoản. Vui lòng thử lại.");
        }
    }

    public async Task<(bool Succeeded, string? ErrorMessage)> LockAsync(
        Guid id,
        Guid currentUserId,
        byte currentUserRole,
        CancellationToken cancellationToken = default)
    {
        var user = await GetManageableUserAsync(id, currentUserId, currentUserRole, cancellationToken);
        if (user is null)
            return (false, "Tài khoản không tồn tại hoặc không thể thao tác.");

        if (!user.IsActive)
            return (false, "Tài khoản đã ở trạng thái dừng hoạt động.");

        user.IsActive = false;
        user.LastModifiedBy = currentUserId;
        user.LastModifiedAt = DateTime.Now;

        await _context.SaveChangesAsync(cancellationToken);
        return (true, null);
    }

    public async Task<(bool Succeeded, string? ErrorMessage)> ReopenAsync(
        Guid id,
        Guid currentUserId,
        byte currentUserRole,
        CancellationToken cancellationToken = default)
    {
        var user = await GetManageableUserAsync(id, currentUserId, currentUserRole, cancellationToken);
        if (user is null)
            return (false, "Tài khoản không tồn tại hoặc không thể thao tác.");

        if (user.IsActive)
            return (false, "Tài khoản đang hoạt động.");

        if (user.EmployeeId.HasValue)
        {
            var employee = await _context.Employees
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x => x.Id == user.EmployeeId.Value && !x.IsDeleted,
                    cancellationToken);

            if (employee is null)
                return (false, "Không thể mở lại tài khoản vì nhân viên được gán không còn tồn tại.");

            if (employee.Status != 1)
                return (false, "Không thể mở lại tài khoản vì nhân viên được gán đã nghỉ việc.");
        }

        user.IsActive = true;
        user.LastModifiedBy = currentUserId;
        user.LastModifiedAt = DateTime.Now;

        await _context.SaveChangesAsync(cancellationToken);
        return (true, null);
    }

    public async Task<(bool Succeeded, string? ErrorMessage)> DeleteAsync(
        Guid id,
        Guid currentUserId,
        byte currentUserRole,
        CancellationToken cancellationToken = default)
    {
        var user = await GetManageableUserAsync(id, currentUserId, currentUserRole, cancellationToken);
        if (user is null)
            return (false, "Tài khoản không tồn tại hoặc không thể thao tác.");

        if (user.IsActive)
            return (false, "Chỉ tài khoản đã dừng hoạt động mới được xóa.");

        user.IsDeleted = true;
        user.LastModifiedBy = currentUserId;
        user.LastModifiedAt = DateTime.Now;

        await _context.SaveChangesAsync(cancellationToken);
        return (true, null);
    }

    private async Task<User?> GetManageableUserAsync(
        Guid id,
        Guid currentUserId,
        byte currentUserRole,
        CancellationToken cancellationToken)
    {
        if (id == Guid.Empty || currentUserId == Guid.Empty || id == currentUserId)
            return null;

        if (currentUserRole is not 0 and not 4)
            return null;

        var user = await _context.Users
            .FirstOrDefaultAsync(
                x => x.Id == id &&
                     !x.IsDeleted &&
                     x.Role != 4,
                cancellationToken);

        if (user is null)
            return null;

        if (user.Role == 0 && currentUserRole != 4)
            return null;

        return user;
    }

    private async Task<UserListSectionViewModel> GetSectionAsync(
        byte role,
        Guid currentUserId,
        UserListFilterViewModel filter,
        bool includeEmployeeCode,
        bool showCreate,
        string title,
        string description,
        string icon,
        CancellationToken cancellationToken)
    {
        filter ??= new UserListFilterViewModel();

        var normalizedSearch = (filter.Search ?? string.Empty).Trim();
        var normalizedSort = NormalizeSort(filter.Sort);
        var normalizedIsActive = NormalizeIsActive(filter.IsActive);
        var pageSize = NormalizePageSize(filter.PageSize);
        var requestedPage = Math.Max(filter.CurrentPage, 1);

        var query = _context.Users
            .AsNoTracking()
            .Where(x =>
                !x.IsDeleted &&
                x.Role == role &&
                x.Id != currentUserId);

        if (!string.IsNullOrWhiteSpace(normalizedSearch))
        {
            query = query.Where(x =>
                x.UserCode.Contains(normalizedSearch) ||
                x.UserName.Contains(normalizedSearch));
        }

        if (normalizedIsActive.HasValue)
        {
            var isActive = normalizedIsActive.Value == 1;
            query = query.Where(x => x.IsActive == isActive);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var totalPages = totalCount == 0
            ? 1
            : (int)Math.Ceiling(totalCount / (double)pageSize);

        var currentPage = Math.Min(requestedPage, totalPages);

        // Giữ nguyên semantics sort hiện tại của User: UserCode được so theo phần số sau U.
        var items = await query
            .Select(x => new UserListItemViewModel
            {
                Id = x.Id,
                UserCode = x.UserCode,
                UserName = x.UserName,
                IsActive = x.IsActive,
                EmployeeCode = includeEmployeeCode
                    ? x.Employee != null ? x.Employee.EmployeeCode : null
                    : null,
                LastModifiedAt = x.LastModifiedAt
            })
            .ToListAsync(cancellationToken);

        var sortedItems = ApplySort(items, normalizedSort);
        var pagedItems = sortedItems
            .Skip((currentPage - 1) * pageSize)
            .Take(pageSize)
            .ToArray();

        var normalizedFilter = new UserListFilterViewModel
        {
            Search = normalizedSearch,
            Sort = normalizedSort,
            IsActive = normalizedIsActive,
            CurrentPage = currentPage,
            PageSize = pageSize
        };

        return new UserListSectionViewModel
        {
            Role = role,
            Title = title,
            Description = description,
            Icon = icon,
            Filter = normalizedFilter,
            Users = pagedItems,
            TotalCount = totalCount,
            Pagination = new PaginationViewModel
            {
                CurrentPage = currentPage,
                PageSize = pageSize,
                TotalItems = totalCount,
                PageParam = GetPageParam(role),
                PageSizeParam = GetPageSizeParam(role),
                BaseUrl = "/User/Index"
            },
            ShowCreate = showCreate
        };
    }

    private static UserListSectionViewModel CreateEmptySection(
        byte role,
        string title,
        string description,
        string icon)
    {
        return new UserListSectionViewModel
        {
            Role = role,
            Title = title,
            Description = description,
            Icon = icon,
            Filter = new UserListFilterViewModel(),
            Users = Array.Empty<UserListItemViewModel>(),
            TotalCount = 0,
            Pagination = new PaginationViewModel
            {
                CurrentPage = 1,
                PageSize = 10,
                TotalItems = 0,
                PageParam = GetPageParam(role),
                PageSizeParam = GetPageSizeParam(role),
                BaseUrl = "/User/Index"
            },
            ShowCreate = false
        };
    }

    private static string GetPageParam(byte role)
    {
        return role switch
        {
            0 => "Admin.CurrentPage",
            1 => "Reception.CurrentPage",
            2 => "Housekeeping.CurrentPage",
            _ => "CurrentPage"
        };
    }

    private static string GetPageSizeParam(byte role)
    {
        return role switch
        {
            0 => "Admin.PageSize",
            1 => "Reception.PageSize",
            2 => "Housekeeping.PageSize",
            _ => "PageSize"
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

    private static byte? NormalizeIsActive(byte? isActive)
    {
        return isActive is 0 or 1
            ? isActive
            : null;
    }

    private static UserSortOption NormalizeSort(UserSortOption sort)
    {
        return Enum.IsDefined(typeof(UserSortOption), sort)
            ? sort
            : UserSortOption.LastModifiedDesc;
    }

    private static IReadOnlyList<UserListItemViewModel> ApplySort(
        IReadOnlyList<UserListItemViewModel> items,
        UserSortOption sort)
    {
        return sort switch
        {
            UserSortOption.LastModifiedAsc => items
                .OrderBy(x => x.LastModifiedAt)
                .ThenBy(x => x.UserCode, StringComparer.OrdinalIgnoreCase)
                .ToArray(),

            UserSortOption.UserCodeDesc => items
                .OrderByDescending(x => ParseUserNumber(x.UserCode))
                .ThenBy(x => x.UserCode, StringComparer.OrdinalIgnoreCase)
                .ToArray(),

            UserSortOption.UserCodeAsc => items
                .OrderBy(x => ParseUserNumber(x.UserCode))
                .ThenBy(x => x.UserCode, StringComparer.OrdinalIgnoreCase)
                .ToArray(),

            UserSortOption.UserNameAsc => items
                .OrderBy(x => x.UserName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(x => x.UserCode, StringComparer.OrdinalIgnoreCase)
                .ToArray(),

            UserSortOption.UserNameDesc => items
                .OrderByDescending(x => x.UserName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(x => x.UserCode, StringComparer.OrdinalIgnoreCase)
                .ToArray(),

            _ => items
                .OrderByDescending(x => x.LastModifiedAt)
                .ThenBy(x => x.UserCode, StringComparer.OrdinalIgnoreCase)
                .ToArray()
        };
    }

    public async Task<string> GenerateNextUserCodeAsync(
        CancellationToken cancellationToken = default)
    {
        var codes = await _context.Users
            .AsNoTracking()
            .Select(x => x.UserCode)
            .ToListAsync(cancellationToken);

        long maxNumber = 0;

        foreach (var code in codes)
        {
            if (TryParseUserNumber(code, out var number) && number > maxNumber)
                maxNumber = number;
        }

        return $"U{maxNumber + 1}";
    }

    private static bool TryParseUserNumber(string? code, out long number)
    {
        number = 0;

        if (string.IsNullOrWhiteSpace(code))
            return false;

        var text = code.Trim();
        if (text.Length < 2 || text[0] != 'U')
            return false;

        return long.TryParse(
            text[1..],
            System.Globalization.NumberStyles.None,
            System.Globalization.CultureInfo.InvariantCulture,
            out number) && number >= 0;
    }

    private static long ParseUserNumber(string? code)
    {
        return TryParseUserNumber(code, out var number)
            ? number
            : -1;
    }

    private static bool IsValidUserCode(string? userCode)
    {
        if (string.IsNullOrWhiteSpace(userCode))
            return false;

        return !userCode.Any(char.IsWhiteSpace);
    }

    private static byte NormalizeRequestedRole(byte currentUserRole, byte requestedRole)
    {
        if (requestedRole is 0 or 1 or 2)
        {
            if (requestedRole == 0 && currentUserRole != 4)
                return 1;

            return requestedRole;
        }

        return 1;
    }
}
