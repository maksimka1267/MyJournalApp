namespace MyJournalApp.Dtos.Group;

public class GroupInfoDto
{
    public string Name { get; set; } = string.Empty;

    public Guid TeacherId { get; set; }
}