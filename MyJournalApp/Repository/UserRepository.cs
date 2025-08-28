// File: UserRepository.cs
using Microsoft.EntityFrameworkCore;
using MyJournalApp.Data;
using MyJournalApp.Interface;
using System.Text.RegularExpressions;

namespace MyJournalApp.Repository
{
    public class UserRepository : Repository<User>, IUserRepository
    {
        public UserRepository(JournalDbContext context) : base(context) { }

        public async Task<User>? GetByEmail(string email)
        {
            if (email == null) throw new ArgumentNullException("email null");
            return await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
        }

        // 👇 ДОБАВЬТЕ ЭТОТ НОВЫЙ МЕТОД
        public async Task<IEnumerable<User>> GetUsersByIdsAsync(List<Guid> ids)
        {
            // Этот LINQ-запрос эффективно найдет всех пользователей,
            // чьи Id содержатся в переданном списке ids.
            return await _dbSet
                .Where(u => ids.Contains(u.Id))
                .ToListAsync();
        }
        public async Task<List<User>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken ct = default)
        {
            var idList = ids?.Distinct().ToList() ?? new();
            if (idList.Count == 0) return new();

            return await _dbSet
                .AsNoTracking()
                .Where(u => idList.Contains(u.Id))
                .ToListAsync(ct);
        }

    }
}