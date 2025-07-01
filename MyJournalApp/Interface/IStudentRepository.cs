using MyJournalApp.Data.Models;

namespace MyJournalApp.Interface
{
    public interface IStudentRepository : IRepository<Student>
    {
        Task<List<Student>> GetByGroupAsync(Guid groupId);
        Task<Student?> GetByEmailAsync(string email);
        Task<bool> EmailExistsAsync(string email);
    }
}
