using Microsoft.EntityFrameworkCore;
using MyJournalApp.Data;
using MyJournalApp.Data.Models;

public class ScheduleRepository : Repository<Schedule>, IScheduleRepository
{
    public ScheduleRepository(JournalDbContext context) : base(context) { }

    public async Task<IEnumerable<Schedule>> GetByGroupIdAsync(Guid groupId)
    {
        return await _context.Schedules
            .Where(s => s.GroupId == groupId)
            .ToListAsync();
    }
    public async Task<IEnumerable<Schedule>> GetByTeacherIdAsync(Guid teacherId)
    {
        return await _context.Schedules
            .Where(s => s.TeacherId == teacherId)
            .ToListAsync();
    }

}
