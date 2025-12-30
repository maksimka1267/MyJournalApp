using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyJournalApp.Interface;

[ApiController]
[Route("api/[controller]")]
public class GradeController : ControllerBase
{
    private readonly IGradeRepository _gradeRepo;
    private readonly IJournalEntryRepository _journalRepo;


    public GradeController(IGradeRepository gradeRepo, IJournalEntryRepository journalRepo)
    {
        _gradeRepo = gradeRepo;
        _journalRepo = journalRepo;
    }


    [Authorize]
    [HttpGet("journal/{journalId}")]
    public async Task<IActionResult> GetByJournal(Guid journalId)
    {
        var grades = await _gradeRepo.GetByJournalEntryIdAsync(journalId);
        return Ok(grades);
    }

    [Authorize]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] Grade grade)
    {
        try
        {
            Console.WriteLine("ID Journal:",grade.JournalEntryId);
            Console.WriteLine("ID Student:",grade.StudentId);
            Console.WriteLine("Value:",grade.Value);
            Console.WriteLine("Comment:",grade.Comment);
            if(grade.Value == 0)
            {
                grade.Value = null;
            }
            await _gradeRepo.AddAsync(grade);
            await _gradeRepo.SaveChangesAsync();
            return Ok(grade);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error saving grade: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
            return StatusCode(500, $"Internal error: {ex.Message}");
        }
    }
    [Authorize]
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] Grade grade)
    {
        var existing = await _gradeRepo.GetByIdAsync(id);
        if (existing == null) return NotFound();

        existing.Value = grade.Value;
        existing.Comment = grade.Comment;
        existing.IsPresent = grade.IsPresent;          // <-- раньше терялось

        await _gradeRepo.Update(existing);
        return Ok(existing);
    }
    // 👇 ДОБАВЬТЕ ЭТОТ НОВЫЙ ЭНДПОИНТ
    [Authorize]
    [HttpGet("journal/{journalId}/date/{date:datetime}")]
    public async Task<IActionResult> GetByJournalAndDate(Guid journalId, DateTime date)
    {
        var grades = await _gradeRepo.GetByJournalAndDateAsync(journalId, date);
        return Ok(grades);
    }
    [Authorize]
    [HttpGet("byStudent/{studentId}")]
    public async Task<IActionResult> GetByStudent(Guid studentId)
    {
        var grades = await _gradeRepo.GetByStudentIdAsync(studentId);
        return Ok(grades);
    }

    [Authorize]
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var grade = await _gradeRepo.GetByIdAsync(id);
        if (grade == null) return NotFound();

        await _gradeRepo.Delete(grade);
        await _gradeRepo.SaveChangesAsync();
        return Ok("Deleted");
    }
}
