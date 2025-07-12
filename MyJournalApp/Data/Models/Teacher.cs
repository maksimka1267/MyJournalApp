namespace MyJournalApp.Data.Models
{
    public class Teacher
    {
        public Guid Id { get; set; }
        public List<Guid>? SubjectIds { get; set; }
    }
}
