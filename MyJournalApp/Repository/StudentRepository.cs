using Microsoft.EntityFrameworkCore;
using MyJournalApp.Data;
using MyJournalApp.Interface;

namespace MyJournalApp.Repository
{
    public class StudentRepository : Repository<Student>, IStudentRepository
    {
        public StudentRepository(JournalDbContext context) : base(context) { }

        public async Task<IEnumerable<Student>> GetByGroupIdAsync(Guid groupId)
        {
            return await _context.Students
                .Where(s => s.GroupId == groupId)
                .ToListAsync();
        }
    }

}
