public class Grade
{
    public Guid Id { get; set; }
    public Guid JournalEntryId { get; set; }
    public Guid StudentId { get; set; }
    public Guid TeacherId { get; set; }
    public int Value { get; set; }
    public string? Comment { get; set; }
    public DateTime Created { get; set; } = DateTime.UtcNow;
}
