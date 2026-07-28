using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyJournalApp.Data.Dtos.Auth;
using System.Security.Claims;

namespace MyJournalApp.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromForm] RegisterDto dto)
    {
        var result = await _authService.RegisterAsync(dto);

        if (!result.Success)
            return BadRequest(result.Message);

        return Ok(result.Message);
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginDto dto)
    {
        var result = await _authService.LoginAsync(dto);

        if (!result.Success)
            return Unauthorized(result.Message);

        Response.Cookies.Append("cookies", result.Data!, new CookieOptions
        {
            HttpOnly = true,
            Secure = HttpContext.Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            Path = "/",
            Expires = DateTimeOffset.UtcNow.AddDays(7)
        });

        return Ok(result.Message);
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(userIdStr, out var userId))
            return Unauthorized();

        var user = await _authService.GetCurrentUserAsync(userId);

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
        return Redirect("/Account/Login");
    }

    [Authorize]
    [HttpPut("change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDto dto)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(userIdStr, out var userId))
            return Unauthorized();

        var result = await _authService.ChangePasswordAsync(userId, dto);

        if (!result.Success)
            return BadRequest(result.Message);

        return Ok(result.Message);
    }

    [Authorize]
    [HttpPost("bulk-register")]
    public async Task<IActionResult> BulkRegister([FromForm] BulkRegisterDto dto)
    {
        var result = await _authService.BulkRegisterAsync(dto);

        if (!result.Success)
            return BadRequest(result.Message);

        return Ok(result.Message);
    }

    [Authorize]
    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto dto)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(userIdStr, out var userId))
            return Unauthorized();

        var result = await _authService.ResetPasswordAsync(userId, dto);

        if (!result.Success)
        {
            return result.StatusCode switch
            {
                403 => Forbid(),
                404 => NotFound(result.Message),
                _ => BadRequest(result.Message)
            };
        }

        return Ok(result.Message);
    }
}