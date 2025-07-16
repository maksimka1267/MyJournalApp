public interface IGradeRepository : IRepository<Grade>
{
    Task<IEnumerable<Grade>> GetByJournalEntryIdAsync(Guid journalEntryId);
}
