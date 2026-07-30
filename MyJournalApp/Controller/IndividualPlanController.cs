using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyJournalApp.Dtos.IndividualPlan;
using MyJournalApp.Interface;
using MyJournalApp.Service.Interface;
using System.Security.Claims;

namespace MyJournalApp.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class IndividualPlanController : ControllerBase
{
    private readonly IIndividualPlanService _individualPlanService;

    public IndividualPlanController(
        IIndividualPlanService individualPlanService)
    {
        _individualPlanService = individualPlanService;
    }

    [HttpGet("me")]
    public async Task<IActionResult> DownloadForMe([FromQuery] int? sem)
    {
        var result = await _individualPlanService.DownloadForMeAsync(
            GetCurrentUserId(),
            sem);

        if (!result.Success)
            return BadRequest(result.Message);

        return File(
            result.Data!.Content,
            result.Data.ContentType,
            result.Data.FileName);
    }

    [HttpGet("student/{studentId:guid}")]
    public async Task<IActionResult> DownloadForStudent(
        Guid studentId,
        [FromQuery] int? sem)
    {
        var dto = new DownloadIndividualPlanRequestDto
        {
            StudentId = studentId,
            Semester = sem
        };

        var result = await _individualPlanService.DownloadForStudentAsync(
            GetCurrentUserId(),
            dto);

        if (!result.Success)
        {
            Console.WriteLine(result.Message);
            return BadRequest(result.Message);
        }
        return File(
            result.Data!.Content,
            result.Data.ContentType,
            result.Data.FileName);
    }

    private Guid GetCurrentUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);

        return Guid.TryParse(value, out var id)
            ? id
            : Guid.Empty;
    }
}