using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyJournalApp.Dtos.Group;
using System.Security.Claims;

namespace MyJournalApp.Controllers;

[ApiController]
[Route("api/[controller]")]
public class GroupController : ControllerBase
{
    private readonly IGroupService _groupService;

    public GroupController(IGroupService groupService)
    {
        _groupService = groupService;
    }

    [Authorize]
    [HttpGet("all")]
    public async Task<IActionResult> GetAll()
    {
        var groups = await _groupService.GetAllAsync();
        return Ok(groups);
    }

    [Authorize]
    [HttpGet("curated-by/me")]
    public async Task<IActionResult> GetCuratedGroups()
    {
        var teacherId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var groups = await _groupService.GetTeacherGroupsAsync(teacherId);

        return Ok(groups);
    }

    [Authorize]
    [HttpGet("my")]
    public async Task<IActionResult> GetMyGroups()
    {
        var teacherId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var groups = await _groupService.GetTeacherGroupsAsync(teacherId);

        return Ok(groups);
    }

    [Authorize]
    [HttpGet("student")]
    public async Task<IActionResult> GetStudentGroup()
    {
        var studentId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var group = await _groupService.GetStudentGroupAsync(studentId);

        if (group == null)
            return NotFound();

        return Ok(new[] { group });
    }

    [Authorize]
    [HttpGet("{id}/users")]
    public async Task<IActionResult> GetUsersByGroupId(Guid id)
    {
        var users = await _groupService.GetUsersByGroupAsync(id);
        return Ok(users);
    }

    [Authorize]
    [HttpGet("{id}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var group = await _groupService.GetByIdAsync(id);

        if (group == null)
            return NotFound();

        return Ok(group);
    }
    [Authorize]
    [HttpPut("move-student")]
    public async Task<IActionResult> MoveStudent([FromBody] MoveStudentDto dto)
    {
        var result = await _groupService.MoveStudentAsync(dto);

        if (!result.Success)
            return BadRequest(result.Message);

        return Ok(result.Message);
    }
    [Authorize]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] Group group)
    {
        var result = await _groupService.CreateAsync(group);

        if (!result.Success)
            return BadRequest(result.Message);

        return Ok(result.Data);
    }

    [Authorize]
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] Group group)
    {
        var result = await _groupService.UpdateAsync(id, group);

        if (!result.Success)
        {
            if (result.Message == "Group not found")
                return NotFound(result.Message);

            return BadRequest(result.Message);
        }

        return Ok(result.Data);
    }

    [Authorize]
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var result = await _groupService.DeleteAsync(id);

        if (!result.Success)
        {
            if (result.Message == "Group not found")
                return NotFound(result.Message);

            return BadRequest(result.Message);
        }

        return Ok(result.Message);
    }

    [Authorize]
    [HttpPost("bulk-import")]
    public async Task<IActionResult> BulkImportGroups([FromForm] BulkGroupImportDto dto)
    {
        var result = await _groupService.BulkImportAsync(dto);

        if (!result.Success)
            return BadRequest(result.Message);

        return Ok(result.Data);
    }
}