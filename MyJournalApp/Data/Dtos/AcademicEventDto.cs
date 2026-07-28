using MyJournalApp.Data.Models;

public class AcademicEventDto
{
    public Guid Id { get; set; }

    public Guid GroupId { get; set; }

    public AcademicWeekType Type { get; set; }

    public int Year { get; set; }

    public int Month { get; set; }

    public int WeekNumber { get; set; }

    public DateTime StartDate { get; set; }

    public DateTime EndDate { get; set; }
}