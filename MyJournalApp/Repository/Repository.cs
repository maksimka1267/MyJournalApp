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

    public async Task<IEnumerable<T>> GetAllAsync() =>
        await _dbSet.AsNoTracking().ToListAsync();

    public async Task<T?> GetByIdAsync(Guid id) =>
        await _dbSet.AsNoTracking()
            .SingleOrDefaultAsync(e => EF.Property<Guid>(e, "Id") == id);

    public async Task AddAsync(T entity)
    {
        await _dbSet.AddAsync(entity);
        await _context.SaveChangesAsync();
    }
    public Task AddRangeAsync(IEnumerable<T> entities)
    {
        _dbSet.AddRange(entities);
        return Task.CompletedTask;
    }

    public async Task Update(T entity)
    {
        _dbSet.Update(entity);
        await _context.SaveChangesAsync();
    }

    public Task UpdateRange(IEnumerable<T> entities)
    {
        _dbSet.UpdateRange(entities);
        return Task.CompletedTask;
    }

    public async Task Delete(T entity)
    {
        _dbSet.Remove(entity);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAllAsync()
    {
        await _dbSet.ExecuteDeleteAsync();
    }

    public Task SaveChangesAsync()
    {
        return _context.SaveChangesAsync();
    }
}