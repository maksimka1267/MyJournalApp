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
    public async Task<IEnumerable<Grade>> GetAbsencesByStudentIdsAndDateRangeAsync(List<Guid> studentIds, DateTime startDate, DateTime endDate)
    {
        return await _context.Grades
            .Where(g => studentIds.Contains(g.StudentId) &&      // Фильтруем по студентам группы
                         g.IsPresent == false &&                   // Нам нужны только "Н-ки"
                         g.Created.Date >= startDate.Date &&       // В указанном диапазоне дат
                         g.Created.Date <= endDate.Date)
            .ToListAsync();
    }

    public async Task<IEnumerable<Grade>> GetByStudentIdAsync(Guid studentId)
    {
        return await _context.Grades
            .Where(g => g.StudentId == studentId)
            .ToListAsync();
    }
    public async Task<List<Grade>> GetByJournalAndDateAsync(Guid journalEntryId, DateTime date)
    {
        return await _context.Grades
            .Where(g => g.JournalEntryId == journalEntryId && g.Created.Date == date.Date)
            .ToListAsync();
    }

}
