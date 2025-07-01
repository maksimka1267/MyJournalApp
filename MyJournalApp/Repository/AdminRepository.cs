using Microsoft.EntityFrameworkCore;
using MyJournalApp.Data;
using MyJournalApp.Data.Models;
using MyJournalApp.Interface;

namespace MyJournalApp.Repository
{
    public class AdminRepository: Repository<Admin>, IAdminRepository
    {
        public AdminRepository(JournalDbContext context) : base(context)
        {
        }
        public async Task<Admin?> GetByEmailAsync(string email)
        {
            return await _dbSet.FirstOrDefaultAsync(c => c.Email == email);
        }

        public async Task<bool> EmailExistsAsync(string email)
        {
            return await _dbSet.AnyAsync(c => c.Email == email);
        }
    }
}
