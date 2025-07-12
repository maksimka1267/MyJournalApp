public class JournalEntry
{
    public Guid Id { get; set; }
    public Guid StudentId { get; set; }
    public DateTime Date { get; set; }
    public string Subject { get; set; }
    public int Grade { get; set; }
    public string Comment { get; set; }
}
