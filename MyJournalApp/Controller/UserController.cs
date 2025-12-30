using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyJournalApp.Data.Models;
using MyJournalApp.Interface;

namespace MyJournalApp.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UserController : ControllerBase
{
    private readonly IStudentRepository _studentRepository;
    private readonly IUserRepository _userRepository;
    private readonly ITeacherRepository _teacherRepository;

    public UserController(
        IStudentRepository studentRepository,
        ITeacherRepository teacherRepository,
        IUserRepository userRepository)
    {
        _studentRepository = studentRepository;
        _teacherRepository = teacherRepository;
        _userRepository = userRepository;
    }
    [Authorize]
    [HttpPut("update-teacher-admin")]
    public async Task<IActionResult> UpdateTeacherAdmin([FromBody] UpdateTeacherAdminDto dto)
    {
        var teacher = await _teacherRepository.GetByIdAsync(dto.TeacherId);
        if (teacher == null)
            return NotFound("Teacher not found");

        teacher.IsAdmin = dto.IsAdmin;
        await _teacherRepository.Update(teacher);
        await _teacherRepository.SaveChangesAsync();

        return Ok("Teacher admin status updated");
    }

    public class UpdateTeacherAdminDto
    {
        public Guid TeacherId { get; set; }
        public bool IsAdmin { get; set; }
    }
    [Authorize]
    [HttpGet("teachers-admin-status")]
    public async Task<IActionResult> GetTeachersAdminStatus()
    {
        var teachers = await _teacherRepository.GetAllTeachersWithAdminAsync();
        return Ok(teachers);
    }
    // Отримати всіх вчителів
    [Authorize]
    [HttpGet("teachers")]
    public async Task<IActionResult> GetAllTeachers()
    {
        var teachers = await _teacherRepository.GetAllTeachersAsync();
        return Ok(teachers);
    }
    // Отримати всіх користувачів
    [Authorize]
    [HttpGet("users")]
    public async Task<IActionResult> GetAllAsync()
    {
        var users = await _userRepository.GetAllAsync();
        return Ok(users);
    }
    // Отримати всіх студентів
    [Authorize]
    [HttpGet("students")]
    public async Task<IActionResult> GetAllStudents()
    {
        var students = await _studentRepository.GetAllStudentsAsync();
        return Ok(students);
    }
    [Authorize]
    [HttpPut("{studentId}/change-group/{newGroupId}")]
    public async Task<IActionResult> ChangeGroup(Guid studentId, Guid newGroupId)
    {
        var student = await _studentRepository.GetByIdAsync(studentId);
        if (student == null) return NotFound();

        student.GroupId = newGroupId;
        await _studentRepository.Update(student);

        return NoContent();
    }
    [Authorize]
    [HttpDelete("delete/{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var user = await _userRepository.GetByIdAsync(id);
        if (user == null) return NotFound();

        await _userRepository.Delete(user);
        await _userRepository.SaveChangesAsync();
        return Ok();
    }
    [HttpDelete("delete-all")]
    public async Task<IActionResult> DeleteAllUsers()
    {
        try
        {
            await _userRepository.DeleteAllAsync();
            return Ok(new { message = "Усі користувачі видалені." });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Ошибка при видалені користувачей", error = ex.Message });
        }
    }
    [Authorize]
    [HttpGet("by-group/{groupId}")]
    public async Task<IActionResult> GetStudentsByGroup(Guid groupId)
    {
        if (groupId == Guid.Empty)
        {
            return BadRequest("Неверный ID группы.");
        }

        // Вызываем новый эффективный метод из репозитория
        var users = await _studentRepository.GetUsersByGroupIdAsync(groupId);

        // Проверяем, найдены ли студенты. Возвращаем пустой список, если нет.
        if (users == null)
        {
            return Ok(new List<User>());
        }

        return Ok(users);
    }
    // Отримати одного вчителя
    [Authorize]
    [HttpGet("teacher")]
    public async Task<IActionResult> GetTeachersByIds([FromQuery] List<Guid> ids)
    {
        if (ids == null || ids.Count == 0)
            return BadRequest("No teacher IDs provided.");

        var teachers = await _userRepository.GetByIdsAsync(ids.Distinct());
        return Ok(teachers);
    }

    [Authorize]
    [HttpGet("teacher-model/{id}")]
    public async Task<IActionResult> GetTeacherModelById(Guid id)
    {
        var teacher = await _teacherRepository.GetByIdAsync(id);
        return teacher is null ? NotFound() : Ok(teacher);
    }
    [Authorize]
    [HttpGet("teacher/{id}")]
    public async Task<IActionResult> GetTeacherById(Guid id)
    {
        var teacher = await _userRepository.GetByIdAsync(id);
        return teacher is null ? NotFound() : Ok(teacher);
    }
    // Отримати одного студента
    [Authorize]
    [HttpGet("student/{id}")]
    public async Task<IActionResult> GetStudentById(Guid id)
    {
        var student = await _studentRepository.GetByIdAsync(id);
        return student is null ? NotFound() : Ok(student);
    }
    // UserController.cs (добавить в класс)
    [Authorize]
    [HttpPut("update-basic")]
    public async Task<IActionResult> UpdateBasic([FromBody] UpdateUserBasicDto dto)
    {
        if (dto.UserId == Guid.Empty)
            return BadRequest("UserId is required.");

        var user = await _userRepository.GetByIdAsync(dto.UserId);
        if (user is null) return NotFound();

        bool changed = false;

        if (!string.IsNullOrWhiteSpace(dto.FullName) && dto.FullName != user.FullName)
        {
            user.FullName = dto.FullName.Trim();
            changed = true;
        }

        if (!string.IsNullOrWhiteSpace(dto.Email) && dto.Email != user.Email)
        {
            user.Email = dto.Email.Trim();
            changed = true;
        }

        if (changed)
        {
            await _userRepository.Update(user);
            await _userRepository.SaveChangesAsync();
        }

        return Ok(user);
    }

    public class UpdateUserBasicDto
    {
        public Guid UserId { get; set; }
        public string? FullName { get; set; }
        public string? Email { get; set; }
    }
}
