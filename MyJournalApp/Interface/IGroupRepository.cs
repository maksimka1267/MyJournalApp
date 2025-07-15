using MyJournalApp.Data.Models;

public interface IGroupRepository : IRepository<Group>
{
    Task<Group?> GetByTeacherIdAsync(Guid teacherId);
}
