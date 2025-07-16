using Microsoft.EntityFrameworkCore;
using MyJournalApp.Data;
using MyJournalApp.Interface;
using System.Text.RegularExpressions;

namespace MyJournalApp.Repository
{
    public class UserRepository:Repository<User>, IUserRepository
    {
        public UserRepository(JournalDbContext context) : base(context) { }

        public async Task<User>? GetByEmail(string email)
        {
            if(email == null) throw new ArgumentNullException("email null");
            return await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
        }
    }
}
