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

    // Совпадает с интерфейсом: Task<IEnumerable<T>>
    public async Task<IEnumerable<T>> GetAllAsync() =>
        await _dbSet.AsNoTracking().ToListAsync();

    // Также AsNoTracking и без FindAsync (чтобы не держать трекинг)
    public async Task<T?> GetByIdAsync(Guid id) =>
        await _dbSet.AsNoTracking()
            .SingleOrDefaultAsync(e => EF.Property<Guid>(e, "Id") == id);

    public async Task AddAsync(T entity)
    {
        _dbSet.Add(entity);
        await _context.SaveChangesAsync();
    }
    public async Task UpdateRange(IEnumerable<T> entities)
    {
        _dbSet.UpdateRange(entities);
        await _context.SaveChangesAsync();
    }

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

    public Task SaveChangesAsync() => _context.SaveChangesAsync();
}
