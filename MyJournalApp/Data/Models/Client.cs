namespace MyJournalApp.Data.Models
{
    public class Client
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string FullName { get; set; } = null!;
        public string Password { get; set; }
        public string Email { get; set; } = null!;
        public string Role { get; set; } = null!; // "Student", "Teacher", "Admin"

    }
}
