public class Course
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = null!;
    public Guid TeacherId { get; set; }
    public List<Guid> GroupsID { get; set; } = new();
}
