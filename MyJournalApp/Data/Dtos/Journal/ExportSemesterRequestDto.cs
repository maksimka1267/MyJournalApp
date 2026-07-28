namespace MyJournalApp.Data.Dtos.Journal
{
    public class ExportSemesterRequestDto
    {
        public Guid GroupId { get; set; }

        public int Semester { get; set; }
        public int Year { get; set; }
    }
}
