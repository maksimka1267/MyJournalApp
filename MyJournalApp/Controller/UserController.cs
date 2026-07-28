using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyJournalApp.Data.Dtos.User;
using MyJournalApp.Service.Interface;

namespace MyJournalApp.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UserController : ControllerBase
{
    private readonly IUserService _userService;

    public UserController(IUserService userService)
    {
        _userService = userService;
    }

    [Authorize]
    [HttpPut("update-teacher-admin")]
    public async Task<IActionResult> UpdateTeacherAdmin(UpdateTeacherAdminDto dto)
    {
        var result = await _userService.UpdateTeacherAdminAsync(dto);

        return result
            ? Ok("Teacher admin status updated")
            : NotFound("Teacher not found");
    }

    [Authorize]
    [HttpGet("teachers-admin-status")]
    public async Task<IActionResult> GetTeachersAdminStatus()
    {
        return Ok(await _userService.GetTeachersAdminStatusAsync());
    }

    [Authorize]
    [HttpGet("teachers")]
    public async Task<IActionResult> GetAllTeachers()
    {
        return Ok(await _userService.GetAllTeachersAsync());
    }

    [Authorize]
    [HttpGet("users")]
    public async Task<IActionResult> GetAllUsers()
    {
        return Ok(await _userService.GetAllUsersAsync());
    }

    [Authorize]
    [HttpGet("students")]
    public async Task<IActionResult> GetAllStudents()
    {
        return Ok(await _userService.GetAllStudentsAsync());
    }

    [Authorize]
    [HttpPut("{studentId}/change-group/{newGroupId}")]
    public async Task<IActionResult> ChangeGroup(Guid studentId, Guid newGroupId)
    {
        var result = await _userService.ChangeStudentGroupAsync(studentId, newGroupId);

        return result
            ? NoContent()
            : NotFound();
    }

    [Authorize]
    [HttpDelete("delete/{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var result = await _userService.DeleteUserAsync(id);

        return result
            ? Ok()
            : NotFound();
    }

    [HttpDelete("delete-all")]
    public async Task<IActionResult> DeleteAllUsers()
    {
        await _userService.DeleteAllUsersAsync();

        return Ok(new
        {
            message = "Усі користувачі видалені."
        });
    }

    [Authorize]
    [HttpGet("by-group/{groupId}")]
    public async Task<IActionResult> GetStudentsByGroup(Guid groupId)
    {
        if (groupId == Guid.Empty)
            return BadRequest("Неверный ID группы.");

        return Ok(await _userService.GetStudentsByGroupAsync(groupId));
    }

    [Authorize]
    [HttpGet("teacher")]
    public async Task<IActionResult> GetTeachersByIds([FromQuery] List<Guid> ids)
    {
        if (ids == null || ids.Count == 0)
            return BadRequest("No teacher IDs provided.");

        return Ok(await _userService.GetTeachersByIdsAsync(ids));
    }

    [Authorize]
    [HttpGet("teacher-model/{id}")]
    public async Task<IActionResult> GetTeacherModelById(Guid id)
    {
        var teacher = await _userService.GetTeacherModelAsync(id);

        return teacher == null
            ? NotFound()
            : Ok(teacher);
    }

    [Authorize]
    [HttpGet("teacher/{id}")]
    public async Task<IActionResult> GetTeacherById(Guid id)
    {
        var teacher = await _userService.GetTeacherAsync(id);

        return teacher == null
            ? NotFound()
            : Ok(teacher);
    }

    [Authorize]
    [HttpGet("student/{id}")]
    public async Task<IActionResult> GetStudentById(Guid id)
    {
        var student = await _userService.GetStudentAsync(id);

        return student == null
            ? NotFound()
            : Ok(student);
    }

    [Authorize]
    [HttpPut("update-basic")]
    public async Task<IActionResult> UpdateBasic(UpdateUserBasicDto dto)
    {
        var user = await _userService.UpdateBasicAsync(dto);

        return user == null
            ? NotFound()
            : Ok(user);
    }
}