using Microsoft.EntityFrameworkCore;
using MyJournalApp.Data;

public class GradeRepository : Repository<Grade>, IGradeRepository
{
    public GradeRepository(JournalDbContext context) : base(context) { }

    public async Task<IEnumerable<Grade>> GetByJournalEntryIdAsync(Guid journalEntryId)
    {
        return await _context.Grades
            .Where(g => g.JournalEntryId == journalEntryId)
            .ToListAsync();
    }
}
