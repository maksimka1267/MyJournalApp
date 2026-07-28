using Microsoft.AspNetCore.Mvc;
using MyJournalApp.Service.Interface;

[ApiController]
[Route("api/[controller]")]
public class ScheduleController : ControllerBase
{
    private readonly IScheduleService _scheduleService;

    public ScheduleController(IScheduleService scheduleService)
    {
        _scheduleService = scheduleService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var schedules = await _scheduleService.GetAllAsync();
        return Ok(schedules);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var schedule = await _scheduleService.GetByIdAsync(id);

        if (schedule == null)
            return NotFound();

        return Ok(schedule);
    }

    [HttpGet("group/{groupId}/week/{weekStart}")]
    public async Task<IActionResult> GetByGroupAndWeek(Guid groupId, string weekStart)
    {
        if (!DateOnly.TryParse(weekStart, out var weekStartDate))
            return BadRequest("Invalid date format. Use YYYY-MM-DD.");

        var schedule = await _scheduleService.GetByGroupAndWeekAsync(groupId, weekStartDate);

        if (schedule == null)
            return NotFound();

        return Ok(schedule);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] Schedule schedule)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var created = await _scheduleService.CreateAsync(schedule);

            return CreatedAtAction(
                nameof(GetById),
                new { id = created.Id },
                created);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ex.Message);
        }
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] Schedule schedule)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var updated = await _scheduleService.UpdateAsync(id, schedule);

            if (!updated)
                return NotFound();

            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var deleted = await _scheduleService.DeleteAsync(id);

        if (!deleted)
            return NotFound();

        return NoContent();
    }
}