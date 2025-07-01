public class Grade
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int Value { get; set; }
    public DateTime Date { get; set; }

    public Guid StudentId { get; set; }
    public Guid TeacherId { get; set; }
    public Guid CourseId { get; set; }
}
