namespace MyJournalApp.Dtos.Group;

public class MoveStudentDto
{
    public Guid StudentId { get; set; }

    public Guid FromGroupId { get; set; }

    public Guid ToGroupId { get; set; }
}