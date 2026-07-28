using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyJournalApp.Service.Interface;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class StudentGradesReportController : ControllerBase
{
    private readonly IStudentGradesReportService _reportService;
    private readonly IStudentGradesExportService _exportService;

    public StudentGradesReportController(
        IStudentGradesReportService reportService,
        IStudentGradesExportService exportService)
    {
        _reportService = reportService;
        _exportService = exportService;
    }

    [HttpGet("student-grades")]
    public async Task<IActionResult> GetStudentGrades(
        [FromQuery] Guid studentId,
        [FromQuery] DateTime start,
        [FromQuery] DateTime end)
    {
        try
        {
            var report = await _reportService.BuildReportAsync(
                studentId,
                start,
                end);

            return Ok(report);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }

    [HttpGet("student-grades/export")]
    public async Task<IActionResult> ExportStudentGradesExcel(
        [FromQuery] Guid studentId,
        [FromQuery] DateTime start,
        [FromQuery] DateTime end)
    {
        try
        {
            var file = await _exportService.ExportAsync(
                studentId,
                start,
                end);

            return File(
                file.Content,
                file.ContentType,
                file.FileName);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }
}