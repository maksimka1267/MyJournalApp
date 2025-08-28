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
                await _studentRepository.AddAsync(new Student
                {
                    Id = userId,
                    GroupId = dto.GroupId!.Value
                });
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
        using var workbook = new XLWorkbook(stream);
        var worksheet = workbook.Worksheets.FirstOrDefault();
        if (worksheet == null) return BadRequest("No worksheet found");

        // --- ИЗМЕНЕНИЕ 1: Получаем группу ОДИН РАЗ перед циклом ---
        Group group = null;
        if (dto.Role == "Student")
        {
            group = await _groupRepository.GetByIdAsync(dto.GroupId.Value);
            if (group == null)
            {
                return BadRequest("Group not found");
            }
            // Инициализируем список, если он пуст
            group.StudentIds ??= new List<Guid>();
        }

        foreach (var row in worksheet.RowsUsed().Skip(1))
        {
            var fullName = row.Cell(2).GetValue<string>().Trim();
            var email = row.Cell(3).GetValue<string>().Trim();

            if (string.IsNullOrEmpty(fullName) || string.IsNullOrEmpty(email))
                continue;

            if (await _userRepository.GetByEmail(email) != null)
                continue;

            var id = Guid.NewGuid();
            var user = new User
            {
                Id = id,
                FullName = fullName,
                Email = email,
                Role = dto.Role,
                PasswordHash = _hasher.Generate("test")
            };

            await _userRepository.AddAsync(user);

            switch (dto.Role)
            {
                case "Student":
                    await _studentRepository.AddAsync(new Student
                    {
                        Id = id,
                        GroupId = dto.GroupId.Value
                    });
                    if (group != null)
                    {
                        group.StudentIds.Add(id);
                    }
                    break;

                case "Teacher":
                    await _teacherRepository.AddAsync(new Teacher { Id = id });
                    break;

                default:
                    continue;
            }
        }

        // --- ИЗМЕНЕНИЕ 3: Обновляем группу ОДИН РАЗ после цикла ---
        if (dto.Role == "Student" && group != null)
        {
            await _groupRepository.Update(group);
        }

        await _userRepository.SaveChangesAsync();
        await _studentRepository.SaveChangesAsync();
        await _teacherRepository.SaveChangesAsync();
        return Ok("Bulk registration complete");
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
