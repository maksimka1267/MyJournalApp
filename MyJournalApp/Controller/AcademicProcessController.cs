using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/[controller]")]
public class AcademicProcessController : ControllerBase
{
    private readonly IAcademicEventRepository _eventRepository;
    private readonly IGroupRepository _groupRepository;

    public AcademicProcessController(IAcademicEventRepository eventRepository,
                                     IGroupRepository groupRepository)
    {
        _eventRepository = eventRepository;
        _groupRepository = groupRepository;
    }

    [HttpGet("{groupId}/{year}")]
    public async Task<IActionResult> GetByGroupAndYear(Guid groupId, int year)
    {
        var events = await _eventRepository.GetByGroupAndYearAsync(groupId, year);
        return Ok(events);
    }

    [HttpPost]
    public async Task<IActionResult> AddEvent([FromBody] AcademicEventDto dto)
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

        return Ok(academicEvent);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateEvent(Guid id, [FromBody] AcademicEventDto dto)
    {
        var existing = await _eventRepository.GetByIdAsync(id);
        if (existing == null) return NotFound();

        existing.Type = dto.Type;
        existing.Year = dto.Year;
        existing.Month = dto.Month;
        existing.WeekNumber = dto.WeekNumber;
        existing.StartDate = dto.StartDate;
        existing.EndDate = dto.EndDate;

        await _eventRepository.Update(existing);
        await _eventRepository.SaveChangesAsync();

        return Ok(existing);
    }

    [Authorize(Roles = "Admin")]
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteEvent(Guid id)
    {
        var existing = await _eventRepository.GetByIdAsync(id);
        if (existing == null) return NotFound();

        await _eventRepository.Delete(existing);
        await _eventRepository.SaveChangesAsync();

        return Ok("Deleted");
    }
    [Authorize(Roles = "Admin")]
    [HttpPut("bulk")]
    public async Task<IActionResult> BulkUpdate([FromBody] List<AcademicEventDto> events)
    {
        foreach (var dto in events)
        {
            var existing = await _eventRepository.GetByIdAsync(dto.Id);
            if (existing == null)
            {
                // 👇 Вставляем новую запись
                var newEvent = new AcademicEvent
                {
                    Id = dto.Id,
                    GroupId = dto.GroupId,
                    Type = dto.Type,
                    Year = dto.Year,
                    Month = dto.Month,
                    WeekNumber = dto.WeekNumber,
                    StartDate = dto.StartDate,
                    EndDate = dto.EndDate
                };

                await _eventRepository.AddAsync(newEvent);
                continue;
            }

            // 👇 Обновление
            existing.Type = dto.Type;
            existing.Year = dto.Year;
            existing.Month = dto.Month;
            existing.WeekNumber = dto.WeekNumber;
            existing.StartDate = dto.StartDate;
            existing.EndDate = dto.EndDate;

            await _eventRepository.Update(existing);
        }

        await _eventRepository.SaveChangesAsync();
        return Ok();
    }
    public class AcademicEventDto
    {
        public Guid Id { get; set; }
        public Guid GroupId { get; set; }
        public AcademicWeekType Type { get; set; }
        public int Year { get; set; }
        public int Month { get; set; }
        public int WeekNumber { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
    }

    public class AcademicEventUploadDto
    {
        public IFormFile File { get; set; }
    }
}
