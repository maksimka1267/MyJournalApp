namespace MyJournalApp.Data.Dtos.StudentGrades
{
    public class StudentGradesReportDto
    {
        public Guid StudentId { get; set; }

        public string StudentName { get; set; } = "";

        public DateTime StartDate { get; set; }

        public DateTime EndDate { get; set; }

        public List<DateTime> Dates { get; set; } = new();

        public List<SubjectRowDto> Rows { get; set; } = new();
    }

    public class SubjectRowDto
    {
        public string SubjectName { get; set; } = "";

        public Dictionary<string, List<int>> Cells { get; set; } = new();
    }

}
