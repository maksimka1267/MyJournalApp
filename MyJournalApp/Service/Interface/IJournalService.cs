using MyJournalApp.Data.Dtos.Journal;
using MyJournalApp.Result;

namespace MyJournalApp.Service.Interface
{
    public interface IJournalService
    {
        Task<IEnumerable<JournalEntry>> GetAllAsync();

        Task<IEnumerable<JournalEntry>> GetTeacherJournalsAsync(Guid teacherId);

        Task<JournalEntry?> GetByIdAsync(Guid id);

        Task<ServiceResult<JournalEntry>> CreateAsync(JournalEntry journal);

        Task<ServiceResult<JournalEntry>> UpdateAsync(Guid id, JournalEntry journal);

        Task<IServiceResult> DeleteAsync(Guid id);
    }
}
