namespace MyJournalApp.Data.Dtos.Lesson
{
    public class ExportDto
    {
        public Guid? GroupId { get; set; }

        public Guid? TeacherId { get; set; }

        public string? SubjectName { get; set; }

        public DateTime? StartDate { get; set; }

        public DateTime? EndDate { get; set; }
    }
}
