using MyJournalApp.Data.Models;

public class Student:Client
{
    public Guid GroupId { get; set; }
    public List<Guid> GradeIds { get; set; } = new();
}
