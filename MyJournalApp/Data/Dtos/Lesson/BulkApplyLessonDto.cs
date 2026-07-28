namespace MyJournalApp.Data.Dtos.Lesson
{
    public class BulkApplyLessonDto
    {
        public Guid Id { get; set; }

        public Guid GroupId { get; set; }

        public DateTime StartTime { get; set; }

        public string? Name { get; set; }

        public string? Topic { get; set; }

        public string? Homework { get; set; }

        public int? Clocks { get; set; }

        public Guid TeacherId { get; set; }

        public Guid? SecondTeacherId { get; set; }

        public bool Delete { get; set; }
    }
}
