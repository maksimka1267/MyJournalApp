using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
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
    public async Task<int> DeleteByJournalEntryIdAsync(Guid journalEntryId)
    {
        var grades = await _context.Grades.Where(g => g.JournalEntryId == journalEntryId).ToListAsync();
        if (grades.Count == 0) return 0;

        _context.Grades.RemoveRange(grades);
        return await _context.SaveChangesAsync();
    }

    public async Task<IEnumerable<Grade>> GetByStudentIdAsync(Guid studentId)
    {
        return await _context.Grades
            .Where(g => g.StudentId == studentId
                        && g.Value.HasValue)   // исключаем null
            .ToListAsync();
    }
    public async Task<List<Grade>> GetByJournalAndDateAsync(Guid journalEntryId, DateTime date)
    {
        return await _context.Grades
            .Where(g => g.JournalEntryId == journalEntryId && g.Created.Date == date.Date)
            .ToListAsync();
    }
    public async Task<IReadOnlyList<Grade>> GetByStudentIdsAndDateRangeAsync(IEnumerable<Guid> studentIds, DateTime startDate, DateTime endDate)
    {
        var ids = studentIds.ToList();
        if (ids.Count == 0) return Array.Empty<Grade>();

        var start = startDate.Date;
        var endExclusive = endDate.Date.AddDays(1); // [start, end]

        return await _context.Grades
            .AsNoTracking()
            .Where(g => ids.Contains(g.StudentId) &&
                        g.Created >= start &&
                        g.Created < endExclusive)
            .ToListAsync();
    }

}
