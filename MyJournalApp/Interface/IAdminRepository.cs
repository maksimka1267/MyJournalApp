using MyJournalApp.Data.Models;

namespace MyJournalApp.Interface
{
    public interface IAdminRepository:IRepository<Admin>
    {
        Task<Admin?> GetByEmailAsync(string email);
        Task<bool> EmailExistsAsync(string email);
    }
}
