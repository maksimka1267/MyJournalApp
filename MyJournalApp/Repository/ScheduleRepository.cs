using Microsoft.EntityFrameworkCore;
using MyJournalApp.Data;

public class ScheduleRepository : Repository<Schedule>, IScheduleRepository
{
    public ScheduleRepository(JournalDbContext context) : base(context) { }

    public async Task<Schedule?> GetByGroupAndWeekAsync(Guid groupId, DateOnly weekStart)
    {
        return await _dbSet
            .Include(s => s.Lessons)
            .FirstOrDefaultAsync(s => s.GroupId == groupId && s.WeekStartDate == weekStart);
    }
}
