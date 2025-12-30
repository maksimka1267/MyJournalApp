using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/[controller]")]
public class ScheduleController : ControllerBase
{
    private readonly IScheduleRepository _scheduleRepository;
    private readonly ILessonRepository _lessonRepository;

    public ScheduleController(IScheduleRepository scheduleRepository, ILessonRepository lessonRepository)
    {
        _scheduleRepository = scheduleRepository;
        _lessonRepository = lessonRepository;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var schedules = await _scheduleRepository.GetAllAsync();
        return Ok(schedules);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var schedule = await _scheduleRepository.GetByIdAsync(id);
        if (schedule == null) return NotFound();
        return Ok(schedule);
    }

    [HttpGet("group/{groupId}/week/{weekStart}")]
    public async Task<IActionResult> GetByGroupAndWeek(Guid groupId, string weekStart)
    {
        if (!DateOnly.TryParse(weekStart, out var weekStartDate))
            return BadRequest("Invalid date format. Use YYYY-MM-DD.");

        var schedule = await _scheduleRepository.GetByGroupAndWeekAsync(groupId, weekStartDate);
        if (schedule == null) return NotFound();

        return Ok(schedule);
    }

    [HttpPost]
    public async Task<IActionResult> Create(Schedule schedule)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            // Проверка, не существует ли уже расписание
            var existing = await _scheduleRepository.GetByGroupAndWeekAsync(schedule.GroupId, schedule.WeekStartDate);
            if (existing != null)
                return Conflict("Schedule already exists for this group and week.");

            // Проверка всех уроков
            foreach (var lessonId in schedule.Lessons)
            {
                var lesson = await _lessonRepository.GetByIdAsync(lessonId);
                if (lesson == null)
                    return BadRequest($"Lesson with ID {lessonId} does not exist.");
            }

            await _scheduleRepository.AddAsync(schedule);
            await _scheduleRepository.SaveChangesAsync();

            return CreatedAtAction(nameof(GetById), new { id = schedule.Id }, schedule);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Server error: {ex.Message}");
        }
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, Schedule updated)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var existing = await _scheduleRepository.GetByIdAsync(id);
            if (existing == null) return NotFound();

            // Проверка всех уроков
            foreach (var lessonId in updated.Lessons)
            {
                var lesson = await _lessonRepository.GetByIdAsync(lessonId);
                if (lesson == null)
                    return BadRequest($"Lesson with ID {lessonId} does not exist.");
            }

            existing.GroupId = updated.GroupId;
            existing.WeekStartDate = updated.WeekStartDate;
            existing.Lessons = updated.Lessons;

            await _scheduleRepository.Update(existing);
            await _scheduleRepository.SaveChangesAsync();

            return NoContent();
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Server error: {ex.Message}");
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        try
        {
            var existing = await _scheduleRepository.GetByIdAsync(id);
            if (existing == null) return NotFound();

            await _scheduleRepository.Delete(existing);
            await _scheduleRepository.SaveChangesAsync();

            return NoContent();
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Server error: {ex.Message}");
        }
    }
}
