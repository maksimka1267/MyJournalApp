using MyJournalApp.Data.Dtos.GroupFiles;
using MyJournalApp.Interface;
using MyJournalApp.Result;
using MyJournalApp.Service.Interface;
using System.Text.RegularExpressions;

public class GroupFilesService : IGroupFilesService
{
    private readonly IGroupRepository _groupRepository;
    private readonly IWebHostEnvironment _environment;

    private const string FolderName = "group-files";

    public GroupFilesService(
        IGroupRepository groupRepository,
        IWebHostEnvironment environment)
    {
        _groupRepository = groupRepository;
        _environment = environment;
    }

    public async Task<ServiceResult<GroupFilesStatusDto>> GetStatusAsync(Guid groupId)
    {
        if (groupId == Guid.Empty)
            return ServiceResult<GroupFilesStatusDto>.Fail("Invalid group id.");

        var group = await GetGroupAsync(groupId);

        if (group == null)
            return ServiceResult<GroupFilesStatusDto>.Fail("Групу не знайдено.");

        var sem1Path = BuildFilePath(group.Name, 1);
        var sem2Path = BuildFilePath(group.Name, 2);

        var result = new GroupFilesStatusDto
        {
            GroupId = group.Id,
            GroupName = group.Name,
            Sem1Exists = File.Exists(sem1Path),
            Sem2Exists = File.Exists(sem2Path)
        };

        return ServiceResult<GroupFilesStatusDto>.Ok(result);
    }

    public async Task<IEnumerable<GroupFilesStatusDto>> GetStatusBatchAsync(IEnumerable<Guid> groupIds)
    {
        var result = new List<GroupFilesStatusDto>();

        if (groupIds == null)
            return result;

        foreach (var groupId in groupIds.Distinct())
        {
            var status = await GetStatusAsync(groupId);

            if (status.Success && status.Data != null)
                result.Add(status.Data);
        }

        return result;
    }

    public async Task<ServiceResult<UploadGroupFileResultDto>> UploadAsync(
    UploadGroupFileDto dto)
    {
        if (dto.GroupId == Guid.Empty)
            return ServiceResult<UploadGroupFileResultDto>.Fail("groupId is required.");

        if (dto.File == null || dto.File.Length == 0)
            return ServiceResult<UploadGroupFileResultDto>.Fail("File is empty.");

        if (!IsValidSemester(dto.Semester))
            return ServiceResult<UploadGroupFileResultDto>.Fail("Semester must be 1 or 2.");

        if (!dto.File.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
            return ServiceResult<UploadGroupFileResultDto>.Fail("Допускаються лише .xlsx файли.");

        var group = await GetGroupAsync(dto.GroupId);

        if (group == null)
            return ServiceResult<UploadGroupFileResultDto>.Fail("Групу не знайдено.");

        var uploadsDir = GetUploadsDirectory();

        Directory.CreateDirectory(uploadsDir);

        var targetPath = BuildFilePath(group.Name, dto.Semester);

        if (File.Exists(targetPath))
            File.Delete(targetPath);

        using (var stream = File.Create(targetPath))
        {
            await dto.File.CopyToAsync(stream);
        }

        return ServiceResult<UploadGroupFileResultDto>.Ok(
            new UploadGroupFileResultDto
            {
                Message = "Файл збережено.",
                Url = BuildPublicUrl(group.Name, dto.Semester)
            });
    }

    public async Task<ServiceResult<FileDownloadDto>> DownloadAsync(
    Guid groupId,
    int semester)
    {
        if (groupId == Guid.Empty || !IsValidSemester(semester))
            return ServiceResult<FileDownloadDto>.Fail("Invalid request.");

        var group = await GetGroupAsync(groupId);

        if (group == null)
            return ServiceResult<FileDownloadDto>.Fail("Групу не знайдено.");

        var path = BuildFilePath(group.Name, semester);

        if (!File.Exists(path))
            return ServiceResult<FileDownloadDto>.Fail("Файл для цієї групи та семестру відсутній.");

        var bytes = await File.ReadAllBytesAsync(path);

        return ServiceResult<FileDownloadDto>.Ok(
            new FileDownloadDto
            {
                Content = bytes,
                ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                FileName = $"{SanitizeFileName(group.Name)}_sem{semester}.xlsx"
            });
    }
    public async Task<IServiceResult> DeleteAsync(
    Guid groupId,
    int semester)
    {
        if (groupId == Guid.Empty || !IsValidSemester(semester))
            return IServiceResult.Fail("Invalid request.");

        var group = await GetGroupAsync(groupId);

        if (group == null)
            return IServiceResult.Fail("Групу не знайдено.");

        var path = BuildFilePath(group.Name, semester);

        if (!File.Exists(path))
            return IServiceResult.Fail("Файл для цієї групи та семестру відсутній.");

        File.Delete(path);

        return IServiceResult.Ok("Файл видалено.");
    }
    private async Task<Group?> GetGroupAsync(Guid groupId)
    {
        return await _groupRepository.GetByIdAsync(groupId);
    }

    private string GetUploadsDirectory()
    {
        return Path.Combine(
            _environment.WebRootPath ?? _environment.ContentRootPath,
            FolderName);
    }

    private string BuildFilePath(string groupName, int semester)
    {
        return Path.Combine(
            GetUploadsDirectory(),
            $"{SanitizeFileName(groupName)}_sem{semester}.xlsx");
    }

    private string BuildPublicUrl(string groupName, int semester)
    {
        var fileName = $"{SanitizeFileName(groupName)}_sem{semester}.xlsx";

        return $"/{FolderName}/{Uri.EscapeDataString(fileName)}";
    }

    private static bool IsValidSemester(int semester)
    {
        return semester == 1 || semester == 2;
    }

    private static string SanitizeFileName(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return "group";

        var cleaned = Regex.Replace(
            input.Trim(),
            @"[^0-9A-Za-zА-Яа-яІіЇїЄєҐґ _\-]",
            "_");

        cleaned = Regex.Replace(cleaned, @"\s+", " ");

        return cleaned;
    }
}