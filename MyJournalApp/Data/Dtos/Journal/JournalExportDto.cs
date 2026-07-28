namespace MyJournalApp.Data.Dtos.Journal
{
    public class JournalExportDto
    {
        public byte[] FileBytes { get; set; } = Array.Empty<byte>();

        public string FileName { get; set; } = string.Empty;

        public string ContentType { get; set; } =
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    }
}
