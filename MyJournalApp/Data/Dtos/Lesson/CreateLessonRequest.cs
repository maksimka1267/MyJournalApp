namespace MyJournalApp.Data.Dtos.Lesson
{
    public class CreateLessonRequest
    {
        public Guid Id { get; set; }

        public Guid GroupId { get; set; }

        public Guid TeacherId { get; set; }

        public Guid? SecondTeacherId { get; set; }

        public string Name { get; set; } = "";

        public DateTime StartTime { get; set; }

        public string? Topic { get; set; }

        public string? Homework { get; set; }

        public int? Clocks { get; set; }

        public bool RepeatWeekly { get; set; }

        public DateTime? EndDate { get; set; }

        public int ForNumerator { get; set; }

        public int ForDenominator { get; set; }
    }
}
