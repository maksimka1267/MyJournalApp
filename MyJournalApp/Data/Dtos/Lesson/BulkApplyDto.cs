namespace MyJournalApp.Data.Dtos.Lesson
{
    public class BulkApplyDto
    {
        public List<BulkApplyLessonDto> Lessons { get; set; } = new();

        public DateTime StartDate { get; set; }

        public DateTime EndDate { get; set; }
    }
}
