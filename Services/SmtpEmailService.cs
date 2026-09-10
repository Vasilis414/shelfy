using System.Net;
using System.Net.Mail;
using System.Net.Mime;
using Microsoft.Extensions.Options;

namespace Shelfy.Services;

public class SmtpEmailService : IEmailService
{
    private readonly EmailSettings _settings;
    private readonly ILogger<SmtpEmailService> _logger;
    private readonly IWebHostEnvironment _environment;

    public SmtpEmailService(
        IOptions<EmailSettings> settings,
        ILogger<SmtpEmailService> logger,
        IWebHostEnvironment environment)
    {
        _settings = settings.Value;
        _logger = logger;
        _environment = environment;
    }

    public bool IsConfigured => _settings.IsConfigured;
    public bool TwoFactorAuth => _settings.EnableTwoFactor && IsConfigured;

    public Task SendWelcomeEmailAsync(string email, string username)
    {
        var body = BuildTemplate(
            "Welcome to Shelfy",
            $"Hello {WebUtility.HtmlEncode(username)},",
            "Your Shelfy account has been created successfully. You can now sign in and use the library catalog.");

        return SendAsync(email, "Welcome to Shelfy", body);
    }

    public Task SendLoginNotificationAsync(string email, string username)
    {
        var body = BuildTemplate(
            "New login to Shelfy",
            $"Hello {WebUtility.HtmlEncode(username)},",
            $"Your account was used to sign in on {DateTime.Now:yyyy-MM-dd HH:mm}. If this was not you, reset your password.");

        return SendAsync(email, "Shelfy login notification", body);
    }

    public Task SendTwoFactorCodeAsync(string email, string username, string code)
    {
        var body = BuildTemplate(
            "Your Shelfy verification code",
            $"Hello {WebUtility.HtmlEncode(username)},",
            $"Use this code to finish signing in: <strong style=\"font-size:24px;letter-spacing:4px;\">{code}</strong>");

        return SendAsync(email, "Your Shelfy verification code", body);
    }

    public Task SendPasswordResetCodeAsync(string email, string username, string code)
    {
        var body = BuildTemplate(
            "Reset your Shelfy password",
            $"Hello {WebUtility.HtmlEncode(username)},",
            $"Use this code to reset your password: <strong style=\"font-size:24px;letter-spacing:4px;\">{code}</strong>");

        return SendAsync(email, "Reset your Shelfy password", body);
    }

    private async Task SendAsync(string toEmail, string subject, string htmlBody)
    {
        if (!IsConfigured)
        {
            _logger.LogInformation("Email skipped because SMTP settings are not configured. Subject: {Subject}", subject);
            return;
        }

        try
        {
            using var message = new MailMessage
            {
                From = new MailAddress(_settings.FromEmail, _settings.FromName),
                Subject = subject,
                IsBodyHtml = true
            };
            message.To.Add(toEmail);
            var htmlView = AlternateView.CreateAlternateViewFromString(htmlBody, null, MediaTypeNames.Text.Html);
            var logoPath = Path.Combine(_environment.WebRootPath, "shelfy.png");

            if (File.Exists(logoPath))
            {
                var logo = new LinkedResource(logoPath, MediaTypeNames.Image.Png)
                {
                    ContentId = "shelfy-logo",
                    TransferEncoding = TransferEncoding.Base64
                };
                htmlView.LinkedResources.Add(logo);
            }

            message.AlternateViews.Add(htmlView);

            using var client = new SmtpClient(_settings.Host, _settings.Port)
            {
                EnableSsl = _settings.EnableSsl,
                Credentials = new NetworkCredential(_settings.Username, _settings.Password)
            };

            await client.SendMailAsync(message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {Email}", toEmail);
        }
    }

    private static string BuildTemplate(string title, string greeting, string message)
    {
        return $"""
            <div style="font-family:Arial,sans-serif;background:#f8f9fa;padding:28px;">
                <div style="max-width:560px;margin:0 auto;background:#ffffff;border:1px solid #dee2e6;border-radius:8px;padding:24px;">
                    <div style="text-align:center;margin-bottom:22px;">
                        <img src="cid:shelfy-logo" alt="Shelfy" style="height:48px;" />
                        <h1 style="font-size:22px;margin:14px 0 0;color:#212529;">{title}</h1>
                    </div>
                    <p style="font-size:16px;color:#212529;">{greeting}</p>
                    <p style="font-size:16px;color:#212529;line-height:1.5;">{message}</p>
                    <p style="font-size:13px;color:#6c757d;margin-top:24px;">Shelfy Library Management System</p>
                </div>
            </div>
            """;
    }
}
