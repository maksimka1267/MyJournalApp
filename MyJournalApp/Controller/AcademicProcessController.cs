using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyJournalApp.Data.Models;
using MyJournalApp.Interface;

[ApiController]
[Route("api/[controller]")]
public class AcademicProcessController : ControllerBase
{
    private readonly IAcademicEventRepository _eventRepository;

    public AcademicProcessController(IAcademicEventRepository eventRepository)
    {
        _eventRepository = eventRepository;
    }

    [HttpGet]
    public async Task<IActionResult> GetAllEvents()
    {
        var events = await _eventRepository.GetUpcomingAsync();
        return Ok(events);
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    public async Task<IActionResult> AddEvent([FromBody] AcademicEventDto dto)
    {
        var academicEvent = new AcademicEvent
        {
            Id = Guid.NewGuid(),
            Type = dto.Type,
            StartDate = dto.StartDate,
            EndDate = dto.EndDate,
            Description = dto.Description
        };

        await _eventRepository.AddAsync(academicEvent);
        await _eventRepository.SaveChangesAsync();

        return Ok(academicEvent);
    }

    [Authorize(Roles = "Admin")]
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateEvent(Guid id, [FromBody] AcademicEventDto dto)
    {
        var existing = await _eventRepository.GetByIdAsync(id);
        if (existing == null) return NotFound();

        existing.Type = dto.Type;
        existing.StartDate = dto.StartDate;
        existing.EndDate = dto.EndDate;
        existing.Description = dto.Description;

        _eventRepository.Update(existing);
        await _eventRepository.SaveChangesAsync();

        return Ok(existing);
    }

    [Authorize(Roles = "Admin")]
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteEvent(Guid id)
    {
        var existing = await _eventRepository.GetByIdAsync(id);
        if (existing == null) return NotFound();

        _eventRepository.Delete(existing);
        await _eventRepository.SaveChangesAsync();

        return Ok("Deleted");
    }
    public class AcademicEventDto
    {
        public string Type { get; set; } // "Practice", "Holiday", "ExamSession"
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string Description { get; set; }
    }

}
