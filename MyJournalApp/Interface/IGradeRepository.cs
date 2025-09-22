public interface IGradeRepository : IRepository<Grade>
{
    Task<IEnumerable<Grade>> GetByJournalEntryIdAsync(Guid journalEntryId);
    Task<List<Grade>> GetByJournalAndDateAsync(Guid journalEntryId, DateTime date);
    Task<IEnumerable<Grade>> GetAbsencesByStudentIdsAndDateRangeAsync(List<Guid> studentIds, DateTime startDate, DateTime endDate);
    Task<IEnumerable<Grade>> GetByStudentIdAsync(Guid studentId);
    Task<int> DeleteByJournalEntryIdAsync(Guid journalEntryId);
    Task<IReadOnlyList<Grade>> GetByStudentIdsAndDateRangeAsync(IEnumerable<Guid> studentIds, DateTime startDate, DateTime endDate);
}
