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
}
