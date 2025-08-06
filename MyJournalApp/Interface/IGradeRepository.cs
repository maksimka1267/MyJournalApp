public interface IGradeRepository : IRepository<Grade>
{
    Task<IEnumerable<Grade>> GetByJournalEntryIdAsync(Guid journalEntryId);
    Task<IEnumerable<Grade>> GetByStudentIdAsync(Guid studentId);

}
