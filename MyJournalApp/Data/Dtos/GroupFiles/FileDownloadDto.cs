namespace MyJournalApp.Data.Dtos.GroupFiles
{
    public class FileDownloadDto
    {
        public byte[] Content { get; set; } = Array.Empty<byte>();

        public string ContentType { get; set; } = "";

        public string FileName { get; set; } = "";
    }
}
