namespace MyJournalApp.Data.Dtos.Journal
{
    public class JournalColumnDto
    {
        public DateTime Date { get; set; }

        public string Topic { get; set; } = "";

        public DateTime FirstCreated { get; set; }
    }
}
