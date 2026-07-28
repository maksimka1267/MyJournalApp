public class JournalExportItemDto
{
    public Guid JournalId { get; set; }

    public Guid GroupId { get; set; }

    public string JournalName { get; set; }

    public string GroupName { get; set; }

    public string Subject { get; set; }

    public List<string> Teachers { get; set; }

    public DateTime Date { get; set; }
}
public class JournalExportListDto
{
    public List<JournalExportItemDto> Journals { get; set; } = new();
}