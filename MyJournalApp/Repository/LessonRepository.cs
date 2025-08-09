using Microsoft.EntityFrameworkCore;
using MyJournalApp.Data;

public class LessonRepository : Repository<Lesson>, ILessonRepository
{
    public LessonRepository(JournalDbContext context) : base(context) { }

    public async Task<IEnumerable<Lesson>> GetLessonsByGroupIdAsync(Guid groupId)
    {
        return await _dbSet
            .Where(l => l.GroupId == groupId)
            .OrderBy(l => l.StartTime)
            .ToListAsync();
    }

    public async Task<IEnumerable<Lesson>> GetLessonsByDateAsync(Guid groupId, DateTime date)
    {
        return await _dbSet
            .Where(l => l.GroupId == groupId && l.StartTime.Date == date.Date)
            .OrderBy(l => l.StartTime)
            .ToListAsync();
    }
    public async Task DeleteLessonsAsync(IEnumerable<Lesson> lessons)
    {
        _dbSet.RemoveRange(lessons);
    }

    public async Task AddRangeAsync(IEnumerable<Lesson> lessons)
    {
        await _dbSet.AddRangeAsync(lessons);
    }
}
