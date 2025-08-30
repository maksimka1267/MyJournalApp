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
        await _context.SaveChangesAsync();
    }
}
