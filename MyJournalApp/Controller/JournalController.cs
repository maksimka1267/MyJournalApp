using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

[ApiController]
[Route("api/[controller]")]
public class JournalController : ControllerBase
{
    private readonly IJournalEntryRepository _journalRepo;
    private readonly IGradeRepository _gradeRepository;
    private readonly IJournalGenerationService _journalGenerationService; // Внедряем новый сервис
    public JournalController(IJournalEntryRepository journalRepo,
                            IGradeRepository gradeRepository,
                            IJournalGenerationService journalGenerationService)
    {
        _journalRepo = journalRepo;
        _gradeRepository = gradeRepository;
        _journalGenerationService = journalGenerationService;
    }

    [Authorize]
    [HttpGet("all")]
    public async Task<IActionResult> GetAll()
    {
        var journals = await _journalRepo.GetAllAsync();
        return Ok(journals);
    }

    [Authorize]
    [HttpGet("my")]
    public async Task<IActionResult> GetMy()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var roles = User.FindAll(ClaimTypes.Role).Select(r => r.Value);

        Console.WriteLine($"➡️ USER ID: {userId}");
        Console.WriteLine($"➡️ ROLES: {string.Join(", ", roles)}");

        var teacherId = Guid.Parse(userId!);
        var journals = await _journalRepo.GetByTeacherIdAsync(teacherId);
        return Ok(journals);
    }
    [Authorize]
    [HttpGet("{id}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var journal = await _journalRepo.GetByIdAsync(id);
        return journal is null ? NotFound() : Ok(journal);
    }

    [Authorize]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] JournalEntry dto)
    {
        dto.Id = Guid.NewGuid();
        await _journalRepo.AddAsync(dto);
        await _journalRepo.SaveChangesAsync();
        return Ok(dto);
    }

    [Authorize]
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] JournalEntry dto)
    {
        var existing = await _journalRepo.GetByIdAsync(id);
        if (existing == null) return NotFound();

        existing.Name = dto.Name;
        existing.Date = dto.Date;
        existing.Comment = dto.Comment;
        existing.GroupId = dto.GroupId;
        existing.TeacherId = dto.TeacherId;

        await _journalRepo.Update(existing);
        await _journalRepo.SaveChangesAsync();
        return Ok(existing);
    }

    [Authorize]
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var existing = await _journalRepo.GetByIdAsync(id);
        if (existing == null) return NotFound();

        await _gradeRepository.DeleteByJournalEntryIdAsync(id);

        await _journalRepo.Delete(existing);
        return NoContent(); // HTTP 204, тело пустое
    }
    [Authorize(Roles = "Admin")] // Рекомендуется защитить этот метод
    [HttpPost("generate-from-schedule")]
    public async Task<IActionResult> GenerateJournals()
    {
        var result = await _journalGenerationService.GenerateJournalsFromScheduleAsync();

        if (!result.Success)
        {
            // Можно вернуть BadRequest, если в сервисе произошла предвиденная ошибка
            return StatusCode(500, result);
        }

        return Ok(result);
    }
    public class JournalEntryDto
    {
        public Guid GroupId { get; set; }
        public DateTime Date { get; set; }
        public string Subject { get; set; }
        public int Grade { get; set; }
        public string Comment { get; set; }
    }

}
