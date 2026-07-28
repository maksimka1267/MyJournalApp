namespace MyJournalApp.Data.Dtos.User;

public class UpdateUserBasicDto
{
    public Guid UserId { get; set; }
    public string? FullName { get; set; }
    public string? Email { get; set; }
}