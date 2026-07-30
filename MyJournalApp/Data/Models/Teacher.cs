namespace MyJournalApp.Data.Models
{
    public class Teacher
    {
        public Guid Id { get; set; }
        public bool IsAdmin {  get; set; } = false;
        public bool IsDirector {  get; set; } = false;

        public List<Guid> GroupIds { get; set; } = new();
    }

}
