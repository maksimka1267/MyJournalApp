namespace MyJournalApp.Data.Dtos.GroupFiles
{
    public class GroupFilesStatusDto
    {
        public Guid GroupId {  get; set; }
        public string GroupName { get; set; } = "";
        public bool Sem1Exists { get; set; }
        public bool Sem2Exists { get; set; }
        public int Count => (Sem1Exists ? 1 : 0) + (Sem2Exists ? 1 : 0);
    }
}
