using MyJournalApp.Data.Dtos.User;
using MyJournalApp.Data.Models;

namespace MyJournalApp.Service.Interface;

public interface IUserService
{
    Task<bool> UpdateTeacherAdminAsync(UpdateTeacherAdminDto dto);
    Task<bool> UpdateTeacherDirectorAsync(UpdateTeacherDirectorDto dto);

    Task<IEnumerable<Teacher>> GetTeachersAdminStatusAsync();

    Task<IEnumerable<User>> GetAllUsersAsync();

    Task<IEnumerable<User>> GetAllTeachersAsync();

    Task<IEnumerable<User>> GetAllStudentsAsync();

    Task<IEnumerable<User>> GetStudentsByGroupAsync(Guid groupId);

    Task<User?> GetTeacherAsync(Guid id);

    Task<Teacher?> GetTeacherModelAsync(Guid id);

    Task<Student?> GetStudentAsync(Guid id);

    Task<bool> ChangeStudentGroupAsync(Guid studentId, Guid newGroupId);

    Task<bool> DeleteUserAsync(Guid id);

    Task DeleteAllUsersAsync();

    Task<User?> UpdateBasicAsync(UpdateUserBasicDto dto);

    Task<IEnumerable<User>> GetTeachersByIdsAsync(IEnumerable<Guid> ids);
}