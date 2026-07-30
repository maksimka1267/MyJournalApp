using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyJournalApp.Service.Interface;
using System.Security.Claims;

[ApiController]
[Route("api/[controller]")]
public class JournalController : ControllerBase
{
    private readonly IJournalService _journalService;
    private readonly IJournalGenerationService _journalGenerationService;

    public JournalController(
        IJournalService journalService,
        IJournalGenerationService journalGenerationService)
    {
        _journalService = journalService;
        _journalGenerationService = journalGenerationService;
    }

    [Authorize]
    [HttpGet("all")]
    public async Task<IActionResult> GetAll()
    {
        var journals = await _journalService.GetAllAsync();
        return Ok(journals);
    }
    [Authorize(Roles = "Teacher")]
    [HttpGet("director/{teacherId:guid}")]
    public async Task<IActionResult> GetTeacherJournalsForDirector(Guid teacherId)
    {
        var currentTeacherId = Guid.Parse(
            User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var isDirector = await _journalService.IsDirectorAsync(currentTeacherId);

        if (!isDirector)
            return Forbid();

        var journals = await _journalService.GetTeacherJournalsAsync(teacherId);

        return Ok(journals);
    }
    [Authorize]
    [HttpGet("my")]
    public async Task<IActionResult> GetMy()
    {
        var teacherId = Guid.Parse(
            User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var journals = await _journalService.GetTeacherJournalsAsync(teacherId);

        return Ok(journals);
    }

    [Authorize]
    [HttpGet("{id}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var journal = await _journalService.GetByIdAsync(id);

        if (journal == null)
            return NotFound();

        return Ok(journal);
    }

    [Authorize]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] JournalEntry journal)
    {
        var result = await _journalService.CreateAsync(journal);

        if (!result.Success)
            return BadRequest(result.Message);

        return Ok(result.Data);
    }

    [Authorize]
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] JournalEntry journal)
    {
        var result = await _journalService.UpdateAsync(id, journal);

        if (!result.Success)
            return NotFound(result.Message);

        return Ok(result.Data);
    }

    [Authorize]
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var result = await _journalService.DeleteAsync(id);

        if (!result.Success)
            return NotFound(result.Message);

        return NoContent();
    }

    [Authorize(Roles = "Admin")]
    [HttpPost("generate-from-schedule")]
    public async Task<IActionResult> GenerateJournals()
    {
        var result = await _journalGenerationService.GenerateJournalsFromScheduleAsync();

        if (!result.Success)
            return StatusCode(500, result);

        return Ok(result);
    }
}