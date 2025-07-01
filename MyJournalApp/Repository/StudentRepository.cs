using MyJournalApp.Data.Models;
using MyJournalApp.Data;
using MyJournalApp.Interface;
using Microsoft.EntityFrameworkCore;

namespace MyJournalApp.Repository
{
    public class StudentRepository : Repository<Student>, IStudentRepository
    {
        public StudentRepository(JournalDbContext context) : base(context) { }

        public async Task<List<Student>> GetByGroupAsync(Guid groupId)
        {
            return await _dbSet
                .Where(s => s.GroupId == groupId)
                .ToListAsync();
        }
        public async Task<Student?> GetByEmailAsync(string email)
        {
            return await _dbSet.FirstOrDefaultAsync(c => c.Email == email);
        }

        public async Task<bool> EmailExistsAsync(string email)
        {
            return await _dbSet.AnyAsync(c => c.Email == email);
        }
    }

}
