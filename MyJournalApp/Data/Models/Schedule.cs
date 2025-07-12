public class Schedule
{
    public Guid Id { get; set; }
    public Guid GroupId { get; set; }
    public DateTime Date { get; set; }
    public string Subject { get; set; }
    public Guid TeacherId { get; set; }
    public string Room { get; set; }
}
