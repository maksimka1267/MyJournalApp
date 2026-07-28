using MyJournalApp.Data.Models;

public class BulkImportContext
{
    public Dictionary<string, Group> GroupsByName { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, User> TeachersByShortName { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<Guid, Teacher> TeachersById { get; set; } = new();

    public List<Group> CreatedGroups { get; set; } = new();

    public List<Group> UpdatedGroups { get; set; } = new();

    public List<string> SkippedGroups { get; set; } = new();

    public List<Teacher> UpdatedTeachers { get; set; } = new();

    public List<(string Group, string TeacherRaw)> MissingTeachers { get; set; } = new();
}