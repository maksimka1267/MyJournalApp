namespace MyJournalApp.Data.Models
{
    public class Teacher
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string FullName { get; set; } = null!;
        public string Email { get; set; } = null!;

        public List<Grade> Grades { get; set; } = new();
    }

}
