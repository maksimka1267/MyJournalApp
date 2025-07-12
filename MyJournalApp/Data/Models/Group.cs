public class Group
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public List<Guid>? StudentIds { get; set; }
    public Guid TeacherId { get; set; }
}
