using MyJournalApp.Data.Models;

public interface IJournalEntryRepository : IRepository<JournalEntry>
{
    Task<IEnumerable<JournalEntry>> GetByGroupIdAsync(Guid groupId);
    Task<IEnumerable<JournalEntry>> GetByTeacherIdAsync(Guid teacherId);
    Task<List<string>> GetJournalSubjectsByGroupIdsAsync(IEnumerable<Guid> groupIds);
    Task<List<(Guid GroupId, string Name)>> GetJournalNamesWithGroupAsync(IEnumerable<Guid> groupIds);
    Task AddRangeAsync(IEnumerable<JournalEntry> entries);
    Task<List<JournalEntry>> GetByPeriodAsync(DateTime start, DateTime end);
}
