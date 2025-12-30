using Microsoft.EntityFrameworkCore;
using MyJournalApp.Data;

public class LessonRepository : Repository<Lesson>, ILessonRepository
{
    public LessonRepository(JournalDbContext context) : base(context) { }

    public async Task<IEnumerable<Lesson>> GetLessonsByGroupIdAsync(Guid groupId)
    {
        return await _dbSet.AsNoTracking()
            .Where(l => l.GroupId == groupId)
            .OrderBy(l => l.StartTime)
            .ToListAsync();
    }
    public async Task<List<string>> GetSubjectsByTeacherAsync(Guid teacherId, DateTime start, DateTime end)
    {
        return await _context.Lessons
            .Where(l => l.TeacherId == teacherId &&
                        l.StartTime >= start &&
                        l.StartTime <= end)
            .Select(l => l.Name)
            .Distinct()
            .OrderBy(n => n)
            .ToListAsync();
    }

    public async Task<List<Lesson>> GetByTeacherAsync(
        Guid teacherId, DateTime from, DateTime to, Guid? groupId, string? subject)
    {
        var q = _dbSet.AsNoTracking()
            .Where(l => l.TeacherId == teacherId &&
                        l.StartTime >= from && l.StartTime <= to);

        if (groupId.HasValue) q = q.Where(l => l.GroupId == groupId.Value);
        if (!string.IsNullOrWhiteSpace(subject)) q = q.Where(l => l.Name == subject);

        return await q
            .OrderBy(l => l.GroupId).ThenBy(l => l.Name).ThenBy(l => l.StartTime)
            .ToListAsync();
    }

    public async Task<IEnumerable<Lesson>> GetLessonsByDateAsync(Guid groupId, DateTime date)
    {
        var start = date.Date;
        var end = start.AddDays(1);
        return await _dbSet.AsNoTracking()
            .Where(l => l.GroupId == groupId && l.StartTime >= start && l.StartTime < end)
            .OrderBy(l => l.StartTime)
            .ToListAsync();
    }

    public async Task DeleteLessonsAsync(IEnumerable<Lesson> lessons)
    {
        _dbSet.RemoveRange(lessons);
        await _context.SaveChangesAsync();
    }

    public async Task AddRangeAsync(IEnumerable<Lesson> lessons)
    {
        _dbSet.AddRange(lessons);
        await _context.SaveChangesAsync();
    }
}
