using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace TrovaBackend.Services;

public interface IEmailService
{
    Task SendVerificationCodeAsync(string toEmail, string name, string code);
    Task SendPasswordResetEmailAsync(string toEmail, string name, string code);
    Task SendAccountApprovedEmailAsync(string toEmail, string name);
    Task SendAccountRejectedEmailAsync(string toEmail, string name, string reason);
    Task SendDisputeResolvedEmailAsync(string toEmail, string name, string projectTitle, string resolutionMessage);
}

// Ported from GoFix's EmailService.cs (MailKit + Gmail SMTP). Needs two
// config values — see appsettings notes in NOTES.md — pull from
// environment variables / user-secrets locally, never commit them.
public class EmailService : IEmailService
{
    private readonly IConfiguration _config;

    public EmailService(IConfiguration config)
    {
        _config = config;
    }

    public async Task SendVerificationCodeAsync(string toEmail, string name, string code)
    {
        await SendAsync(
            toEmail,
            "Your Trova Verification Code",
            $@"
<!DOCTYPE html>
<html>
<body style='font-family: Arial, sans-serif; background-color: #f4f4f4; padding: 20px;'>
  <div style='max-width: 500px; margin: auto; background: white; border-radius: 12px; padding: 32px;'>
    <h2 style='color: #1A1A1A; text-align: center;'>Trova</h2>
    <hr style='border: 2px solid #B7202E; margin: 16px 0;'/>
    <p style='color: #444; font-size: 16px;'>Hi <strong>{name}</strong>,</p>
    <p style='color: #444; font-size: 16px;'>Your email verification code is:</p>
    <div style='text-align: center; margin: 32px 0;'>
      <span style='font-size: 48px; font-weight: bold; letter-spacing: 16px; color: #B7202E;'>{code}</span>
    </div>
    <p style='color: #888; font-size: 14px;'>This code expires in <strong>10 minutes</strong>.</p>
    <p style='color: #888; font-size: 14px;'>If you didn't request this, you can safely ignore this email.</p>
    <hr style='border: 1px solid #eee; margin: 24px 0;'/>
    <p style='color: #aaa; font-size: 12px; text-align: center;'>Trova — Contractor Capability &amp; Guarantee Platform</p>
  </div>
</body>
</html>");
    }

    public async Task SendPasswordResetEmailAsync(string toEmail, string name, string code)
    {
        await SendAsync(
            toEmail,
            "Trova — Password Reset Code",
            $@"
<!DOCTYPE html>
<html>
<body style='font-family: Arial, sans-serif; background-color: #f4f4f4; padding: 20px;'>
  <div style='max-width: 500px; margin: auto; background: white; border-radius: 12px; padding: 32px;'>
    <h2 style='color: #1A1A1A; text-align: center;'>Trova</h2>
    <hr style='border: 2px solid #B7202E; margin: 16px 0;'/>
    <p style='color: #444; font-size: 16px;'>Hi <strong>{name}</strong>,</p>
    <p style='color: #444; font-size: 16px;'>We received a request to reset your password. Use the code below:</p>
    <div style='text-align: center; margin: 32px 0;'>
      <span style='font-size: 48px; font-weight: bold; letter-spacing: 16px; color: #B7202E;'>{code}</span>
    </div>
    <p style='color: #888; font-size: 14px;'>This code expires in <strong>15 minutes</strong>.</p>
    <p style='color: #888; font-size: 14px;'>If you did not request a password reset, you can safely ignore this email.</p>
    <hr style='border: 1px solid #eee; margin: 24px 0;'/>
    <p style='color: #aaa; font-size: 12px; text-align: center;'>Trova — Contractor Capability &amp; Guarantee Platform</p>
  </div>
</body>
</html>");
    }

    public async Task SendAccountApprovedEmailAsync(string toEmail, string name)
    {
        await SendAsync(
            toEmail,
            "Your Trova account has been approved",
            $@"
<!DOCTYPE html>
<html>
<body style='font-family: Arial, sans-serif; background-color: #f4f4f4; padding: 20px;'>
  <div style='max-width: 500px; margin: auto; background: white; border-radius: 12px; padding: 32px;'>
    <h2 style='color: #1A1A1A; text-align: center;'>Trova</h2>
    <hr style='border: 2px solid #B7202E; margin: 16px 0;'/>
    <p style='color: #444; font-size: 16px;'>Hi <strong>{name}</strong>,</p>
    <p style='color: #444; font-size: 16px;'>Good news — your account has been approved. You can now log in and start using Trova.</p>
    <hr style='border: 1px solid #eee; margin: 24px 0;'/>
    <p style='color: #aaa; font-size: 12px; text-align: center;'>Trova — Contractor Capability &amp; Guarantee Platform</p>
  </div>
</body>
</html>");
    }

    public async Task SendAccountRejectedEmailAsync(string toEmail, string name, string reason)
    {
        await SendAsync(
            toEmail,
            "Your Trova account application",
            $@"
<!DOCTYPE html>
<html>
<body style='font-family: Arial, sans-serif; background-color: #f4f4f4; padding: 20px;'>
  <div style='max-width: 500px; margin: auto; background: white; border-radius: 12px; padding: 32px;'>
    <h2 style='color: #1A1A1A; text-align: center;'>Trova</h2>
    <hr style='border: 2px solid #B7202E; margin: 16px 0;'/>
    <p style='color: #444; font-size: 16px;'>Hi <strong>{name}</strong>,</p>
    <p style='color: #444; font-size: 16px;'>We've reviewed your account application and are unable to approve it at this time.</p>
    <p style='color: #444; font-size: 16px;'><strong>Reason:</strong> {reason}</p>
    <hr style='border: 1px solid #eee; margin: 24px 0;'/>
    <p style='color: #aaa; font-size: 12px; text-align: center;'>Trova — Contractor Capability &amp; Guarantee Platform</p>
  </div>
</body>
</html>");
    }

    public async Task SendDisputeResolvedEmailAsync(string toEmail, string name, string projectTitle, string resolutionMessage)
    {
        await SendAsync(
            toEmail,
            $"Dispute resolved — {projectTitle}",
            $@"
<!DOCTYPE html>
<html>
<body style='font-family: Arial, sans-serif; background-color: #f4f4f4; padding: 20px;'>
  <div style='max-width: 500px; margin: auto; background: white; border-radius: 12px; padding: 32px;'>
    <h2 style='color: #1A1A1A; text-align: center;'>Trova</h2>
    <hr style='border: 2px solid #B7202E; margin: 16px 0;'/>
    <p style='color: #444; font-size: 16px;'>Hi <strong>{name}</strong>,</p>
    <p style='color: #444; font-size: 16px;'>The dispute on <strong>{projectTitle}</strong> has been resolved by the Trova team:</p>
    <p style='color: #444; font-size: 16px; background: #f9f9f9; border-radius: 8px; padding: 16px;'>{resolutionMessage}</p>
    <hr style='border: 1px solid #eee; margin: 24px 0;'/>
    <p style='color: #aaa; font-size: 12px; text-align: center;'>Trova — Contractor Capability &amp; Guarantee Platform</p>
  </div>
</body>
</html>");
    }

    private async Task SendAsync(string toEmail, string subject, string htmlBody)
    {
        var fromEmail = _config["Email:From"]!;
        var appPassword = _config["Email:AppPassword"]!;

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress("Trova", fromEmail));
        message.To.Add(new MailboxAddress(string.Empty, toEmail));
        message.Subject = subject;
        message.Body = new TextPart("html") { Text = htmlBody };

        using var client = new SmtpClient();
        await client.ConnectAsync("smtp.gmail.com", 465, SecureSocketOptions.SslOnConnect); await client.AuthenticateAsync(fromEmail, appPassword);
        await client.SendAsync(message);
        await client.DisconnectAsync(true);
    }
}
