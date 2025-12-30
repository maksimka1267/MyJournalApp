using MyJournalApp.Data.Models;

public interface IAcademicEventRepository: IRepository<AcademicEvent>
{
    Task<List<AcademicEvent>> GetByGroupAsync(Guid groupId);
    Task<List<AcademicEvent>> GetByGroupAndYearAsync(Guid groupId, int year);
    Task<AcademicEvent?> GetByGroupWeekAsync(Guid groupId, int year, int weekNumber);
    Task<List<AcademicEvent>> GetBetweenDatesAsync(Guid groupId, DateTime from, DateTime to);
    Task AddOrUpdateBulkAsync(List<AcademicEvent> events);
}
