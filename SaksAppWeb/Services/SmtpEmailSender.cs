using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Configuration;
using SaksAppWeb.Models;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;

public class SmtpEmailSender : IEmailSender<ApplicationUser>
{
    private readonly string _host;
    private readonly int _port;
    private readonly string _fromEmail;

    public SmtpEmailSender(IConfiguration configuration)
    {
        _host = configuration["Smtp:Host"] ?? "localhost";
        _port = configuration.GetValue<int>("Smtp:Port", 1025);
        _fromEmail = configuration["Smtp:FromEmail"] ?? "noreply@localhost";
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
        using var client = new SmtpClient(_host, _port)
        {
            EnableSsl = false,
            DeliveryMethod = SmtpDeliveryMethod.Network
        };

        var message = new MailMessage(_fromEmail, email, subject, htmlMessage)
        {
            IsBodyHtml = true
        };

        await client.SendMailAsync(message);
    }
}
