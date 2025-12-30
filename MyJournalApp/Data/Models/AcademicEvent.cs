public class AcademicEvent
{
    public Guid Id { get; set; }

    public Guid GroupId { get; set; }
    public int Year { get; set; }              // 2024
    public int WeekNumber { get; set; }        // 1-52
    public int Month { get; set; }             // 1-12

    public AcademicWeekType Type { get; set; } // Enum

    public DateTime StartDate { get; set; }    // Начало недели (вручную или авто)
    public DateTime EndDate { get; set; }      // Конец недели
}
