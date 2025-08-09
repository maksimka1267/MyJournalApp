using MyJournalApp.Data.Models;

public interface IGroupRepository : IRepository<Group>
{
    Task<IEnumerable<Group>> GetByTeacherIdAsync(Guid teacherId);
    Task<bool> ExistsAsync(Guid groupId);

}
