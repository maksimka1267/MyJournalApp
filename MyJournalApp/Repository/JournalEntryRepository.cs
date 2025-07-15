using Microsoft.EntityFrameworkCore;
using MyJournalApp.Data;
using MyJournalApp.Data.Models;

public class JournalEntryRepository : Repository<JournalEntry>, IJournalEntryRepository
{
    public JournalEntryRepository(JournalDbContext context) : base(context) { }

    public async Task<IEnumerable<JournalEntry>> GetByStudentIdAsync(Guid studentId)
    {
        return await _context.JournalEntries
            .Where(j => j.StudentId == studentId)
            .ToListAsync();
    }
}
