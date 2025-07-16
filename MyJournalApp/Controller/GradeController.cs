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
    public async Task<IActionResult> Create([FromForm] Grade grade)
    {
        grade.Id = Guid.NewGuid();

        var journal = await _journalRepo.GetByIdAsync(grade.JournalEntryId);
        if (journal == null)
            return NotFound("Journal entry not found");

        journal.Grades.Add(grade); // связь

        _journalRepo.Update(journal); // можно, можно и _gradeRepo.AddAsync(grade)
        await _journalRepo.SaveChangesAsync();

        return Ok(grade);
    }

    [Authorize]
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] Grade grade)
    {
        var existing = await _gradeRepo.GetByIdAsync(id);
        if (existing == null) return NotFound();

        existing.Value = grade.Value;
        existing.Comment = grade.Comment;

        _gradeRepo.Update(existing);
        await _gradeRepo.SaveChangesAsync();
        return Ok(existing);
    }

    [Authorize]
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var grade = await _gradeRepo.GetByIdAsync(id);
        if (grade == null) return NotFound();

        _gradeRepo.Delete(grade);
        await _gradeRepo.SaveChangesAsync();
        return Ok("Deleted");
    }
}
