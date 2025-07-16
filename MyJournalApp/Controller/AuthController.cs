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
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _hasher;
    private readonly IJwtProvider _jwtProvider;

    public AuthController(IPasswordHasher hasher,
                          IJwtProvider jwtProvider,
                          IStudentRepository studentRepository,
                          ITeacherRepository teacherRepository,
                          IAdminRepository adminRepository,
                          IUserRepository userRepository)
    {
        _adminRepository = adminRepository;
        _studentRepository = studentRepository;
        _teacherRepository = teacherRepository;
        _hasher = hasher;
        _jwtProvider = jwtProvider;
        _userRepository = userRepository;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromForm] RegisterDto dto)
    {
        var existingUser = await _userRepository.GetByEmail(dto.Email);
        if (existingUser != null)
            return BadRequest("Email already in use");

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
        // Создание дополнительных записей в зависимости от роли
        switch (dto.Role)
        {
            case "Student":
                var student = new Student
                {
                    Id = userId
                };
                await _studentRepository.AddAsync(student);
                break;

            case "Teacher":
                var teacher = new Teacher
                {
                    Id = userId
                };
                await _teacherRepository.AddAsync(teacher);
                break;

            case "Admin":
                var admin = new Admin
                {
                    Id = userId
                };
                await _adminRepository.AddAsync(admin);
                break;

            default:
                return BadRequest("Invalid role.");
        }
        await _userRepository.SaveChangesAsync();
        return Ok("Registered successfully");
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromForm]LoginDto dto)
    {
        var user = await _userRepository.GetByEmail(dto.Email);
        if (user == null || !_hasher.Verify(dto.Password, user.PasswordHash))
            return Unauthorized("Invalid credentials");

        var token = _jwtProvider.GenerateToken(user);

        Response.Cookies.Append("cookies", token, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Expires = DateTimeOffset.UtcNow.AddDays(7)
        });

        return Ok("Logged in");
    }

    [Authorize]
    [HttpGet("me")]
    public IActionResult Me()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var role = User.FindFirstValue(ClaimTypes.Role);
        var email = User.FindFirstValue(ClaimTypes.Email);

        return Ok(new { userId, email, role });
    }

    [Authorize]
    [HttpPost("logout")]
    public IActionResult Logout()
    {
        Response.Cookies.Delete("cookies");
        return Ok("Logged out");
    }
    public class RegisterDto
    {
        public string FullName { get; set; }
        public string Email { get; set; }
        public string Password { get; set; }
        public string Role { get; set; } // Student, Teacher, Admin, GroupLeader
    }
    public class LoginDto
    {
        public string Email { get; set; }
        public string Password { get; set; }
    }

}
