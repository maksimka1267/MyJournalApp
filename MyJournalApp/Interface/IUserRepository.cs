namespace MyJournalApp.Interface
{
    public interface IUserRepository:IRepository<User>
    {
        Task<User>? GetByEmail(string email);
    }
}
