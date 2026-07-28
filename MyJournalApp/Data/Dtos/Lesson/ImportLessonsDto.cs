namespace MyJournalApp.Data.Dtos.Lesson
{
    public class ImportLessonsDto
    {
        public IFormFile File { get; set; } = null!;

        public Guid GroupId { get; set; }

        public bool IsNumerator { get; set; }

        public DateTime StartDate { get; set; }

        public DateTime EndDate { get; set; }
    }
}
