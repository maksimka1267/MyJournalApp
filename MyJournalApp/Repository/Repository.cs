using Microsoft.EntityFrameworkCore;
using MyJournalApp.Data;

public class Repository<T> : IRepository<T> where T : class
{
    protected readonly JournalDbContext _context;
    protected readonly DbSet<T> _dbSet;

    public Repository(JournalDbContext context)
    {
        _context = context;
        _dbSet = _context.Set<T>();
    }

    public async Task<IEnumerable<T>> GetAllAsync() => await _dbSet.ToListAsync();

    public async Task<T?> GetByIdAsync(Guid id) => await _dbSet.FindAsync(id);

    public async Task AddAsync(T entity) => await _dbSet.AddAsync(entity);

    public async Task Update(T entity)
    {
        _dbSet.Update(entity);
        await _context.SaveChangesAsync();
    }
    public async Task Delete(T entity)
    {
        _dbSet.Remove(entity);
        await _context.SaveChangesAsync();
    }
    public async Task DeleteAllAsync()
    {
        _dbSet.RemoveRange(_dbSet);
        await _context.SaveChangesAsync();
    }

    public async Task SaveChangesAsync() => await _context.SaveChangesAsync();
}
