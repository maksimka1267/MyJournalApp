using MyJournalApp.Data.Models;

namespace MyJournalApp.Jwt
{
    public interface IJwtProvider
    {
        string GenerateToken(User user);
    }

}