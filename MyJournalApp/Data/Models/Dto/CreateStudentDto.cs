namespace MyJournalApp.Data.Models.Dto
{
    public class CreateStudentDto
    {
        public string FullName { get; set; } = null!;
        public string Email { get; set; } = null!;
        public Guid GroupId { get; set; }
    }

}
