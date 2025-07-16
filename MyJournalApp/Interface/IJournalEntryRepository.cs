using MyJournalApp.Data.Models;

public interface IJournalEntryRepository : IRepository<JournalEntry>
{
    Task<IEnumerable<JournalEntry>> GetByGroupIdAsync(Guid groupId);
    Task<IEnumerable<JournalEntry>> GetByTeacherIdAsync(Guid teacherId);

}
