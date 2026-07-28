using ClosedXML.Excel;
using MyJournalApp.Auth;
using MyJournalApp.Data.Dtos.Auth;
using MyJournalApp.Data.Models;
using MyJournalApp.Interface;
using MyJournalApp.Jwt;
using MyJournalApp.Result;

public class AuthService : IAuthService
{
    private readonly IStudentRepository _studentRepository;
    private readonly ITeacherRepository _teacherRepository;
    private readonly IAdminRepository _adminRepository;
    private readonly IGroupRepository _groupRepository;
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _hasher;
    private readonly IJwtProvider _jwtProvider;

    public AuthService(
        IPasswordHasher hasher,
        IJwtProvider jwtProvider,
        IStudentRepository studentRepository,
        ITeacherRepository teacherRepository,
        IAdminRepository adminRepository,
        IUserRepository userRepository,
        IGroupRepository groupRepository)
    {
        _hasher = hasher;
        _jwtProvider = jwtProvider;
        _studentRepository = studentRepository;
        _teacherRepository = teacherRepository;
        _adminRepository = adminRepository;
        _userRepository = userRepository;
        _groupRepository = groupRepository;
    }

    public async Task<IServiceResult> RegisterAsync(RegisterDto dto)
    {
        var users = await _userRepository.GetByEmail(dto.Email);
        if ( users!= null)
            return IServiceResult.Fail("Email already in use.");

        if (dto.Role == "Student")
        {
            if (!dto.GroupId.HasValue)
                return IServiceResult.Fail("GroupId is required for student role.");

            if (!await _groupRepository.ExistsAsync(dto.GroupId.Value))
                return IServiceResult.Fail("Group not found.");
        }

        var userId = Guid.NewGuid();

        var user = new User
        {
            Id = userId,
            FullName = dto.FullName,
            Email = dto.Email,
            Role = dto.Role,
            PasswordHash = _hasher.Generate(dto.Password)
        };

        await _userRepository.AddAsync(user);

        switch (dto.Role)
        {
            case "Student":

                var student = new Student
                {
                    Id = userId,
                    GroupId = dto.GroupId!.Value
                };

                await _studentRepository.AddAsync(student);

                var group = await _groupRepository.GetByIdAsync(dto.GroupId.Value);

                if (group == null)
                    return IServiceResult.Fail("Group not found.");

                group.StudentIds ??= new List<Guid>();

                if (!group.StudentIds.Contains(userId))
                    group.StudentIds.Add(userId);

                await _groupRepository.Update(group);

                break;

            case "Teacher":

                await _teacherRepository.AddAsync(new Teacher
                {
                    Id = userId
                });

                break;

            case "Admin":

                await _adminRepository.AddAsync(new Admin
                {
                    Id = userId
                });

                break;

            default:
                return IServiceResult.Fail("Invalid role.");
        }

        await _userRepository.SaveChangesAsync();

        return IServiceResult.Ok("Registered successfully.");
    }

    public async Task<ServiceResult<string>> LoginAsync(LoginDto dto)
    {
        var user = await _userRepository.GetByEmail(dto.Email);

        if (user == null)
            return ServiceResult<string>.Fail("Invalid credentials.", 401);

        if (!_hasher.Verify(dto.Password, user.PasswordHash))
            return ServiceResult<string>.Fail("Invalid credentials.", 401);

        var token = _jwtProvider.GenerateToken(user);

        return ServiceResult<string>.Ok(token, "Logged in.");
    }

    public async Task<User?> GetCurrentUserAsync(Guid userId)
    {
        return await _userRepository.GetByIdAsync(userId);
    }

    public async Task<IServiceResult> ChangePasswordAsync(Guid userId, ChangePasswordDto dto)
    {
        if (dto == null || string.IsNullOrWhiteSpace(dto.NewPassword))
            return IServiceResult.Fail("New password is required.");

        var user = await _userRepository.GetByIdAsync(userId);

        if (user == null)
            return IServiceResult.Fail("User not found.", 404);

        user.MustChangePassword = false;
        user.PasswordHash = _hasher.Generate(dto.NewPassword);

        await _userRepository.Update(user);
        await _userRepository.SaveChangesAsync();

        return IServiceResult.Ok("Password updated.");
    }

    public async Task<IServiceResult> ResetPasswordAsync(Guid adminId, ResetPasswordDto dto)
    {
        if (dto == null || (dto.UserId == null && string.IsNullOrWhiteSpace(dto.Email)))
            return IServiceResult.Fail("Specify UserId or Email.");

        var admin = await _userRepository.GetByIdAsync(adminId);

        if (admin == null)
            return IServiceResult.Fail("User not found.", 404);

        if (!string.Equals(admin.Role, "Admin", StringComparison.OrdinalIgnoreCase))
            return IServiceResult.Fail("Access denied.", 403);

        User? target = null;

        if (dto.UserId.HasValue)
            target = await _userRepository.GetByIdAsync(dto.UserId.Value);
        else
            target = await _userRepository.GetByEmail(dto.Email!);

        if (target == null)
            return IServiceResult.Fail("User not found.", 404);

        target.PasswordHash = _hasher.Generate("test");
        target.MustChangePassword = true;

        await _userRepository.Update(target);
        await _userRepository.SaveChangesAsync();

        return IServiceResult.Ok("Password reset to default.");
    }

    public async Task<IServiceResult> BulkRegisterAsync(BulkRegisterDto dto)
    {
        var validation = await ValidateBulkRequest(dto);

        if (!validation.Success)
            return validation;

        using var workbook = await OpenWorkbookAsync(dto.File);

        var worksheet = workbook.Worksheets.FirstOrDefault();

        if (worksheet == null)
            return IServiceResult.Fail("No worksheet found.");

        var rows = worksheet.RowsUsed().Skip(1).ToList();

        if (rows.Count == 0)
            return IServiceResult.Fail("The file does not contain any data.");

        var context = await LoadBulkContext(dto);

        foreach (var row in rows)
        {
            await ProcessRowAsync(row, dto, context);
        }

        await SaveBulkChangesAsync(context);

        return IServiceResult.Ok("Bulk registration complete.");
    }

    private async Task<XLWorkbook> OpenWorkbookAsync(IFormFile file)
    {
        var stream = new MemoryStream();

        await file.CopyToAsync(stream);

        stream.Position = 0;

        return new XLWorkbook(stream);
    }

    private async Task<BulkRegisterContext> LoadBulkContext(BulkRegisterDto dto)
    {
        var context = new BulkRegisterContext();

        var users = (await _userRepository.GetAllAsync()).ToList();

        context.UsersByEmail = users
            .Where(u => !string.IsNullOrWhiteSpace(u.Email))
            .GroupBy(u => u.Email!.Trim().ToLowerInvariant())
            .ToDictionary(g => g.Key, g => g.First());

        context.UsersByName = users
            .Where(u => !string.IsNullOrWhiteSpace(u.FullName))
            .GroupBy(u => u.FullName!.Trim().ToLowerInvariant())
            .ToDictionary(g => g.Key, g => g.First());

        context.Students = (await _studentRepository.GetAllAsync())
            .ToDictionary(s => s.Id);

        context.Teachers = (await _teacherRepository.GetAllAsync())
            .ToDictionary(t => t.Id);

        context.Groups = (await _groupRepository.GetAllAsync())
            .ToDictionary(g => g.Id);

        foreach (var group in context.Groups.Values)
            group.StudentIds ??= new List<Guid>();

        if (dto.Role == "Student")
        {
            context.TargetGroup = context.Groups[dto.GroupId!.Value];
        }

        return context;
    }
    private async Task<IServiceResult> ValidateBulkRequest(BulkRegisterDto dto)
    {
        if (dto.File == null || dto.File.Length == 0)
            return IServiceResult.Fail("Invalid file.");

        if (dto.Role == "Student")
        {
            if (!dto.GroupId.HasValue)
                return IServiceResult.Fail("GroupId is required for student role.");

            if (!await _groupRepository.ExistsAsync(dto.GroupId.Value))
                return IServiceResult.Fail("Target group not found.");
        }

        return IServiceResult.Ok();
    }

    private async Task ProcessRowAsync(
                        IXLRow row,
                        BulkRegisterDto dto,
                        BulkRegisterContext context)
    {
        var fullName = row.Cell(2).GetValue<string>().Trim();
        var email = row.Cell(3).GetValue<string>().Trim();

        if (string.IsNullOrWhiteSpace(fullName) &&
            string.IsNullOrWhiteSpace(email))
            return;

        User? user = null;

        if (!string.IsNullOrWhiteSpace(email))
            context.UsersByEmail.TryGetValue(email.ToLowerInvariant(), out user);

        if (user == null && !string.IsNullOrWhiteSpace(fullName))
            context.UsersByName.TryGetValue(fullName.ToLowerInvariant(), out user);

        if (user == null)
            await CreateUserAsync(fullName, email, dto, context);
        else
            await UpdateUserAsync(user, fullName, email, dto, context);
    }
    private async Task CreateUserAsync(
                        string fullName,
                        string email,
                        BulkRegisterDto dto,
                        BulkRegisterContext context)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            FullName = fullName,
            Email = email,
            Role = dto.Role,
            PasswordHash = _hasher.Generate("test")
        };

        await _userRepository.AddAsync(user);

        if (!string.IsNullOrWhiteSpace(user.Email))
            context.UsersByEmail[user.Email.Trim().ToLowerInvariant()] = user;

        if (!string.IsNullOrWhiteSpace(user.FullName))
            context.UsersByName[user.FullName.Trim().ToLowerInvariant()] = user;

        if (dto.Role == "Student")
        {
            var student = new Student
            {
                Id = user.Id,
                GroupId = context.TargetGroup!.Id
            };

            await _studentRepository.AddAsync(student);

            context.Students[user.Id] = student;

            if (!context.TargetGroup.StudentIds!.Contains(user.Id))
            {
                context.TargetGroup.StudentIds.Add(user.Id);
                context.ChangedGroups.Add(context.TargetGroup.Id);
            }
        }
        else if (dto.Role == "Teacher")
        {
            var teacher = new Teacher
            {
                Id = user.Id
            };

            await _teacherRepository.AddAsync(teacher);

            context.Teachers[user.Id] = teacher;
        }
    }

    private async Task UpdateUserAsync(
    User user,
    string fullName,
    string email,
    BulkRegisterDto dto,
    BulkRegisterContext context)
    {
        user.FullName = fullName;
        user.Email = email;

        if (dto.Role == "Student")
        {
            context.Students.TryGetValue(user.Id, out var student);

            if (student == null)
            {
                student = new Student
                {
                    Id = user.Id,
                    GroupId = context.TargetGroup!.Id
                };

                await _studentRepository.AddAsync(student);

                context.Students[user.Id] = student;
            }

            if (user.Role == "Teacher" &&
                context.Teachers.TryGetValue(user.Id, out var teacher))
            {
                await _teacherRepository.Delete(teacher);
                context.Teachers.Remove(user.Id);
            }

            if (student.GroupId != context.TargetGroup!.Id &&
                context.Groups.TryGetValue(student.GroupId, out var oldGroup))
            {
                if (oldGroup.StudentIds!.Remove(user.Id))
                    context.ChangedGroups.Add(oldGroup.Id);
            }

            student.GroupId = context.TargetGroup.Id;

            if (!context.TargetGroup.StudentIds!.Contains(user.Id))
            {
                context.TargetGroup.StudentIds.Add(user.Id);
                context.ChangedGroups.Add(context.TargetGroup.Id);
            }

            user.Role = "Student";
        }
        else if (dto.Role == "Teacher")
        {
            if (context.Students.TryGetValue(user.Id, out var student))
            {
                if (context.Groups.TryGetValue(student.GroupId, out var oldGroup))
                {
                    if (oldGroup.StudentIds!.Remove(user.Id))
                        context.ChangedGroups.Add(oldGroup.Id);
                }

                await _studentRepository.Delete(student);

                context.Students.Remove(user.Id);
            }

            if (!context.Teachers.TryGetValue(user.Id, out var teacher))
            {
                teacher = new Teacher
                {
                    Id = user.Id
                };

                await _teacherRepository.AddAsync(teacher);

                context.Teachers[user.Id] = teacher;
            }

            user.Role = "Teacher";
        }

        await _userRepository.Update(user);
    }

    private async Task SaveBulkChangesAsync(BulkRegisterContext context)
    {
        await _userRepository.SaveChangesAsync();

        foreach (var groupId in context.ChangedGroups)
        {
            await _groupRepository.Update(context.Groups[groupId]);
        }

        await _groupRepository.SaveChangesAsync();
    }

    private class BulkRegisterContext
    {
        public Dictionary<string, User> UsersByEmail { get; set; } = new();

        public Dictionary<string, User> UsersByName { get; set; } = new();

        public Dictionary<Guid, Student> Students { get; set; } = new();

        public Dictionary<Guid, Teacher> Teachers { get; set; } = new();

        public Dictionary<Guid, Group> Groups { get; set; } = new();

        public Group? TargetGroup { get; set; }

        public HashSet<Guid> ChangedGroups { get; } = new();
    }
}