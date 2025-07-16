using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyJournalApp.Interface;
using MyJournalApp.Data.Models;

namespace MyJournalApp.Controllers;

[ApiController]
[Route("api/[controller]")]
public class GroupController : ControllerBase
{
    private readonly IGroupRepository _groupRepository;
    private readonly IStudentRepository _studentRepository;
    private readonly ITeacherRepository _teacherRepository;

    public GroupController(
        IGroupRepository groupRepository,
        IStudentRepository studentRepository,
        ITeacherRepository teacherRepository)
    {
        _groupRepository = groupRepository;
        _studentRepository = studentRepository;
        _teacherRepository = teacherRepository;
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var groups = await _groupRepository.GetAllAsync();
        return Ok(groups);
    }

    [Authorize]
    [HttpGet("{id}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var group = await _groupRepository.GetByIdAsync(id);
        return group is null ? NotFound() : Ok(group);
    }
    [Authorize(Roles = "Admin")]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] Group group)
    {
        group.Id = Guid.NewGuid();
        await _groupRepository.AddAsync(group);
        await _groupRepository.SaveChangesAsync();

        // Присвоить GroupId всем студентам
        if (group.StudentIds != null)
        {
            foreach (var studentId in group.StudentIds)
            {
                var student = await _studentRepository.GetByIdAsync(studentId);
                if (student != null)
                {
                    student.GroupId = group.Id;
                    _studentRepository.Update(student);
                }
            }
            await _studentRepository.SaveChangesAsync();
        }

        // ✅ Добавить GroupId учителю
        var teacher = await _teacherRepository.GetByIdAsync(group.TeacherId);
        if (teacher != null)
        {
            teacher.GroupIds ??= new();
            if (!teacher.GroupIds.Contains(group.Id))
            {
                teacher.GroupIds.Add(group.Id);
                _teacherRepository.Update(teacher);
                await _teacherRepository.SaveChangesAsync();
            }
        }

        return Ok(group);
    }
    [Authorize(Roles = "Admin")]
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] Group group)
    {
        var existing = await _groupRepository.GetByIdAsync(id);
        if (existing == null) return NotFound();

        existing.Name = group.Name;
        existing.TeacherId = group.TeacherId;
        existing.StudentIds = group.StudentIds;

        _groupRepository.Update(existing);
        await _groupRepository.SaveChangesAsync();

        // Обновить GroupId у студентов
        if (group.StudentIds != null)
        {
            foreach (var studentId in group.StudentIds)
            {
                var student = await _studentRepository.GetByIdAsync(studentId);
                if (student != null)
                {
                    student.GroupId = group.Id;
                    _studentRepository.Update(student);
                }
            }

            await _studentRepository.SaveChangesAsync();
        }

        return Ok(existing);
    }

    [Authorize(Roles = "Admin")]
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var group = await _groupRepository.GetByIdAsync(id);
        if (group == null) return NotFound();

        _groupRepository.Delete(group);
        await _groupRepository.SaveChangesAsync();
        return Ok("Deleted");
    }
}
