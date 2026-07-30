using MyJournalApp.Data.Dtos.User;
using MyJournalApp.Data.Models;
using MyJournalApp.Interface;
using MyJournalApp.Service.Interface;

namespace MyJournalApp.Service;

public class UserService : IUserService
{
    private readonly IUserRepository _userRepository;
    private readonly IStudentRepository _studentRepository;
    private readonly ITeacherRepository _teacherRepository;

    public UserService(
        IUserRepository userRepository,
        IStudentRepository studentRepository,
        ITeacherRepository teacherRepository)
    {
        _userRepository = userRepository;
        _studentRepository = studentRepository;
        _teacherRepository = teacherRepository;
    }

    public async Task<bool> UpdateTeacherAdminAsync(UpdateTeacherAdminDto dto)
    {
        var teacher = await _teacherRepository.GetByIdAsync(dto.TeacherId);

        if (teacher == null)
            return false;

        teacher.IsAdmin = dto.IsAdmin;

        await _teacherRepository.Update(teacher);
        await _teacherRepository.SaveChangesAsync();

        return true;
    }
    public async Task<bool> UpdateTeacherDirectorAsync(UpdateTeacherDirectorDto dto)
    {
        var teacher = await _teacherRepository.GetByIdAsync(dto.TeacherId);

        if (teacher == null)
            return false;

        teacher.IsDirector = dto.IsDirector;

        await _teacherRepository.Update(teacher);
        await _teacherRepository.SaveChangesAsync();

        return true;
    }
    public async Task<IEnumerable<Teacher>> GetTeachersAdminStatusAsync()
    {
        return await _teacherRepository.GetAllTeachersWithAdminAsync();
    }

    public async Task<IEnumerable<User>> GetAllUsersAsync()
    {
        return await _userRepository.GetAllAsync();
    }

    public async Task<IEnumerable<User>> GetAllTeachersAsync()
    {
        return await _teacherRepository.GetAllTeachersAsync();
    }

    public async Task<IEnumerable<User>> GetAllStudentsAsync()
    {
        return await _studentRepository.GetAllStudentsAsync();
    }

    public async Task<IEnumerable<User>> GetStudentsByGroupAsync(Guid groupId)
    {
        if (groupId == Guid.Empty)
            return Enumerable.Empty<User>();

        return await _studentRepository.GetUsersByGroupIdAsync(groupId)
               ?? Enumerable.Empty<User>();
    }

    public async Task<User?> GetTeacherAsync(Guid id)
    {
        return await _userRepository.GetByIdAsync(id);
    }

    public async Task<Teacher?> GetTeacherModelAsync(Guid id)
    {
        return await _teacherRepository.GetByIdAsync(id);
    }

    public async Task<Student?> GetStudentAsync(Guid id)
    {
        return await _studentRepository.GetByIdAsync(id);
    }

    public async Task<bool> ChangeStudentGroupAsync(Guid studentId, Guid newGroupId)
    {
        var student = await _studentRepository.GetByIdAsync(studentId);

        if (student == null)
            return false;

        student.GroupId = newGroupId;

        await _studentRepository.Update(student);
        await _studentRepository.SaveChangesAsync();

        return true;
    }

    public async Task<bool> DeleteUserAsync(Guid id)
    {
        var user = await _userRepository.GetByIdAsync(id);

        if (user == null)
            return false;

        await _userRepository.Delete(user);
        await _userRepository.SaveChangesAsync();

        return true;
    }

    public async Task DeleteAllUsersAsync()
    {
        await _userRepository.DeleteAllAsync();
    }

    public async Task<User?> UpdateBasicAsync(UpdateUserBasicDto dto)
    {
        if (dto.UserId == Guid.Empty)
            return null;

        var user = await _userRepository.GetByIdAsync(dto.UserId);

        if (user == null)
            return null;

        bool changed = false;

        if (!string.IsNullOrWhiteSpace(dto.FullName) &&
            dto.FullName != user.FullName)
        {
            user.FullName = dto.FullName.Trim();
            changed = true;
        }

        if (!string.IsNullOrWhiteSpace(dto.Email) &&
            dto.Email != user.Email)
        {
            user.Email = dto.Email.Trim();
            changed = true;
        }

        if (changed)
        {
            await _userRepository.Update(user);
            await _userRepository.SaveChangesAsync();
        }

        return user;
    }

    public async Task<IEnumerable<User>> GetTeachersByIdsAsync(IEnumerable<Guid> ids)
    {
        var distinctIds = ids
            .Where(x => x != Guid.Empty)
            .Distinct()
            .ToList();

        if (!distinctIds.Any())
            return Enumerable.Empty<User>();

        return await _userRepository.GetByIdsAsync(distinctIds);
    }
}