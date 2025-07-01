namespace MyJournalApp.Data.Models
{
    public class Group
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = null!;

        // Навигационное свойство
        public List<Guid> Students { get; set; } = new();
    }

}
