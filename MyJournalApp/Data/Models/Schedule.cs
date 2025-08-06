public class Schedule
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GroupId { get; set; }

    public DateOnly WeekStartDate { get; set; } // Например, понедельник этой недели
    public List<Guid> Lessons { get; set; }

}
