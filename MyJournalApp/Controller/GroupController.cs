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
    [HttpGet("curated-by/me")]
    public async Task<IActionResult> GetCuratedGroups()
    {
        // Получаем ID текущего пользователя (учителя) из токена
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));

        // Вам нужно будет создать метод GetByCuratorIdAsync в вашем репозитории
        var groups = await _groupRepository.GetByTeacherIdAsync(userId);

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

    [Authorize]
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
    [Authorize]
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

    [Authorize]
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

    [Authorize]
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

    [Authorize]
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
    [Authorize]
    [HttpPost("bulk-import")]
    public async Task<IActionResult> BulkImportGroups([FromForm] BulkGroupImportDto dto)
    {
        if (dto.File == null || dto.File.Length == 0)
            return BadRequest("Invalid file");

        // 1) Считываем Excel
        using var stream = new MemoryStream();
        await dto.File.CopyToAsync(stream);
        stream.Position = 0;

        using var workbook = new XLWorkbook(stream);
        var worksheet = workbook.Worksheets.FirstOrDefault(ws => !ws.IsEmpty());
        if (worksheet == null)
            return BadRequest("No worksheet found");

        // 2) Подготовим справочники ОДИН РАЗ для максимальной производительности

        // Получаем всех пользователей-преподавателей
        var teacherUsers = await _teacherRepository.GetAllTeachersAsync();

        var usersByShortName = teacherUsers
            .Where(u => !string.IsNullOrWhiteSpace(_teacherRepository.ToShortName(u.FullName)))
            .GroupBy(u => _teacherRepository.ToShortName(u.FullName).Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,      // Ключ - это короткое ФИО
                g => g.First(),  // Значение - первый попавшийся пользователь с таким ФИО
                StringComparer.OrdinalIgnoreCase);

        var allTeachers = await _teacherRepository.GetAllAsync();
        var teachersById = allTeachers.ToDictionary(t => t.Id);

        var allGroups = await _groupRepository.GetAllAsync();
        var groupsByName = allGroups.ToDictionary(g => g.Name.Trim(), g => g, StringComparer.OrdinalIgnoreCase);

        var created = new List<Group>();
        var updated = new List<Group>();
        var skipped = new List<string>();
        var updatedteacher = new List<Teacher>();
        var notFoundTeachers = new List<(string Group, string TeacherRaw)>();

        // 3) Парсим строки надежным способом
        var lastRowNumber = worksheet.Column(2).LastCellUsed()?.Address.RowNumber;
        if (!lastRowNumber.HasValue)
            return BadRequest("No data found in group column (B)");

        // Начинаем с нужной строки (пропуская заголовок, обычно i = 2)
        for (int i = 2; i <= lastRowNumber.Value; i++)
        {
            var row = worksheet.Row(i);
            var groupName = row.Cell(2).GetValue<string>().Trim();
            var teacherNameRaw = row.Cell(3).GetValue<string>().Trim();

            if (string.IsNullOrWhiteSpace(groupName))
                continue;

            // Ищем преподавателя в нашем словаре. Это моментально и без запроса к БД.
            User teacherUser = null;
            if (!string.IsNullOrWhiteSpace(teacherNameRaw))
            {
                usersByShortName.TryGetValue(teacherNameRaw, out teacherUser);
            }

            // --- Остальная логика без изменений ---

            // Если группа уже существует — обновляем преподавателя, если нужно
            if (groupsByName.TryGetValue(groupName, out var existingGroup))
            {
                skipped.Add(groupName);

                if (teacherUser != null && existingGroup.TeacherId != teacherUser.Id)
                {
                    existingGroup.TeacherId = teacherUser.Id;
                    updated.Add(existingGroup);
                    if (teachersById.TryGetValue(teacherUser.Id, out var teacher))
                    {
                        teacher.GroupIds ??= new List<Guid>();
                        if (!teacher.GroupIds.Contains(existingGroup.Id))
                        {
                            updatedteacher.Add(teacher);
                            teacher.GroupIds.Add(existingGroup.Id);
                        }
                    }
                }
                continue;
            }

            // Создаем новую группу
            Guid teacherId = Guid.Empty;
            if (teacherUser != null)
            {
                teacherId = teacherUser.Id;
            }
            else if (!string.IsNullOrWhiteSpace(teacherNameRaw))
            {
                notFoundTeachers.Add((groupName, teacherNameRaw));
            }

            var group = new Group
            {
                Id = Guid.NewGuid(),
                Name = groupName,
                TeacherId = teacherId,
                StudentIds = new List<Guid>()
            };

            await _groupRepository.AddAsync(group);
            created.Add(group);

            if (teacherId != Guid.Empty && teachersById.TryGetValue(teacherId, out var teacherModel))
            {
                teacherModel.GroupIds ??= new List<Guid>();
                teacherModel.GroupIds.Add(group.Id);
            }
        }
        if(updated.Count > 0)
        {
            await _groupRepository.UpdateRange(updated);
        }
        if (updatedteacher.Count > 0)
        {
            await _teacherRepository.UpdateRange(updatedteacher);
        }
        // 4) Сохраняем все изменения одной транзакцией
        await _groupRepository.SaveChangesAsync();

        // 5) Возвращаем результат
        return Ok(new
        {
            Message = $"Імпорт завершено. Нових груп створено: {created.Count}. Уже існувало: {skipped.Count}. Оновлено (TeacherId): {updated.Count}.",
            Created = created.Select(g => new { g.Name, g.TeacherId }),
            SkippedExisting = skipped,
            UpdatedTeacher = updated.Select(g => new { g.Name, g.TeacherId }),
            MissingTeachers = notFoundTeachers.Select(x => new { Group = x.Group, Teacher = x.TeacherRaw })
        });
    }
    public class BulkGroupImportDto
        {
            public IFormFile File { get; set; }
        }
}
