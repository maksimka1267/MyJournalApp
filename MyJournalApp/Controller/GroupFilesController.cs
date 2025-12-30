using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyJournalApp.Interface;
using System.Text.RegularExpressions;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class GroupFilesController : ControllerBase
{
    private readonly IGroupRepository _groupRepo;
    private readonly IWebHostEnvironment _env;

    private const string FolderName = "group-files";

    public GroupFilesController(IGroupRepository groupRepo, IWebHostEnvironment env)
    {
        _groupRepo = groupRepo;
        _env = env;
    }

    // ---------- STATUS (one) ----------
    // GET: api/GroupFiles/status/{groupId}
    [HttpGet("status/{groupId:guid}")]
    public async Task<IActionResult> GetStatus(Guid groupId)
    {
        if (groupId == Guid.Empty) return BadRequest();

        var group = await _groupRepo.GetByIdAsync(groupId);
        if (group == null) return NotFound("Групу не знайдено.");

        var safe = SanitizeFileName(group.Name);
        var dir = Path.Combine(_env.WebRootPath ?? _env.ContentRootPath, FolderName);

        var p1 = Path.Combine(dir, $"{safe}_sem1.xlsx");
        var p2 = Path.Combine(dir, $"{safe}_sem2.xlsx");

        var res = new GroupFilesStatusDto
        {
            GroupId = groupId,
            GroupName = group.Name,
            Sem1Exists = System.IO.File.Exists(p1),
            Sem2Exists = System.IO.File.Exists(p2)
        };
        return Ok(res);
    }

    // ---------- STATUS (batch) ----------
    // GET: api/GroupFiles/status?groupIds=guid1&groupIds=guid2...
    [HttpGet("status")]
    public async Task<IActionResult> GetStatusBatch([FromQuery] List<Guid> groupIds)
    {
        if (groupIds == null || groupIds.Count == 0) return Ok(Array.Empty<GroupFilesStatusDto>());

        var result = new List<GroupFilesStatusDto>();
        foreach (var id in groupIds.Distinct())
        {
            var group = await _groupRepo.GetByIdAsync(id);
            if (group == null) continue;

            var safe = SanitizeFileName(group.Name);
            var dir = Path.Combine(_env.WebRootPath ?? _env.ContentRootPath, FolderName);

            var p1 = Path.Combine(dir, $"{safe}_sem1.xlsx");
            var p2 = Path.Combine(dir, $"{safe}_sem2.xlsx");

            result.Add(new GroupFilesStatusDto
            {
                GroupId = id,
                GroupName = group.Name,
                Sem1Exists = System.IO.File.Exists(p1),
                Sem2Exists = System.IO.File.Exists(p2)
            });
        }
        return Ok(result);
    }

    // ---------- Upload ----------
    [HttpPost("upload")]
    [RequestSizeLimit(50_000_000)]
    public async Task<IActionResult> Upload([FromForm] Guid groupId, [FromForm] IFormFile file, [FromForm] int semester)
    {
        if (groupId == Guid.Empty) return BadRequest("groupId is required.");
        if (file == null || file.Length == 0) return BadRequest("File is empty.");
        if (semester != 1 && semester != 2) return BadRequest("Semester must be 1 or 2.");
        if (!file.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
            return BadRequest("Допускаються лише .xlsx файли.");

        var group = await _groupRepo.GetByIdAsync(groupId);
        if (group == null) return NotFound("Групу не знайдено.");

        var safeGroupName = SanitizeFileName(group.Name);
        var uploadsDir = Path.Combine(_env.WebRootPath ?? _env.ContentRootPath, FolderName);
        Directory.CreateDirectory(uploadsDir);

        var targetPath = Path.Combine(uploadsDir, $"{safeGroupName}_sem{semester}.xlsx");

        if (System.IO.File.Exists(targetPath))
            System.IO.File.Delete(targetPath);

        using (var stream = System.IO.File.Create(targetPath))
            await file.CopyToAsync(stream);

        var publicUrl = $"/{FolderName}/{Uri.EscapeDataString($"{safeGroupName}_sem{semester}.xlsx")}";
        return Ok(new { message = "Файл збережено.", url = publicUrl });
    }

    // ---------- Download ----------
    [HttpGet("download/{groupId:guid}/{semester:int}")]
    public async Task<IActionResult> Download(Guid groupId, int semester)
    {
        if (groupId == Guid.Empty || (semester != 1 && semester != 2))
            return BadRequest();

        var group = await _groupRepo.GetByIdAsync(groupId);
        if (group == null) return NotFound("Групу не знайдено.");

        var safeGroupName = SanitizeFileName(group.Name);
        var uploadsDir = Path.Combine(_env.WebRootPath ?? _env.ContentRootPath, FolderName);
        var path = Path.Combine(uploadsDir, $"{safeGroupName}_sem{semester}.xlsx");

        if (!System.IO.File.Exists(path))
            return NotFound("Файл для цієї групи та семестру відсутній.");

        var bytes = await System.IO.File.ReadAllBytesAsync(path);
        var fileName = $"{safeGroupName}_sem{semester}.xlsx";
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }

    // ---------- Delete ----------
    [HttpDelete("{groupId:guid}/{semester:int}")]
    public async Task<IActionResult> Delete(Guid groupId, int semester)
    {
        if (groupId == Guid.Empty || (semester != 1 && semester != 2))
            return BadRequest();

        var group = await _groupRepo.GetByIdAsync(groupId);
        if (group == null) return NotFound("Групу не знайдено.");

        var safeGroupName = SanitizeFileName(group.Name);
        var uploadsDir = Path.Combine(_env.WebRootPath ?? _env.ContentRootPath, FolderName);
        var path = Path.Combine(uploadsDir, $"{safeGroupName}_sem{semester}.xlsx");

        if (System.IO.File.Exists(path))
        {
            System.IO.File.Delete(path);
            return Ok(new { message = "Файл видалено." });
        }

        return NotFound("Файл для цієї групи та семестру відсутній.");
    }

    private static string SanitizeFileName(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return "group";
        var cleaned = Regex.Replace(input.Trim(), @"[^0-9A-Za-zА-Яа-яІіЇїЄєҐґ _\-]", "_");
        cleaned = Regex.Replace(cleaned, @"\s+", " ");
        return cleaned;
    }
}

public class GroupFilesStatusDto
{
    public Guid GroupId { get; set; }
    public string GroupName { get; set; } = "";
    public bool Sem1Exists { get; set; }
    public bool Sem2Exists { get; set; }
    public int Count => (Sem1Exists ? 1 : 0) + (Sem2Exists ? 1 : 0);
}
