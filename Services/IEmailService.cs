namespace Shelfy.Services;

public interface IEmailService
{
    bool IsConfigured { get; }
    bool TwoFactorAuth { get; }
    Task SendWelcomeEmailAsync(string email, string username);
    Task SendLoginNotificationAsync(string email, string username);
    Task SendTwoFactorCodeAsync(string email, string username, string code);
    Task SendPasswordResetCodeAsync(string email, string username, string code);
}
