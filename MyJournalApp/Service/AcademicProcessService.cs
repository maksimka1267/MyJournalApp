using MyJournalApp.Data.Models;
using static AcademicProcessController;

public class AcademicProcessService : IAcademicProcessService
{
    private readonly IAcademicEventRepository _eventRepository;

    public AcademicProcessService(IAcademicEventRepository eventRepository)
    {
        _eventRepository = eventRepository;
    }

    public async Task<IEnumerable<AcademicEvent>> GetByGroupAndYearAsync(Guid groupId, int year)
    {
        return await _eventRepository.GetByGroupAndYearAsync(groupId, year);
    }

    public async Task<AcademicEvent> AddEventAsync(AcademicEventDto dto)
    {
        var academicEvent = new AcademicEvent
        {
            Id = Guid.NewGuid(),
            GroupId = dto.GroupId,
            Type = dto.Type,
            Year = dto.Year,
            Month = dto.Month,
            WeekNumber = dto.WeekNumber,
            StartDate = dto.StartDate,
            EndDate = dto.EndDate
        };

        await _eventRepository.AddAsync(academicEvent);
        await _eventRepository.SaveChangesAsync();
        return academicEvent;
    }

    public async Task<AcademicEvent?> UpdateEventAsync(Guid id, AcademicEventDto dto)
    {
        var existing = await _eventRepository.GetByIdAsync(id);

        if (existing == null)
            return null;

        existing.Type = dto.Type;
        existing.Year = dto.Year;
        existing.Month = dto.Month;
        existing.WeekNumber = dto.WeekNumber;
        existing.StartDate = dto.StartDate;
        existing.EndDate = dto.EndDate;

        await _eventRepository.Update(existing);
        await _eventRepository.SaveChangesAsync();
        return existing;
    }

    public async Task<bool> DeleteEventAsync(Guid id)
    {
        var existing = await _eventRepository.GetByIdAsync(id);

        if (existing == null)
            return false;

        await _eventRepository.Delete(existing);
        await _eventRepository.SaveChangesAsync();
        return true;
    }

    public async Task BulkUpdateAsync(List<AcademicEventDto> events)
    {
        foreach (var dto in events)
        {
            var existing = await _eventRepository.GetByIdAsync(dto.Id);

            if (existing == null)
            {
                var newEvent = new AcademicEvent
                {
                    Id = dto.Id == Guid.Empty ? Guid.NewGuid() : dto.Id,
                    GroupId = dto.GroupId,
                    Type = dto.Type,
                    Year = dto.Year,
                    Month = dto.Month,
                    WeekNumber = dto.WeekNumber,
                    StartDate = dto.StartDate,
                    EndDate = dto.EndDate
                };

                await _eventRepository.AddAsync(newEvent);
            }
            else
            {
                existing.Type = dto.Type;
                existing.Year = dto.Year;
                existing.Month = dto.Month;
                existing.WeekNumber = dto.WeekNumber;
                existing.StartDate = dto.StartDate;
                existing.EndDate = dto.EndDate;

                await _eventRepository.Update(existing);
            }
        }
        await _eventRepository.SaveChangesAsync();
    }
}