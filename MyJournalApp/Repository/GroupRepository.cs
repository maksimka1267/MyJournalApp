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

}
