using static AcademicProcessController;

public interface IAcademicProcessService
{
    Task<IEnumerable<AcademicEvent>> GetByGroupAndYearAsync(Guid groupId, int year);

    Task<AcademicEvent> AddEventAsync(AcademicEventDto dto);

    Task<AcademicEvent?> UpdateEventAsync(Guid id, AcademicEventDto dto);

    Task<bool> DeleteEventAsync(Guid id);

    Task BulkUpdateAsync(List<AcademicEventDto> events);
}