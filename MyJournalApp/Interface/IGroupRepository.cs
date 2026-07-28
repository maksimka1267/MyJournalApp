using MyJournalApp.Data.Models;

public interface IGroupRepository : IRepository<Group>
{
    Task<IEnumerable<Group>> GetByTeacherIdAsync(Guid teacherId);
    Task<string?> GetNameByIdAsync(Guid groupId);
    Task<IEnumerable<Group>> GetGroupsWithLessonsAsync();
    Task<bool> ExistsAsync(Guid groupId);
    Task<List<Group>> GetByIdsAsync(List<Guid> ids);
}
