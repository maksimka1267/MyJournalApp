namespace MyJournalApp.Interface
{
    public interface IUserRepository:IRepository<User>
    {
        Task<User>? GetByEmail(string email);
        Task<IEnumerable<User>> GetUsersByIdsAsync(List<Guid> ids);
        Task<List<User>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken ct = default);

    }
}
