namespace MyJournalApp.Data.Dtos.Auth
{
    public class ResetPasswordDto
    {
        public Guid? UserId { get; set; }

        public string? Email { get; set; }
    }
}