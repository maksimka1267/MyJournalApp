namespace MyJournalApp.Dtos.IndividualPlan;

public class IndividualPlanFileDto
{
    public byte[] Content { get; set; } = Array.Empty<byte>();

    public string FileName { get; set; } = "";

    public string ContentType { get; set; } =
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
}