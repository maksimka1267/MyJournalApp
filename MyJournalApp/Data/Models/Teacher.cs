namespace MyJournalApp.Data.Models
{
    public class Teacher:Client
    {
        public List<Guid> Grades { get; set; } = new();
    }

}
