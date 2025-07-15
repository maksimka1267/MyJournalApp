using MyJournalApp.Data.Models;

public interface IJournalEntryRepository : IRepository<JournalEntry>
{
    Task<IEnumerable<JournalEntry>> GetByStudentIdAsync(Guid studentId);
}
