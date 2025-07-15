using MyJournalApp.Data.Models;

public interface IAcademicEventRepository : IRepository<AcademicEvent>
{
    Task<IEnumerable<AcademicEvent>> GetUpcomingAsync();
}
