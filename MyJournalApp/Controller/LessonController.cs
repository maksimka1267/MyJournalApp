using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyJournalApp.Data.Dtos.Lesson;
using MyJournalApp.Service.Interface;

[ApiController]
[Route("api/[controller]")]
public class LessonController : ControllerBase
{
    private readonly ILessonService _lessonService;
    private readonly ILessonImportService _lessonImportService;
    private readonly ILessonExportService _lessonExportService;
    private readonly ILessonBulkService _lessonBulkService;

    public LessonController(
        ILessonService lessonService,
        ILessonImportService lessonImportService,
        ILessonExportService lessonExportService,
        ILessonBulkService lessonBulkService)
    {
        _lessonService = lessonService;
        _lessonImportService = lessonImportService;
        _lessonExportService = lessonExportService;
        _lessonBulkService = lessonBulkService;
    }
    [Authorize]
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        return Ok(await _lessonService.GetAllAsync());
    }

    [Authorize]
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var lesson = await _lessonService.GetByIdAsync(id);

        if (lesson == null)
            return NotFound();

        return Ok(lesson);
    }

    [Authorize]
    [HttpGet("group/{groupId}")]
    public async Task<IActionResult> GetByGroup(Guid groupId)
    {
        return Ok(await _lessonService.GetByGroupAsync(groupId));
    }

    [Authorize]
    [HttpGet("group/{groupId}/date/{date}")]
    public async Task<IActionResult> GetByGroupAndDate(Guid groupId, DateTime date)
    {
        return Ok(await _lessonService.GetByGroupAndDateAsync(groupId, date));
    }
    [Authorize]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateLessonRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var lesson = await _lessonService.CreateAsync(request);

            return CreatedAtAction(
                nameof(GetById),
                new { id = lesson.Id },
                lesson);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }
    [Authorize]
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] Lesson lesson)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var result = await _lessonService.UpdateAsync(id, lesson);

        if (!result)
            return NotFound();

        return NoContent();
    }
    [Authorize]
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var result = await _lessonService.DeleteAsync(id);

        if (!result)
            return NotFound();

        return NoContent();
    }
    [Authorize(Roles = "Admin")]
    [HttpGet("group/{groupId}/subjects")]
    public async Task<IActionResult> GetSubjectsByGroup(Guid groupId)
    {
        return Ok(await _lessonService.GetSubjectsByGroupAsync(groupId));
    }
    [Authorize]
    [HttpPost("import")]
    public async Task<IActionResult> ImportLessons([FromForm] ImportLessonsDto dto)
    {
        try
        {
            var result = await _lessonImportService.ImportAsync(dto);

            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }
    [Authorize]
    [HttpGet("export")]
    public async Task<IActionResult> Export([FromQuery] ExportDto dto)
    {
        try
        {
            var file = await _lessonExportService.ExportAsync(dto);

            return File(
                file.Content,
                file.ContentType,
                file.FileName);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }
    [Authorize(Roles = "Admin")]
    [HttpPost("bulk-apply")]
    public async Task<IActionResult> BulkApply([FromBody] BulkApplyDto dto)
    {
        try
        {
            var result = await _lessonBulkService.BulkApplyAsync(dto);

            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }
    [Authorize(Roles = "Admin")]
    [HttpPost("export/semester")]
    public async Task<IActionResult> ExportSemester(
    [FromBody] ExportSemesterLessonsDto dto)
    {
        try
        {
            var result = await _lessonExportService.ExportSemesterAsync(dto);
            Console.WriteLine("ExportSemester called");

            return File(
                result.Content,
                result.ContentType,
                result.FileName);
        }
        catch (ArgumentException ex)
        {
            Console.WriteLine(ex.Message);
            return BadRequest(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            Console.WriteLine(ex.Message);
            return BadRequest(ex.Message);
        }
    }
}