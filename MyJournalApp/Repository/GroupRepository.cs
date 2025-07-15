using Microsoft.EntityFrameworkCore;
using MyJournalApp.Data;
using MyJournalApp.Data.Models;

public class GroupRepository : Repository<Group>, IGroupRepository
{
    public GroupRepository(JournalDbContext context) : base(context) { }

    public async Task<Group?> GetByTeacherIdAsync(Guid teacherId)
    {
        return await _context.Groups.FirstOrDefaultAsync(g => g.TeacherId == teacherId);
    }
}
