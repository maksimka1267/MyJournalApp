namespace MyJournalApp.Data.Dtos.User
{
    public class UpdateTeacherDirectorDto
    {
        public Guid TeacherId { get; set; }
        public bool IsDirector { get; set; }
    }
}
