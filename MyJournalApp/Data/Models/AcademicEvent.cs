public class AcademicEvent
{
    public Guid Id { get; set; }
    public string Type { get; set; } // "Practice", "Holiday", "ExamSession"
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string Description { get; set; }
}
