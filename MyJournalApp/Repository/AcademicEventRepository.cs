using Microsoft.EntityFrameworkCore;
using MyJournalApp.Data;
using MyJournalApp.Data.Models;

public class AcademicEventRepository : Repository<AcademicEvent>, IAcademicEventRepository
{
    public AcademicEventRepository(JournalDbContext context) : base(context) { }

    public async Task<IEnumerable<AcademicEvent>> GetUpcomingAsync()
    {
        return await _context.AcademicEvents
            .Where(e => e.StartDate >= DateTime.Today)
            .OrderBy(e => e.StartDate)
            .ToListAsync();
    }
}
