using MyJournalApp.Data.Models;

public interface ITeacherRepository : IRepository<Teacher>
{
    Task<Teacher?> GetByGroupIdAsync(Guid groupId);
    Task<List<User>> GetAllTeachersAsync();
    Task<List<Teacher>> GetAllTeachersWithAdminAsync();
    Task<string?> GetFullNameByIdAsync(Guid teacherId);
    Task<bool> IsTeacherAsync(Guid userId);
    Task<Guid?> GetTeacherIdByFullNameAsync(string fullName);
    Task<User?> GetTeacherModelByFullNameAsync(string fullName);
    string ToShortName(string fullName);
}
