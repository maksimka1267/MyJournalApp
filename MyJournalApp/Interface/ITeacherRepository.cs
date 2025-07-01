using MyJournalApp.Data.Models;
using MyJournalApp.Repository;

namespace MyJournalApp.Interface
{
    public interface ITeacherRepository: IRepository <Teacher>
    {
        Task<Teacher?> GetByEmailAsync(string email);
        Task<bool> EmailExistsAsync(string email);
    }
}
