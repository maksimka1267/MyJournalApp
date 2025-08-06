using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyJournalApp.Data.Models;
using MyJournalApp.Interface;
using System.Security.Claims;

namespace MyJournalApp.Controllers;

[ApiController]
[Route("api/[controller]")]
public class GroupController : ControllerBase
{
    private readonly IGroupRepository _groupRepository;
    private readonly IStudentRepository _studentRepository;
    private readonly ITeacherRepository _teacherRepository;
    private readonly IUserRepository _userRepository;

    public GroupController(
        IGroupRepository groupRepository,
        IStudentRepository studentRepository,
        ITeacherRepository teacherRepository,
        IUserRepository userRepository)
    {
        _groupRepository = groupRepository;
        _studentRepository = studentRepository;
        _teacherRepository = teacherRepository;
        _userRepository = userRepository;
    }

    [Authorize]
    [HttpGet("all")]
    public async Task<IActionResult> GetAll()
    {
        var groups = await _groupRepository.GetAllAsync();
        return Ok(groups);
    }

    [Authorize]
    [HttpGet("my")]
    public async Task<IActionResult> GetMyGroups()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null) return Unauthorized();

        var teacherId = Guid.Parse(userId);
        var groups = await _groupRepository.GetByTeacherIdAsync(teacherId);
        return Ok(groups);
    }

    [Authorize(Roles = "Student")]
    [HttpGet("student")]
    public async Task<IActionResult> GetStudentGroup()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null) return Unauthorized();

        var student = await _studentRepository.GetByIdAsync(Guid.Parse(userId));
        if (student == null) return NotFound("Student not found");

        var group = await _groupRepository.GetByIdAsync(student.GroupId);
        if (group == null) return NotFound("Group not found");

        return Ok(new List<Group> { group }); // ✅ оборачиваем в список
    }
    [Authorize]
    [HttpGet("{id}/users")]
    public async Task<IActionResult> GetUsersByGroupId(Guid id)
    {
        var students = await _studentRepository.GetByGroupIdAsync(id);
        if (!students.Any())
            return Ok(new List<User>());

        var studentIds = students.Select(s => s.Id).ToList();
        var users = await _userRepository.GetAllAsync();
        var groupUsers = users.Where(u => studentIds.Contains(u.Id)).ToList();

        return Ok(groupUsers);
    }
    [Authorize(Roles = "Admin")]
    [HttpPut("move-student")]
    public async Task<IActionResult> MoveStudent([FromBody] MoveStudentDto dto)
    {
        if (dto.StudentId == Guid.Empty || dto.FromGroupId == Guid.Empty || dto.ToGroupId == Guid.Empty)
            return BadRequest("Invalid parameters");

        // Находим текущую группу
        var fromGroup = await _groupRepository.GetByIdAsync(dto.FromGroupId);
        if (fromGroup == null) return NotFound("Source group not found");

        // Находим целевую группу
        var toGroup = await _groupRepository.GetByIdAsync(dto.ToGroupId);
        if (toGroup == null) return NotFound("Target group not found");

        // Удаляем студента из старой группы
        fromGroup.StudentIds ??= new List<Guid>();
        if (fromGroup.StudentIds.Contains(dto.StudentId))
        {
            fromGroup.StudentIds.Remove(dto.StudentId);
            await _groupRepository.Update(fromGroup);
        }

        // Добавляем студента в новую группу
        toGroup.StudentIds ??= new List<Guid>();
        if (!toGroup.StudentIds.Contains(dto.StudentId))
        {
            toGroup.StudentIds.Add(dto.StudentId);
            await _groupRepository.Update(toGroup);
        }

        // Обновляем запись студента
        var student = await _studentRepository.GetByIdAsync(dto.StudentId);
        if (student == null) return NotFound("Student not found");

        student.GroupId = dto.ToGroupId;
        await _studentRepository.Update(student);

        // Сохраняем изменения
        await _groupRepository.SaveChangesAsync();
        await _studentRepository.SaveChangesAsync();

        return Ok("Student moved successfully");
    }

    public class MoveStudentDto
    {
        public Guid StudentId { get; set; }
        public Guid FromGroupId { get; set; }
        public Guid ToGroupId { get; set; }
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

        if (group.StudentIds != null)
        {
            foreach (var studentId in group.StudentIds)
            {
                var student = await _studentRepository.GetByIdAsync(studentId);
                if (student != null)
                {
                    student.GroupId = group.Id;
                    await _studentRepository.Update(student);
                }
            }
            await _studentRepository.SaveChangesAsync();
        }

        var teacher = await _teacherRepository.GetByIdAsync(group.TeacherId);
        if (teacher != null)
        {
            teacher.GroupIds ??= new();
            if (!teacher.GroupIds.Contains(group.Id))
            {
                teacher.GroupIds.Add(group.Id);
                await _teacherRepository.Update(teacher);
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

        // Обновление студентов
        existing.Name = group.Name;
        existing.StudentIds = group.StudentIds;

        if (group.StudentIds != null)
        {
            foreach (var studentId in group.StudentIds)
            {
                var student = await _studentRepository.GetByIdAsync(studentId);
                if (student != null)
                {
                    student.GroupId = existing.Id;
                    await _studentRepository.Update(student);
                }
            }
            await _studentRepository.SaveChangesAsync();
        }

        // Обновление учителя (удаление старого и добавление нового)
        if (existing.TeacherId != group.TeacherId)
        {
            var oldTeacher = await _teacherRepository.GetByIdAsync(existing.TeacherId);
            if (oldTeacher?.GroupIds != null && oldTeacher.GroupIds.Contains(existing.Id))
            {
                oldTeacher.GroupIds.Remove(existing.Id);
                await _teacherRepository.Update(oldTeacher);
            }

            var newTeacher = await _teacherRepository.GetByIdAsync(group.TeacherId);
            if (newTeacher != null)
            {
                newTeacher.GroupIds ??= new();
                if (!newTeacher.GroupIds.Contains(existing.Id))
                {
                    newTeacher.GroupIds.Add(existing.Id);
                }
                await _teacherRepository.Update(newTeacher);
            }

            await _teacherRepository.SaveChangesAsync();
        }

        existing.TeacherId = group.TeacherId;
        await _groupRepository.Update(existing);
        await _groupRepository.SaveChangesAsync();

        return Ok(existing);
    }

    [Authorize(Roles = "Admin")]
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var group = await _groupRepository.GetByIdAsync(id);
        if (group == null) return NotFound();

        // Очистить связи у студентов
        if (group.StudentIds != null)
        {
            foreach (var studentId in group.StudentIds)
            {
                var student = await _studentRepository.GetByIdAsync(studentId);
                if (student != null)
                {
                    student.GroupId = Guid.Empty;
                    await _studentRepository.Update(student);
                }
            }
            await _studentRepository.SaveChangesAsync();
        }

        // Очистить связь у учителя
        var teacher = await _teacherRepository.GetByIdAsync(group.TeacherId);
        if (teacher?.GroupIds != null && teacher.GroupIds.Contains(group.Id))
        {
            teacher.GroupIds.Remove(group.Id);
            await _teacherRepository.Update(teacher);
            await _teacherRepository.SaveChangesAsync();
        }

        await _groupRepository.Delete(group);
        await _groupRepository.SaveChangesAsync();
        return Ok("Deleted");
    }
    [Authorize(Roles = "Admin")]
    [HttpPost("bulk-import")]
    public async Task<IActionResult> BulkImportGroups([FromForm] BulkGroupImportDto dto)
    {
        if (dto.File == null || dto.File.Length == 0)
            return BadRequest("Invalid file");

        using var stream = new MemoryStream();
        await dto.File.CopyToAsync(stream);
        using var workbook = new XLWorkbook(stream);
        var worksheet = workbook.Worksheets.FirstOrDefault();
        if (worksheet == null) return BadRequest("No worksheet found");

        var createdGroups = new List<Group>();

        foreach (var row in worksheet.RowsUsed().Skip(1)) // Пропустить заголовок
        {
            var groupName = row.Cell(2).GetValue<string>().Trim();     // Група
            var teacherName = row.Cell(3).GetValue<string>().Trim();   // ПІБ керівника

            if (string.IsNullOrWhiteSpace(groupName))
                continue;

            // 🔍 Получаем ID преподавателя через репозиторий
            var teacherId = await _teacherRepository.GetTeacherIdByFullNameAsync(teacherName);

            var group = new Group
            {
                Id = Guid.NewGuid(),
                Name = groupName,
                TeacherId = teacherId ?? Guid.Empty,
                StudentIds = new List<Guid>()
            };

            await _groupRepository.AddAsync(group);
            createdGroups.Add(group);

            // 🔄 Привязка группы к преподавателю (если найден)
            if (teacherId.HasValue)
            {
                var teacherModel = await _teacherRepository.GetByIdAsync(teacherId.Value);
                if (teacherModel != null)
                {
                    teacherModel.GroupIds ??= new();
                    if (!teacherModel.GroupIds.Contains(group.Id))
                    {
                        teacherModel.GroupIds.Add(group.Id);
                        await _teacherRepository.Update(teacherModel);
                    }
                }
            }
        }

        await _groupRepository.SaveChangesAsync();
        await _teacherRepository.SaveChangesAsync();

        return Ok(new
        {
            Message = $"Імпорт завершено. Створено {createdGroups.Count} груп(и).",
            Groups = createdGroups.Select(g => new { g.Name, g.TeacherId })
        });
    }
    public class BulkGroupImportDto
    {
        public IFormFile File { get; set; }
    }

}
