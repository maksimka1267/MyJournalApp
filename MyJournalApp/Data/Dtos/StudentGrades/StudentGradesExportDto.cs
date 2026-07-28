namespace MyJournalApp.Data.Dtos.StudentGrades
{
    public class StudentGradesExportDto
    {
        public byte[] Content { get; set; } = Array.Empty<byte>();

        public string ContentType { get; set; } = string.Empty;

        public string FileName { get; set; } = string.Empty;
    }

}
