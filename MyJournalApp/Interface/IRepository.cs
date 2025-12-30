public interface IRepository<T> where T : class
{
    Task<IEnumerable<T>> GetAllAsync();
    Task<T?> GetByIdAsync(Guid id);
    Task AddAsync(T entity);
    Task Update(T entity);
    Task UpdateRange(IEnumerable<T> entities);
    Task Delete(T entity);
    Task DeleteAllAsync();
    Task SaveChangesAsync();
}
