namespace MyJournalApp.Dtos.Group;

public class BulkGroupImportResultDto
{
    public string Message { get; set; } = string.Empty;

    public List<GroupInfoDto> Created { get; set; } = [];

    public List<string> SkippedExisting { get; set; } = [];

    public List<GroupInfoDto> UpdatedTeacher { get; set; } = [];

    public List<MissingTeacherDto> MissingTeachers { get; set; } = [];
}