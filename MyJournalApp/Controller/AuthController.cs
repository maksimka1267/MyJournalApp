using ClosedXML.Excel;
using DocumentFormat.OpenXml.Office2010.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyJournalApp.Auth;
using MyJournalApp.Data;
using MyJournalApp.Data.Models;
using MyJournalApp.Interface;
using MyJournalApp.Jwt;
using System.Security.Claims;

namespace MyJournalApp.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IStudentRepository _studentRepository;
    private readonly ITeacherRepository _teacherRepository;
    private readonly IAdminRepository _adminRepository;
    private readonly IGroupRepository _groupRepository;
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _hasher;
    private readonly IJwtProvider _jwtProvider;

    public AuthController(IPasswordHasher hasher,
                          IJwtProvider jwtProvider,
                          IStudentRepository studentRepository,
                          ITeacherRepository teacherRepository,
                          IAdminRepository adminRepository,
                          IUserRepository userRepository,
                          IGroupRepository groupRepository)
    {
        _adminRepository = adminRepository;
        _studentRepository = studentRepository;
        _teacherRepository = teacherRepository;
        _hasher = hasher;
        _jwtProvider = jwtProvider;
        _userRepository = userRepository;
        _groupRepository = groupRepository;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromForm] RegisterDto dto)
    {
        // 1) Валидация
        if (await _userRepository.GetByEmail(dto.Email) != null)
            return BadRequest("Email already in use");

        if (dto.Role == "Student")
        {
            if (dto.GroupId is null) return BadRequest("GroupId is required for student role");
            var groupExists = await _groupRepository.ExistsAsync(dto.GroupId.Value);
            if (!groupExists) return BadRequest("Group not found");
        }

        // 2) Создаем User
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

        // 3) Создаем профиль по роли
        switch (dto.Role)
        {
            case "Student":
                if (!dto.GroupId.HasValue)
                    return BadRequest("GroupId is required for student role");
                await _studentRepository.AddAsync(new Student
                {
                    Id = userId,
                    GroupId = dto.GroupId.Value
                });

                var group = await _groupRepository.GetByIdAsync(dto.GroupId.Value);
                if (group == null)
                    return BadRequest("Group not found");

                // вот это — ключевой момент
                group.StudentIds ??= new List<Guid>();
                if (!group.StudentIds.Contains(userId))
                    group.StudentIds.Add(userId);

                await _groupRepository.Update(group);
                break;

            case "Teacher":
                await _teacherRepository.AddAsync(new Teacher { Id = userId });
                break;

            case "Admin":
                await _adminRepository.AddAsync(new Admin { Id = userId });
                break;

            default:
                return BadRequest("Invalid role.");
        }

        // 4) Один общий SaveChanges
        await _userRepository.SaveChangesAsync(); // должен сохранить всё, если контекст общий

        return Ok("Registered successfully");
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginDto dto)
    {
        var user = await _userRepository.GetByEmail(dto.Email);
        if (user == null || !_hasher.Verify(dto.Password, user.PasswordHash))
            return Unauthorized("Invalid credentials");

        var token = _jwtProvider.GenerateToken(user);

        Response.Cookies.Append("cookies", token, new CookieOptions
        {
            HttpOnly = true,
            Secure = HttpContext.Request.IsHttps,   // ← ключевая правка
            SameSite = SameSiteMode.Lax,            // ← чтобы навигация после POST сохранила куку
            Path = "/",
            Expires = DateTimeOffset.UtcNow.AddDays(7)
        });

        return Ok("Logged in");
    }
    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdStr, out var userId))
            return Unauthorized();

        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null)
            return NotFound();

        return Ok(new
        {
            user.Id,
            user.Email,
            user.Role,
            user.MustChangePassword
        });
    }
    [Authorize]
    [HttpPost("logout")]
    public IActionResult Logout()
    {
        Response.Cookies.Delete("cookies");
        return Redirect("/Account/Login"); // або твоя сторінка авторизації
    }

    [Authorize]
    [HttpPut("change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDto dto)
    {
        if (dto is null || string.IsNullOrWhiteSpace(dto.NewPassword))
            return BadRequest("NewPassword is required.");

        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdStr, out var userId))
            return Unauthorized();

        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null) return NotFound();

        user.MustChangePassword = false;
        user.PasswordHash = _hasher.Generate(dto.NewPassword);
        await _userRepository.Update(user);
        await _userRepository.SaveChangesAsync();

        return Ok(new { message = "Password updated" });
    }
    [Authorize]
    [HttpPost("bulk-register")]
    public async Task<IActionResult> BulkRegister([FromForm] BulkRegisterDto dto)
    {
        if (dto.File == null || dto.File.Length == 0)
            return BadRequest("Invalid file");

        if (dto.Role == "Student" && dto.GroupId == null)
            return BadRequest("GroupId is required for student role");

        using var stream = new MemoryStream();
        await dto.File.CopyToAsync(stream);
        stream.Position = 0; // 🔧 важно для чтения
        using var workbook = new XLWorkbook(stream);
        var ws = workbook.Worksheets.FirstOrDefault();
        if (ws == null) return BadRequest("No worksheet found");

        var allUsers = await _userRepository.GetAllAsync();
        var byEmail = allUsers
            .Where(u => !string.IsNullOrWhiteSpace(u.Email))
            .GroupBy(u => u.Email!.Trim().ToLowerInvariant())
            .ToDictionary(g => g.Key, g => g.First());

        var byName = allUsers
            .Where(u => !string.IsNullOrWhiteSpace(u.FullName))
            .GroupBy(u => u.FullName!.Trim().ToLowerInvariant())
            .ToDictionary(g => g.Key, g => g.First());

        var allStudents = (await _studentRepository.GetAllAsync()).ToDictionary(s => s.Id);
        var allTeachers = (await _teacherRepository.GetAllAsync()).ToDictionary(t => t.Id);

        var groupsById = (await _groupRepository.GetAllAsync()).ToDictionary(g => g.Id);
        foreach (var group in groupsById.Values)
            group.StudentIds ??= new List<Guid>();

        Group? targetGroup = null;
        if (dto.Role == "Student" && groupsById.TryGetValue(dto.GroupId!.Value, out var foundGroup))
            targetGroup = foundGroup;
        else if (dto.Role == "Student")
            return BadRequest("Target group not found");

        // 🔧 будем знать какие группы нужно явно апдейтнуть/сохранить
        var touchedGroupIds = new HashSet<Guid>();

        foreach (var row in ws.RowsUsed().Skip(1))
        {
            var fullName = row.Cell(2).GetValue<string>().Trim();
            var email = row.Cell(3).GetValue<string>().Trim();

            if (string.IsNullOrWhiteSpace(fullName) && string.IsNullOrWhiteSpace(email))
                continue;

            User? user = null;
            if (!string.IsNullOrWhiteSpace(email) && byEmail.TryGetValue(email.ToLowerInvariant(), out var u1))
                user = u1;
            else if (!string.IsNullOrWhiteSpace(fullName) && byName.TryGetValue(fullName.ToLowerInvariant(), out var u2))
                user = u2;

            // --- Новый пользователь ---
            if (user == null)
            {
                user = new User
                {
                    Id = Guid.NewGuid(),
                    FullName = fullName,
                    Email = email,
                    Role = dto.Role,
                    PasswordHash = _hasher.Generate("test")
                };
                await _userRepository.AddAsync(user);

                // 🔧 поддерживаем индексы для последующих строк
                if (!string.IsNullOrWhiteSpace(user.Email))
                    byEmail[user.Email.Trim().ToLowerInvariant()] = user;
                if (!string.IsNullOrWhiteSpace(user.FullName))
                    byName[user.FullName.Trim().ToLowerInvariant()] = user;

                if (dto.Role == "Student")
                {
                    var student = new Student { Id = user.Id, GroupId = targetGroup!.Id };
                    await _studentRepository.AddAsync(student);
                    allStudents[user.Id] = student;

                    if (!targetGroup!.StudentIds!.Contains(user.Id))
                    {
                        targetGroup.StudentIds.Add(user.Id);
                        touchedGroupIds.Add(targetGroup.Id); // 🔧 пометили группу
                    }
                }
                else if (dto.Role == "Teacher")
                {
                    var teacher = new Teacher { Id = user.Id };
                    await _teacherRepository.AddAsync(teacher);
                    allTeachers[user.Id] = teacher;
                }
            }
            // --- Обновление существующего пользователя ---
            else
            {
                user.FullName = fullName;
                user.Email = email;

                if (dto.Role == "Student")
                {
                    allStudents.TryGetValue(user.Id, out var student);
                    if (student == null)
                    {
                        student = new Student { Id = user.Id, GroupId = targetGroup!.Id };
                        await _studentRepository.AddAsync(student);
                        allStudents[user.Id] = student;
                    }

                    if (user.Role == "Teacher" && allTeachers.TryGetValue(user.Id, out var oldTeacher))
                    {
                        await _teacherRepository.Delete(oldTeacher);
                        allTeachers.Remove(user.Id);
                    }

                    // если был в другой группе — убрать из старой
                    if (student.GroupId != targetGroup!.Id && groupsById.TryGetValue(student.GroupId, out var oldGroup))
                    {
                        if (oldGroup.StudentIds!.Remove(user.Id))
                            touchedGroupIds.Add(oldGroup.Id); // 🔧 старая группа изменилась
                    }

                    student.GroupId = targetGroup!.Id;

                    if (!targetGroup.StudentIds!.Contains(user.Id))
                    {
                        targetGroup.StudentIds.Add(user.Id);
                        touchedGroupIds.Add(targetGroup.Id); // 🔧 целевая группа изменилась
                    }

                    user.Role = "Student";
                }
                else if (dto.Role == "Teacher")
                {
                    if (allStudents.TryGetValue(user.Id, out var oldStudent))
                    {
                        if (groupsById.TryGetValue(oldStudent.GroupId, out var oldGroup))
                        {
                            if (oldGroup.StudentIds!.Remove(user.Id))
                                touchedGroupIds.Add(oldGroup.Id); // 🔧 убрали из старой группы
                        }

                        await _studentRepository.Delete(oldStudent);
                        allStudents.Remove(user.Id);
                    }

                    if (!allTeachers.TryGetValue(user.Id, out var teacher))
                    {
                        teacher = new Teacher { Id = user.Id };
                        await _teacherRepository.AddAsync(teacher);
                        allTeachers[user.Id] = teacher;
                    }

                    user.Role = "Teacher";
                }

                await _userRepository.Update(user);
            }
        }

        // ----- Сохранение -----
        await _userRepository.SaveChangesAsync();

        // 🔧 ВАЖНО: явно сохранить изменённые группы
        foreach (var gid in touchedGroupIds)
            await _groupRepository.Update(groupsById[gid]);   // если UpdateRange нет, делаем поштучно

        await _groupRepository.SaveChangesAsync();
        return Ok("Bulk registration complete (upsert).");
    }
    [Authorize]
    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto dto)
    {
        if (dto == null || (dto.UserId == null && string.IsNullOrWhiteSpace(dto.Email)))
            return BadRequest("Specify UserId or Email.");

        // Проверяем, что вызывающий — админ (по БД, не по JWT-claim на всякий случай)
        var meIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(meIdStr, out var meId))
            return Unauthorized();

        var me = await _userRepository.GetByIdAsync(meId);
        if (me == null || !string.Equals(me.Role, "Admin", StringComparison.OrdinalIgnoreCase))
            return Forbid();

        // Находим целевого пользователя по Id или Email
        User? target = null;
        if (dto.UserId.HasValue)
            target = await _userRepository.GetByIdAsync(dto.UserId.Value);
        else if (!string.IsNullOrWhiteSpace(dto.Email))
            target = await _userRepository.GetByEmail(dto.Email);

        if (target == null)
            return NotFound("User not found.");

        // Сбрасываем пароль на "test" и требуем смену при следующем входе
        target.PasswordHash = _hasher.Generate("test");
        target.MustChangePassword = true;

        await _userRepository.Update(target);
        await _userRepository.SaveChangesAsync();

        return Ok(new { message = "Password reset to default.", userId = target.Id });
    }
    public class ResetPasswordDto
    {
        public Guid? UserId { get; set; }
        public string? Email { get; set; }
    }

    // DTOs
    public class RegisterDto
    {
        public string FullName { get; set; }
        public string Email { get; set; }
        public string Password { get; set; }
        public string Role { get; set; }
        // Add this new property for students
        public Guid? GroupId { get; set; }
    }

    public class LoginDto
    {
        public string Email { get; set; }
        public string Password { get; set; }
    }
    public class BulkRegisterDto
    {
        public IFormFile File { get; set; }
        public string Role { get; set; }
        public Guid? GroupId { get; set; } // Только для студентов
    }


    public class ChangePasswordDto
    {
        public string NewPassword { get; set; }
    }
}
