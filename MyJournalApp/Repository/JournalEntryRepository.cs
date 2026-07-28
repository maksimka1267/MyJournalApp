using Microsoft.EntityFrameworkCore;
using MyJournalApp.Data;
using MyJournalApp.Data.Models;

public class JournalEntryRepository : Repository<JournalEntry>, IJournalEntryRepository
{
    public JournalEntryRepository(JournalDbContext context) : base(context) { }

    public async Task<IEnumerable<JournalEntry>> GetByGroupIdAsync(Guid groupId)
    {
        return await _context.JournalEntries
            .Where(j => j.GroupId == groupId)
            .ToListAsync();
    }
    public async Task<IEnumerable<JournalEntry>> GetByTeacherIdAsync(Guid teacherId)
    {
        return await _context.JournalEntries
            .Where(j => j.TeacherId.Contains(teacherId))
            .ToListAsync();
    }
    public async Task<List<string>> GetJournalSubjectsByGroupIdsAsync(IEnumerable<Guid> groupIds)
    {
        return await _context.JournalEntries
            .Where(j => groupIds.Contains(j.GroupId))
            .Select(j => j.Name)
            .ToListAsync();
    }
    public async Task AddRangeAsync(IEnumerable<JournalEntry> entries)
    {
        await _context.JournalEntries.AddRangeAsync(entries);
    }
    public async Task<List<(Guid GroupId, string Name)>> GetJournalNamesWithGroupAsync(IEnumerable<Guid> groupIds)
    {
        return await _context.JournalEntries
            .Where(j => groupIds.Contains(j.GroupId))
            .Select(j => new ValueTuple<Guid, string>(j.GroupId, j.Name))
            .ToListAsync();
    }
    public async Task<List<JournalEntry>> GetByPeriodAsync(DateTime start, DateTime end)
    {
        return await _context.JournalEntries
            .Where(x => x.Date >= start && x.Date <= end)
            .ToListAsync();
    }
}
