namespace MyJournalApp.Data.Models
{
    public class Grade
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public int Value { get; set; }
        public DateTime Date { get; set; }

        public Guid StudentId { get; set; }
        public Student Student { get; set; } = null!;

        public Guid TeacherId { get; set; }
        public Teacher Teacher { get; set; } = null!;

        public Guid CourseId { get; set; }
        public Course Course { get; set; } = null!;
    }

}
