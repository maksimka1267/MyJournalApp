using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/[controller]")]
public class AcademicProcessController : ControllerBase
{
    private readonly IAcademicProcessService _academicProcessService;

    public AcademicProcessController(IAcademicProcessService academicProcessService)
    {
        _academicProcessService = academicProcessService;
    }

    [HttpGet("{groupId}/{year}")]
    public async Task<IActionResult> GetByGroupAndYear(Guid groupId, int year)
    {
        var events = await _academicProcessService.GetByGroupAndYearAsync(groupId, year);
        return Ok(events);
    }

    [HttpPost]
    public async Task<IActionResult> AddEvent([FromBody] AcademicEventDto dto)
    {
        var result = await _academicProcessService.AddEventAsync(dto);
        return Ok(result);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateEvent(Guid id, [FromBody] AcademicEventDto dto)
    {
        var result = await _academicProcessService.UpdateEventAsync(id, dto);

        if (result == null)
            return NotFound();

        return Ok(result);
    }

    [Authorize]
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteEvent(Guid id)
    {
        var deleted = await _academicProcessService.DeleteEventAsync(id);

        if (!deleted)
            return NotFound();

        return Ok("Deleted");
    }

    [Authorize]
    [HttpPut("bulk")]
    public async Task<IActionResult> BulkUpdate([FromBody] List<AcademicEventDto> events)
    {
        await _academicProcessService.BulkUpdateAsync(events);
        return Ok();
    }
}