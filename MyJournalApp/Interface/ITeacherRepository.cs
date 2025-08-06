using MyJournalApp.Data.Models;

public interface ITeacherRepository : IRepository<Teacher>
{
    Task<Teacher?> GetByGroupIdAsync(Guid groupId);
    Task<List<User>> GetAllTeachersAsync();
    Task<List<Teacher>> GetAllTeachersWithAdminAsync();
    Task<Guid?> GetTeacherIdByFullNameAsync(string fullName);
}
