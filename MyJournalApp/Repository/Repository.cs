using Microsoft.EntityFrameworkCore;
using MyJournalApp.Data;
using MyJournalApp.Interface;

namespace MyJournalApp.Repository
{
    public class Repository<T> : IRepository<T> where T : class
    {
        protected readonly JournalDbContext _context;
        protected readonly DbSet<T> _dbSet;

        public Repository(JournalDbContext context)
        {
            _context = context;
            _dbSet = context.Set<T>();
        }

        public virtual async Task<IEnumerable<T>> GetAllAsync() =>
            await _dbSet.ToListAsync();

        public virtual async Task<T?> GetByIdAsync(Guid id) =>
            await _dbSet.FindAsync(id);

        public virtual async Task AddAsync(T entity)
        {
            await _dbSet.AddAsync(entity);
        }

        public virtual Task UpdateAsync(T entity)
        {
            _dbSet.Update(entity);
            return Task.CompletedTask;
        }

        public virtual async Task DeleteAsync(Guid id)
        {
            var entity = await GetByIdAsync(id);
            if (entity != null)
                _dbSet.Remove(entity);
        }

        public virtual async Task SaveAsync() =>
            await _context.SaveChangesAsync();
    }

}
