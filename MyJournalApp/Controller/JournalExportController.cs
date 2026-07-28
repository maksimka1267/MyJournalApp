using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyJournalApp.Data.Dtos.Journal;
using MyJournalApp.Service.Interface;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class JournalExportController : ControllerBase
{
    private readonly IJournalExportService _journalExportService;

    public JournalExportController(
        IJournalExportService journalExportService)
    {
        _journalExportService = journalExportService;
    }

    [HttpGet("{journalId:guid}")]
    public async Task<IActionResult> Export(Guid journalId)
    {
        var result = await _journalExportService.ExportAsync(journalId);

        if (!result.Success)
        {
            if (result.Message == "Журнал не знайдено.")
                return NotFound(result.Message);

            return BadRequest(result.Message);
        }

        return File(
            result.Data!.FileBytes,
            result.Data.ContentType,
            result.Data.FileName);
    }
    [HttpPost("semester/journals")]
    public async Task<IActionResult> GetSemesterJournalList(
    [FromBody] ExportSemesterRequestDto dto)
    {
        var result = await _journalExportService.GetJournalsAsync(dto);

        if (!result.Success)
            return BadRequest(result.Message);

        return Ok(result.Data);
    }
    [HttpPost("semester")]
    public async Task<IActionResult> ExportSemester(
    [FromBody] ExportSemesterRequestDto dto)
    {
        var result = await _journalExportService.ExportSemesterAsync(dto);

        if (!result.Success)
            return BadRequest(result.Message);

        return File(
            result.Data!.FileBytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            result.Data.FileName);
    }
}