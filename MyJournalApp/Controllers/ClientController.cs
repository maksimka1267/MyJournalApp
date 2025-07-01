using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyJournalApp.Auth;
using MyJournalApp.Data.Models;
using MyJournalApp.Interface;
using MyJournalApp.Jwt;
using System.Security.Claims;

namespace MyJournalApp.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ClientController : ControllerBase
{
    private readonly IJwtProvider _jwtProvider;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IStudentRepository _studentRepository;
    private readonly ITeacherRepository _teacherRepository;
    private readonly IAdminRepository _adminRepository;

    public ClientController(
        IJwtProvider jwtProvider,
        IPasswordHasher passwordHasher,
        IStudentRepository studentRepository,
        ITeacherRepository teacherRepository,
        IAdminRepository adminRepository)
    {
        _jwtProvider = jwtProvider;
        _passwordHasher = passwordHasher;
        _studentRepository = studentRepository;
        _teacherRepository = teacherRepository;
        _adminRepository = adminRepository;
    }

    [HttpPost("register/student")]
    public async Task<IActionResult> RegisterStudent([FromForm] RegisterDto dto)
    {
        if (await _studentRepository.EmailExistsAsync(dto.Email))
            return BadRequest("Email already in use");

        var student = new Student
        {
            FullName = dto.FullName,
            Email = dto.Email,
            Password = _passwordHasher.Generate(dto.Password),
            Role = "Student"
        };

        await _studentRepository.AddAsync(student);
        await _studentRepository.SaveAsync();

        return Ok("Student registered successfully");
    }

    [HttpPost("register/teacher")]
    public async Task<IActionResult> RegisterTeacher([FromForm] RegisterDto dto)
    {
        if (await _teacherRepository.EmailExistsAsync(dto.Email))
            return BadRequest("Email already in use");

        var teacher = new Teacher
        {
            FullName = dto.FullName,
            Email = dto.Email,
            Password = _passwordHasher.Generate(dto.Password),
            Role = "Teacher"
        };

        await _teacherRepository.AddAsync(teacher);
        await _teacherRepository.SaveAsync();

        return Ok("Teacher registered successfully");
    }

    [HttpPost("register/admin")]
    public async Task<IActionResult> RegisterAdmin([FromForm]RegisterDto dto)
    {
        if (await _adminRepository.EmailExistsAsync(dto.Email))
            return BadRequest("Email already in use");

        var admin = new Admin
        {
            FullName = dto.FullName,
            Email = dto.Email,
            Password = _passwordHasher.Generate(dto.Password),
            Role = "Admin"
        };

        await _adminRepository.AddAsync(admin);
        await _adminRepository.SaveAsync();

        return Ok("Admin registered successfully");
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromForm] ClientLoginDto dto)
    {
        Client? client = await _studentRepository.GetByEmailAsync(dto.Email) as Client
                      ?? await _teacherRepository.GetByEmailAsync(dto.Email) as Client
                      ?? await _adminRepository.GetByEmailAsync(dto.Email) as Client;

        if (client == null || !_passwordHasher.Verify(dto.Password, client.Password))
            return Unauthorized("Invalid credentials");

        var token = _jwtProvider.GenerateToken(client);
        return Ok(new { token });
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Me()
    {
        var clientId = User.FindFirst("clientId")?.Value;

        if (!Guid.TryParse(clientId, out var id))
            return Unauthorized();

        Client? client = await _studentRepository.GetByIdAsync(id) as Client
                      ?? await _teacherRepository.GetByIdAsync(id) as Client
                      ?? await _adminRepository.GetByIdAsync(id) as Client;

        if (client == null)
            return NotFound();

        return Ok(new { client.Id, client.FullName, client.Email, client.Role });
    }

    public class RegisterDto
    {
        public string FullName { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string Password { get; set; } = null!;
    }

    public class ClientLoginDto
    {
        public string Email { get; set; } = null!;
        public string Password { get; set; } = null!;
    }
}
