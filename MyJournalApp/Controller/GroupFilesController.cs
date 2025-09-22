using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyJournalApp.Interface; // IGroupRepository
using System.Text.RegularExpressions;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class GroupFilesController : ControllerBase
{
    private readonly IGroupRepository _groupRepo;
    private readonly IWebHostEnvironment _env;

    // папка для хранения файлов
    private const string FolderName = "group-files";

    public GroupFilesController(IGroupRepository groupRepo, IWebHostEnvironment env)
    {
        _groupRepo = groupRepo;
        _env = env;
    }

    [HttpPost("upload")]
    [RequestSizeLimit(50_000_000)] // 50 MB, при необходимости скорректируй
    public async Task<IActionResult> Upload([FromForm] Guid groupId, [FromForm] IFormFile file)
    {
        if (groupId == Guid.Empty) return BadRequest("groupId is required.");
        if (file == null || file.Length == 0) return BadRequest("File is empty.");
        if (!file.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
            return BadRequest("Допускаються лише .xlsx файли.");

        var group = await _groupRepo.GetByIdAsync(groupId);
        if (group == null) return NotFound("Групу не знайдено.");

        // Имя файла = имя группы.xlsx
        var safeGroupName = SanitizeFileName(group.Name);
        var uploadsDir = Path.Combine(_env.WebRootPath ?? _env.ContentRootPath, FolderName);
        Directory.CreateDirectory(uploadsDir);

        var targetPath = Path.Combine(uploadsDir, $"{safeGroupName}.xlsx");

        // Если такой файл уже есть — удаляем
        if (System.IO.File.Exists(targetPath))
            System.IO.File.Delete(targetPath);

        // Сохраняем новый
        using (var stream = System.IO.File.Create(targetPath))
        {
            await file.CopyToAsync(stream);
        }

        // Вернём ссылку на скачивание
        var publicUrl = $"/{FolderName}/{Uri.EscapeDataString($"{safeGroupName}.xlsx")}";
        return Ok(new { message = "Файл збережено.", url = publicUrl });
    }

    // Скачать по groupId (вернёт 404, если не найдено)
    [HttpGet("download/{groupId:guid}")]
    public async Task<IActionResult> Download(Guid groupId)
    {
        if (groupId == Guid.Empty) return BadRequest();

        var group = await _groupRepo.GetByIdAsync(groupId);
        if (group == null) return NotFound("Групу не знайдено.");

        var safeGroupName = SanitizeFileName(group.Name);
        var uploadsDir = Path.Combine(_env.WebRootPath ?? _env.ContentRootPath, FolderName);
        var path = Path.Combine(uploadsDir, $"{safeGroupName}.xlsx");

        if (!System.IO.File.Exists(path)) return NotFound("Файл для цієї групи відсутній.");

        var bytes = await System.IO.File.ReadAllBytesAsync(path);
        var fileName = $"{safeGroupName}.xlsx";
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }

    // Удалить файл группы
    [HttpDelete("{groupId:guid}")]
    public async Task<IActionResult> Delete(Guid groupId)
    {
        if (groupId == Guid.Empty) return BadRequest();

        var group = await _groupRepo.GetByIdAsync(groupId);
        if (group == null) return NotFound("Групу не знайдено.");

        var safeGroupName = SanitizeFileName(group.Name);
        var uploadsDir = Path.Combine(_env.WebRootPath ?? _env.ContentRootPath, FolderName);
        var path = Path.Combine(uploadsDir, $"{safeGroupName}.xlsx");

        if (System.IO.File.Exists(path))
        {
            System.IO.File.Delete(path);
            return Ok(new { message = "Файл видалено." });
        }

        return NotFound("Файл для цієї групи відсутній.");
    }

    private static string SanitizeFileName(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return "group";
        // допустим буквы/цифры/пробел/дефис/нижнее подчёркивание, остальное заменим на "_"
        var cleaned = Regex.Replace(input.Trim(), @"[^0-9A-Za-zА-Яа-яІіЇїЄєҐґ _\-]", "_");
        // уберём двойные пробелы/подчёркивания
        cleaned = Regex.Replace(cleaned, @"\s+", " ");
        return cleaned;
    }
}
