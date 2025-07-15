using Microsoft.EntityFrameworkCore;
using MyJournalApp.Data;
using MyJournalApp.Data.Models;

public class TeacherRepository : Repository<Teacher>, ITeacherRepository
{
    public TeacherRepository(JournalDbContext context) : base(context) { }

    public async Task<Teacher?> GetBySubjectIdAsync(Guid subjectId)
    {
        return await _context.Teachers
            .FirstOrDefaultAsync(t => t.SubjectIds != null && t.SubjectIds.Contains(subjectId));
    }
}
