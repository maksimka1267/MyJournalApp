using MyJournalApp.Data.Dtos.Auth;
using MyJournalApp.Result;

public interface IAuthService
{
    Task<IServiceResult> RegisterAsync(RegisterDto dto);

    Task<ServiceResult<string>> LoginAsync(LoginDto dto);

    Task<User?> GetCurrentUserAsync(Guid userId);

    Task<IServiceResult> ChangePasswordAsync(Guid userId, ChangePasswordDto dto);

    Task<IServiceResult> BulkRegisterAsync(BulkRegisterDto dto);

    Task<IServiceResult> ResetPasswordAsync(Guid adminId, ResetPasswordDto dto);
}