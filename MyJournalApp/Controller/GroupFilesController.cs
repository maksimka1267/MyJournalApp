using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyJournalApp.Data.Dtos.GroupFiles;
using MyJournalApp.Interface;
using MyJournalApp.Service.Interface;

namespace MyJournalApp.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class GroupFilesController : ControllerBase
{
    private readonly IGroupFilesService _groupFilesService;

    public GroupFilesController(IGroupFilesService groupFilesService)
    {
        _groupFilesService = groupFilesService;
    }

    [HttpGet("status/{groupId:guid}")]
    public async Task<IActionResult> GetStatus(Guid groupId)
    {
        var result = await _groupFilesService.GetStatusAsync(groupId);

        if (!result.Success)
            return result.StatusCode switch
            {
                400 => BadRequest(result.Message),
                404 => NotFound(result.Message),
                _ => BadRequest(result.Message)
            };

        return Ok(result.Data);
    }

    [HttpGet("status")]
    public async Task<IActionResult> GetStatusBatch([FromQuery] List<Guid> groupIds)
    {
        var result = await _groupFilesService.GetStatusBatchAsync(groupIds);

        return Ok(result);
    }

    [HttpPost("upload")]
    [RequestSizeLimit(50_000_000)]
    public async Task<IActionResult> Upload(
        [FromForm] UploadGroupFileDto dto)
    {
        var result = await _groupFilesService.UploadAsync(dto);

        if (!result.Success)
            return result.StatusCode switch
            {
                400 => BadRequest(result.Message),
                404 => NotFound(result.Message),
                _ => BadRequest(result.Message)
            };

        return Ok(result.Data);
    }

    [HttpGet("download/{groupId:guid}/{semester:int}")]
    public async Task<IActionResult> Download(
        Guid groupId,
        int semester)
    {
        var result = await _groupFilesService.DownloadAsync(
            groupId,
            semester);

        if (!result.Success)
            return result.StatusCode switch
            {
                400 => BadRequest(result.Message),
                404 => NotFound(result.Message),
                _ => BadRequest(result.Message)
            };

        return File(
            result.Data!.Content,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            result.Data.FileName);
    }

    [HttpDelete("{groupId:guid}/{semester:int}")]
    public async Task<IActionResult> Delete(
        Guid groupId,
        int semester)
    {
        var result = await _groupFilesService.DeleteAsync(
            groupId,
            semester);

        if (!result.Success)
            return result.StatusCode switch
            {
                400 => BadRequest(result.Message),
                404 => NotFound(result.Message),
                _ => BadRequest(result.Message)
            };

        return Ok(result.Message);
    }
}