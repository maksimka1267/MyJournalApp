namespace MyJournalApp.Data.Models.Dto
{
    public class StudentDto
    {
        public Guid Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? GroupName { get; set; }
    }

}
