namespace MyJournalApp.Data.Dtos.Absence
{
    public class AbsenceReportDto
    {
        public Guid GroupId { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
    }
}
