namespace MyJournalApp.Dtos.IndividualPlan;

public class DownloadIndividualPlanRequestDto
{
    public Guid StudentId { get; set; }

    public int? Semester { get; set; }
}