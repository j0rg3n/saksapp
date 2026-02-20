using MailKit.Net.Smtp;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MimeKit;
using SaksAppWeb.Models;
using System.Diagnostics;
using System.Threading.Tasks;

public class SmtpEmailSender : IEmailSender<ApplicationUser>
{
    private readonly string _host;
    private readonly int _port;
    private readonly string _username;
    private readonly string _password;
    private readonly string _fromEmail;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(IConfiguration configuration, ILogger<SmtpEmailSender> logger)
    {
        _host = configuration["Smtp:Host"] ?? "localhost";
        _port = configuration.GetValue<int>("Smtp:Port", 465);
        _username = configuration["Smtp:Username"] ?? "";
        _password = configuration["Smtp:Password"] ?? "";
        _fromEmail = configuration["Smtp:FromEmail"] ?? "noreply@localhost";
        _logger = logger;
        
        _logger.LogInformation("SMTP configured: Host={Host}, Port={Port}, FromEmail={FromEmail}, Username={Username}", 
            _host, _port, _fromEmail, _username);
    }

    public async Task SendConfirmationLinkAsync(ApplicationUser user, string email, string confirmationLink)
    {
        await SendEmailAsync(email, "Confirm your email", $"Please confirm your account by <a href=\"{confirmationLink}\">clicking here</a>.");
    }

    public async Task SendPasswordResetLinkAsync(ApplicationUser user, string email, string resetLink)
    {
        await SendEmailAsync(email, "Reset your password", $"Reset your password by <a href=\"{resetLink}\">clicking here</a>.");
    }

    public async Task SendPasswordResetCodeAsync(ApplicationUser user, string email, string resetCode)
    {
        await SendEmailAsync(email, "Reset your password", $"Your password reset code is: {resetCode}");
    }

    public async Task SendEmailAsync(string email, string subject, string htmlMessage)
    {
        _logger.LogInformation("Sending email to {Email}, subject: {Subject}", email, subject);
        
        try
        {
            var message = new MimeMessage();
            message.From.Add(MailboxAddress.Parse(_fromEmail));
            message.To.Add(MailboxAddress.Parse(email));
            message.Subject = subject;
            message.Body = new TextPart("html")
            {
                Text = htmlMessage
            };

            using var client = new SmtpClient();
            
            _logger.LogDebug("Connecting to {Host}:{Port}...", _host, _port);
            await client.ConnectAsync(_host, _port, MailKit.Security.SecureSocketOptions.SslOnConnect);
            
            if (!string.IsNullOrEmpty(_username))
            {
                _logger.LogDebug("Authenticating...");
                await client.AuthenticateAsync(_username, _password);
            }
            
            _logger.LogDebug("Sending...");
            var stopwatch = Stopwatch.StartNew();
            await client.SendAsync(message);
            stopwatch.Stop();
            
            _logger.LogDebug("Disconnecting...");
            await client.DisconnectAsync(true);
            
            _logger.LogInformation("Email sent successfully in {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {Email}", email);
            throw;
        }
    }
}
