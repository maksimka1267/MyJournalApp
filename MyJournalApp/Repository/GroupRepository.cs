using Microsoft.EntityFrameworkCore;
using MyJournalApp.Data;
using MyJournalApp.Data.Models;

public class GroupRepository : Repository<Group>, IGroupRepository
{
    public GroupRepository(JournalDbContext context) : base(context) { }

    public async Task<IEnumerable<Group>> GetByTeacherIdAsync(Guid teacherId)
    {
        return await _context.Groups
            .Where(g => g.TeacherId == teacherId)
            .ToListAsync();
    }
    public async Task<bool> ExistsAsync(Guid groupId)
    {
        return await _context.Groups.AnyAsync(g => g.Id == groupId);
    }
    public async Task<string?> GetNameByIdAsync(Guid groupId)
    {
        return await _context.Groups
            .Where(g => g.Id == groupId)
            .Select(g => g.Name)
            .FirstOrDefaultAsync();
    }
    public async Task<List<Group>> GetByIdsAsync(List<Guid> ids)
    {
        return await _context.Groups
            .Where(x => ids.Contains(x.Id))
            .ToListAsync();
    }
    public async Task<IEnumerable<Group>> GetGroupsWithLessonsAsync()
    {
        var groupIdsWithLessons = await _context.Lessons
            .Select(l => l.GroupId)
            .Distinct()
            .ToListAsync();

        return await _context.Groups
            .Where(g => groupIdsWithLessons.Contains(g.Id))
            .ToListAsync();
    }
}
