using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyJournalApp.Data.Models;
using MyJournalApp.Interface;

[ApiController]
[Route("api/[controller]")]
public class ScheduleController : ControllerBase
{
    private readonly IScheduleRepository _scheduleRepo;

    public ScheduleController(IScheduleRepository scheduleRepo)
    {
        _scheduleRepo = scheduleRepo;
    }

    [HttpGet("group/{groupId}")]
    public async Task<IActionResult> GetScheduleForGroup(Guid groupId)
    {
        var schedule = await _scheduleRepo.GetByGroupIdAsync(groupId);
        return Ok(schedule);
    }

    [HttpGet("teacher/{teacherId}")]
    public async Task<IActionResult> GetScheduleForTeacher(Guid teacherId)
    {
        var schedule = await _scheduleRepo.GetByTeacherIdAsync(teacherId);
        return Ok(schedule);
    }

    [Authorize(Roles = "Admin,Teacher")]
    [HttpPost]
    public async Task<IActionResult> AddLesson([FromBody] ScheduleDto dto)
    {
        var lesson = new Schedule
        {
            Id = Guid.NewGuid(),
            GroupId = dto.GroupId,
            Date = dto.Date,
            Subject = dto.Subject,
            TeacherId = dto.TeacherId,
            Room = dto.Room
        };

        await _scheduleRepo.AddAsync(lesson);
        await _scheduleRepo.SaveChangesAsync();

        return Ok(lesson);
    }

    [Authorize(Roles = "Admin,Teacher")]
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateLesson(Guid id, [FromBody] ScheduleDto dto)
    {
        var existing = await _scheduleRepo.GetByIdAsync(id);
        if (existing == null) return NotFound();

        existing.GroupId = dto.GroupId;
        existing.Date = dto.Date;
        existing.Subject = dto.Subject;
        existing.TeacherId = dto.TeacherId;
        existing.Room = dto.Room;

        _scheduleRepo.Update(existing);
        await _scheduleRepo.SaveChangesAsync();

        return Ok(existing);
    }

    [Authorize(Roles = "Admin,Teacher")]
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteLesson(Guid id)
    {
        var lesson = await _scheduleRepo.GetByIdAsync(id);
        if (lesson == null) return NotFound();

        _scheduleRepo.Delete(lesson);
        await _scheduleRepo.SaveChangesAsync();

        return Ok("Deleted");
    }
    public class ScheduleDto
    {
        public Guid GroupId { get; set; }
        public DateTime Date { get; set; }
        public string Subject { get; set; }
        public Guid TeacherId { get; set; }
        public string Room { get; set; }
    }

}
