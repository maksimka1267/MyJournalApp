using ClosedXML.Excel;
using MyJournalApp.Data.Models;
using MyJournalApp.Dtos.Group;
using MyJournalApp.Interface;
using MyJournalApp.Result;

public class GroupService : IGroupService
{
    private readonly IGroupRepository _groupRepository;
    private readonly IStudentRepository _studentRepository;
    private readonly ITeacherRepository _teacherRepository;
    private readonly IUserRepository _userRepository;

    public GroupService(
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
    public async Task<IEnumerable<Group>> GetAllAsync()
    {
        return await _groupRepository.GetAllAsync();
    }
    public async Task<IEnumerable<Group>> GetTeacherGroupsAsync(Guid teacherId)
    {
        return await _groupRepository.GetByTeacherIdAsync(teacherId);
    }
    public async Task<Group?> GetStudentGroupAsync(Guid studentId)
    {
        var student = await _studentRepository.GetByIdAsync(studentId);

        if (student == null)
            return null;

        return await _groupRepository.GetByIdAsync(student.GroupId);
    }
    public async Task<List<User>> GetStudentsByGroupAsync(Guid groupId)
    {
        var group = await _groupRepository.GetByIdAsync(groupId);

        if (group == null || group.StudentIds == null || !group.StudentIds.Any())
            return new List<User>();

        var students = await _userRepository.GetByIdsAsync(group.StudentIds);

        return students
            .OrderBy(x => x.FullName)
            .ToList();
    }
    public async Task<IEnumerable<User>> GetUsersByGroupAsync(Guid groupId)
    {
        var students = await _studentRepository.GetByGroupIdAsync(groupId);

        var ids = students
            .Select(s => s.Id)
            .ToList();

        if (ids.Count == 0)
            return Enumerable.Empty<User>();

        return await _userRepository.GetByIdsAsync(ids);
    }
    public async Task<Group?> GetByIdAsync(Guid id)
    {
        return await _groupRepository.GetByIdAsync(id);
    }
    public async Task<IServiceResult> MoveStudentAsync(MoveStudentDto dto)
    {
        if (dto.StudentId == Guid.Empty ||
            dto.FromGroupId == Guid.Empty ||
            dto.ToGroupId == Guid.Empty)
        {
            return IServiceResult.Fail("Invalid parameters");
        }

        var fromGroup = await _groupRepository.GetByIdAsync(dto.FromGroupId);
        if (fromGroup == null)
            return IServiceResult.Fail("Source group not found");

        var toGroup = await _groupRepository.GetByIdAsync(dto.ToGroupId);
        if (toGroup == null)
            return IServiceResult.Fail("Target group not found");

        var student = await _studentRepository.GetByIdAsync(dto.StudentId);
        if (student == null)
            return IServiceResult.Fail("Student not found");

        fromGroup.StudentIds ??= new();
        toGroup.StudentIds ??= new();

        fromGroup.StudentIds.Remove(dto.StudentId);

        if (!toGroup.StudentIds.Contains(dto.StudentId))
            toGroup.StudentIds.Add(dto.StudentId);

        student.GroupId = dto.ToGroupId;

        await _groupRepository.Update(fromGroup);
        await _groupRepository.Update(toGroup);
        await _studentRepository.Update(student);


        return IServiceResult.Ok("Student moved successfully");
    }
    public async Task<ServiceResult<Group>> CreateAsync(Group group)
    {
        group.Id = Guid.NewGuid();

        await _groupRepository.AddAsync(group);

        await UpdateStudentsGroupAsync(group.StudentIds, group.Id);

        await AttachTeacherAsync(group.TeacherId, group.Id);

        return ServiceResult<Group>.Ok(group);
    }
    public async Task<ServiceResult<Group>> UpdateAsync(Guid id, Group group)
    {
        var existing = await _groupRepository.GetByIdAsync(id);

        if (existing == null)
            return ServiceResult<Group>.Fail("Group not found");

        existing.Name = group.Name;
        existing.StudentIds = group.StudentIds;

        await UpdateStudentsGroupAsync(group.StudentIds, existing.Id);

        if (existing.TeacherId != group.TeacherId)
        {
            await DetachTeacherAsync(existing.TeacherId, existing.Id);
            await AttachTeacherAsync(group.TeacherId, existing.Id);

            existing.TeacherId = group.TeacherId;
        }

        await _groupRepository.Update(existing);

        return ServiceResult<Group>.Ok(existing);
    }
    public async Task<IServiceResult> DeleteAsync(Guid id)
    {
        var group = await _groupRepository.GetByIdAsync(id);

        if (group == null)
            return IServiceResult.Fail("Group not found");

        await ClearStudentsAsync(group);

        await DetachTeacherAsync(group.TeacherId, group.Id);

        await _groupRepository.Delete(group);

        return IServiceResult.Ok("Deleted");
    }
    private async Task UpdateStudentsGroupAsync(
                        IEnumerable<Guid>? studentIds,
                        Guid groupId)
    {
        if (studentIds == null)
            return;

        foreach (var studentId in studentIds)
        {
            var student = await _studentRepository.GetByIdAsync(studentId);

            if (student == null)
                continue;

            student.GroupId = groupId;

            await _studentRepository.Update(student);
        }
    }
    private async Task AttachTeacherAsync(Guid teacherId, Guid groupId)
    {
        var teacher = await _teacherRepository.GetByIdAsync(teacherId);

        if (teacher == null)
            return;

        teacher.GroupIds ??= new();

        if (!teacher.GroupIds.Contains(groupId))
            teacher.GroupIds.Add(groupId);

        await _teacherRepository.Update(teacher);
    }
    private async Task DetachTeacherAsync(Guid teacherId, Guid groupId)
    {
        var teacher = await _teacherRepository.GetByIdAsync(teacherId);

        if (teacher?.GroupIds == null)
            return;

        teacher.GroupIds.Remove(groupId);

        await _teacherRepository.Update(teacher);
    }
    private async Task ClearStudentsAsync(Group group)
    {
        if (group.StudentIds == null)
            return;

        foreach (var studentId in group.StudentIds)
        {
            var student = await _studentRepository.GetByIdAsync(studentId);

            if (student == null)
                continue;

            student.GroupId = Guid.Empty;

            await _studentRepository.Update(student);
        }
    }
    public async Task<ServiceResult<BulkGroupImportResultDto>> BulkImportAsync(BulkGroupImportDto dto)
    {
        var validation = ValidateImport(dto);

        if (!validation.Success)
            return ServiceResult<BulkGroupImportResultDto>.Fail(validation.Message!);

        var worksheet = await OpenWorksheetAsync(dto.File);

        if (worksheet == null)
            return ServiceResult<BulkGroupImportResultDto>.Fail("No worksheet found");

        var context = await LoadImportContextAsync();

        await ProcessWorksheetAsync(worksheet, context);

        await SaveChangesAsync(context);

        return ServiceResult<BulkGroupImportResultDto>.Ok(
            BuildImportResult(context));
    }
    private IServiceResult ValidateImport(BulkGroupImportDto dto)
    {
        if (dto.File == null || dto.File.Length == 0)
            return IServiceResult.Fail("Invalid file");

        return IServiceResult.Ok();
    }
    private async Task<IXLWorksheet?> OpenWorksheetAsync(IFormFile file)
    {
        var stream = new MemoryStream();

        await file.CopyToAsync(stream);

        stream.Position = 0;

        var workbook = new XLWorkbook(stream);

        return workbook.Worksheets
            .FirstOrDefault(ws => !ws.IsEmpty());
    }
    private async Task<BulkImportContext> LoadImportContextAsync()
    {
        var context = new BulkImportContext();

        var teacherUsers = await _teacherRepository.GetAllTeachersAsync();

        context.TeachersByShortName = teacherUsers
            .Where(u => !string.IsNullOrWhiteSpace(
                _teacherRepository.ToShortName(u.FullName)))
            .GroupBy(
                u => _teacherRepository.ToShortName(u.FullName).Trim(),
                StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => g.First(),
                StringComparer.OrdinalIgnoreCase);

        context.TeachersById = (await _teacherRepository.GetAllAsync())
            .ToDictionary(t => t.Id);

        context.GroupsByName = (await _groupRepository.GetAllAsync())
            .ToDictionary(
                g => g.Name.Trim(),
                g => g,
                StringComparer.OrdinalIgnoreCase);

        return context;
    }
    private async Task ProcessWorksheetAsync(
                        IXLWorksheet worksheet,
                        BulkImportContext context)
    {
        var lastRow = worksheet.Column(2)
            .LastCellUsed()?
            .Address.RowNumber;

        if (!lastRow.HasValue)
            return;

        for (int i = 2; i <= lastRow.Value; i++)
        {
            var row = worksheet.Row(i);

            await ProcessRowAsync(row, context);
        }
    }
    private async Task ProcessRowAsync(
    IXLRow row,
    BulkImportContext context)
    {
        var groupName = row.Cell(2)
            .GetValue<string>()
            .Trim();

        var teacherRaw = row.Cell(3)
            .GetValue<string>()
            .Trim();

        if (string.IsNullOrWhiteSpace(groupName))
            return;

        context.TeachersByShortName.TryGetValue(
            teacherRaw,
            out var teacherUser);

        if (context.GroupsByName.TryGetValue(groupName, out var existingGroup))
        {
            await UpdateGroupAsync(
                existingGroup,
                teacherUser,
                context);

            return;
        }

        await CreateGroupAsync(
            groupName,
            teacherRaw,
            teacherUser,
            context);
    }
    private async Task CreateGroupAsync(
                        string groupName,
                        string teacherRaw,
                        User? teacherUser,
                        BulkImportContext context)
    {
        var teacherId = teacherUser?.Id ?? Guid.Empty;

        if (teacherUser == null &&
            !string.IsNullOrWhiteSpace(teacherRaw))
        {
            context.MissingTeachers.Add((groupName, teacherRaw));
        }

        var group = new Group
        {
            Id = Guid.NewGuid(),
            Name = groupName,
            TeacherId = teacherId,
            StudentIds = new List<Guid>()
        };

        await _groupRepository.AddAsync(group);

        context.CreatedGroups.Add(group);

        context.GroupsByName[group.Name] = group;

        if (teacherId != Guid.Empty &&
            context.TeachersById.TryGetValue(teacherId, out var teacher))
        {
            teacher.GroupIds ??= new();

            if (!teacher.GroupIds.Contains(group.Id))
            {
                teacher.GroupIds.Add(group.Id);

                if (!context.UpdatedTeachers.Contains(teacher))
                    context.UpdatedTeachers.Add(teacher);
            }
        }
    }
    private async Task UpdateGroupAsync(
                        Group group,
                        User? teacherUser,
                        BulkImportContext context)
    {
        context.SkippedGroups.Add(group.Name);

        if (teacherUser == null)
            return;

        if (group.TeacherId == teacherUser.Id)
            return;

        group.TeacherId = teacherUser.Id;

        if (!context.UpdatedGroups.Contains(group))
            context.UpdatedGroups.Add(group);

        if (context.TeachersById.TryGetValue(teacherUser.Id, out var teacher))
        {
            teacher.GroupIds ??= new();

            if (!teacher.GroupIds.Contains(group.Id))
            {
                teacher.GroupIds.Add(group.Id);

                if (!context.UpdatedTeachers.Contains(teacher))
                    context.UpdatedTeachers.Add(teacher);
            }
        }
    }
    private async Task SaveChangesAsync(BulkImportContext context)
    {
        if (context.UpdatedGroups.Count > 0)
        {
            await _groupRepository.UpdateRange(context.UpdatedGroups);
        }

        if (context.UpdatedTeachers.Count > 0)
        {
            await _teacherRepository.UpdateRange(context.UpdatedTeachers);
        }

        await _groupRepository.SaveChangesAsync();
    }
    private BulkGroupImportResultDto BuildImportResult(BulkImportContext context)
    {
        return new BulkGroupImportResultDto
        {
            Message = $"Імпорт завершено. Нових груп створено: {context.CreatedGroups.Count}. Уже існувало: {context.SkippedGroups.Count}. Оновлено (TeacherId): {context.UpdatedGroups.Count}.",

            Created = context.CreatedGroups
                .Select(g => new GroupInfoDto
                {
                    Name = g.Name,
                    TeacherId = g.TeacherId
                })
                .ToList(),

            SkippedExisting = context.SkippedGroups,

            UpdatedTeacher = context.UpdatedGroups
                .Select(g => new GroupInfoDto
                {
                    Name = g.Name,
                    TeacherId = g.TeacherId
                })
                .ToList(),

            MissingTeachers = context.MissingTeachers
                .Select(x => new MissingTeacherDto
                {
                    Group = x.Group,
                    Teacher = x.TeacherRaw
                })
                .ToList()
        };
    }
}