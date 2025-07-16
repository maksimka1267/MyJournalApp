using System.Diagnostics;

public class JournalEntry
{
    public Guid Id { get; set; }
    public Guid GroupId { get; set; }
    public DateTime Date { get; set; }
    public List<Guid> TeacherId { get; set; }
    public string Subject { get; set; }
    public string Comment { get; set; }

    public List<Grade> Grades { get; set; } = new();
}
