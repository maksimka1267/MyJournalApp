namespace MyJournalApp.Data.Dtos.Journal
{
    public class CreateUpdateJournalDto
    {
        public string Name { get; set; } = "";

        public DateTime Date { get; set; }

        public string? Comment { get; set; }

        public Guid GroupId { get; set; }

        public Guid TeacherId { get; set; }
    }
}
