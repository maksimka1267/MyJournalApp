using MyJournalApp.Data.Models;

public interface ITeacherRepository : IRepository<Teacher>
{
    Task<Teacher?> GetByGroupIdAsync(Guid groupId);

}
