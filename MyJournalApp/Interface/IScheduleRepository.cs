using MyJournalApp.Data.Models;

public interface IScheduleRepository : IRepository<Schedule>
{
    Task<IEnumerable<Schedule>> GetByGroupIdAsync(Guid groupId);
}
