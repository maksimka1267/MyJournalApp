namespace MyJournalApp.Data.Dtos.User;

public class UpdateTeacherAdminDto
{
    public Guid TeacherId { get; set; }
    public bool IsAdmin { get; set; }
}