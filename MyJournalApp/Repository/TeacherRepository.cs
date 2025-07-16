using Microsoft.EntityFrameworkCore;
using MyJournalApp.Data;
using MyJournalApp.Data.Models;

public class TeacherRepository : Repository<Teacher>, ITeacherRepository
{
    public TeacherRepository(JournalDbContext context) : base(context) { }

    public async Task<Teacher?> GetByGroupIdAsync(Guid groupId)
    {
        return await _context.Teachers
            .FirstOrDefaultAsync(t => t.GroupIds != null && t.GroupIds.Contains(groupId));
    }

}
