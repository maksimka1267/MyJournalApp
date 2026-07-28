namespace MyJournalApp.Data.Dtos.GroupFiles
{
    public class UploadGroupFileDto
    {
        public Guid GroupId { get; set; }
        public int Semester { get; set; }
        public IFormFile File { get; set; } = null!;
    }
}
