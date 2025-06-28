namespace MyJournalApp.Data.Models
{
    public class Course
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Title { get; set; } = null!;
        public string Description { get; set; }
        public List<Grade> Grades { get; set; } = new();
    }

}
