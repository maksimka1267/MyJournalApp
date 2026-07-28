using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyJournalApp.Data.Dtos.Absence;
using MyJournalApp.Service.Interface;

[ApiController]
[Route("api/[controller]")]
public class ReportController : ControllerBase
{
    private readonly IReportService _reportService;

    public ReportController(IReportService reportService)
    {
        _reportService = reportService;
    }

    [Authorize]
    [HttpGet("absences/group/{groupId}")]
    public async Task<IActionResult> GenerateAbsenceReport(
        Guid groupId,
        [FromQuery] DateTime startDate,
        [FromQuery] DateTime endDate)
    {
        try
        {
            var file = await _reportService.GenerateAbsenceReportAsync(
                new AbsenceReportDto
                {
                    GroupId = groupId,
                    StartDate = startDate,
                    EndDate = endDate
                });

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
}