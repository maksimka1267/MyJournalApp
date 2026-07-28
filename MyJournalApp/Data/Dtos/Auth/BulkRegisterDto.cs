using Microsoft.AspNetCore.Http;

namespace MyJournalApp.Data.Dtos.Auth
{
    public class BulkRegisterDto
    {
        public IFormFile File { get; set; } = null!;

        public string Role { get; set; } = string.Empty;

        public Guid? GroupId { get; set; }
    }
}