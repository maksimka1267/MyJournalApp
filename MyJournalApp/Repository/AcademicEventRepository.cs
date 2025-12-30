using Microsoft.EntityFrameworkCore;
using MyJournalApp.Data;
using MyJournalApp.Data.Models;

public class AcademicEventRepository : Repository<AcademicEvent>, IAcademicEventRepository
{
    public AcademicEventRepository(JournalDbContext context) : base(context) { }

    public async Task<List<AcademicEvent>> GetByGroupAsync(Guid groupId)
    {
        return await _context.AcademicEvents
            .Where(e => e.GroupId == groupId)
            .OrderBy(e => e.Year)
            .ThenBy(e => e.WeekNumber)
            .ToListAsync();
    }
    public async Task<List<AcademicEvent>> GetByGroupAndYearAsync(Guid groupId, int year)
    {
        return await _context.AcademicEvents
            .Where(e => e.GroupId == groupId && e.Year == year)
            .OrderBy(e => e.WeekNumber)
            .ToListAsync();
    }
    public async Task<AcademicEvent?> GetByGroupWeekAsync(Guid groupId, int year, int weekNumber)
    {
        return await _context.AcademicEvents
            .FirstOrDefaultAsync(e =>
                e.GroupId == groupId &&
                e.Year == year &&
                e.WeekNumber == weekNumber);
    }

    public async Task<List<AcademicEvent>> GetBetweenDatesAsync(Guid groupId, DateTime from, DateTime to)
    {
        return await _context.AcademicEvents
            .Where(e => e.GroupId == groupId && e.StartDate >= from && e.EndDate <= to)
            .OrderBy(e => e.StartDate)
            .ToListAsync();
    }

    public async Task AddOrUpdateBulkAsync(List<AcademicEvent> events)
    {
        foreach (var e in events)
        {
            var existing = await GetByGroupWeekAsync(e.GroupId, e.Year, e.WeekNumber);
            if (existing != null)
            {
                existing.Type = e.Type;
                existing.Month = e.Month;
                existing.StartDate = e.StartDate;
                existing.EndDate = e.EndDate;
                _context.AcademicEvents.Update(existing);
            }
            else
            {
                _context.AcademicEvents.Add(e);
            }
        }

        await _context.SaveChangesAsync();
    }
}

