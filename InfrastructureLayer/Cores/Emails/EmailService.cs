using DomainLayer.InterfaceCore.Email;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MimeKit;

namespace InfrastructureLayer.Cores.Emails;

public class EmailService : IEmailService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task SendVerificationEmailAsync(string email, string fullName, string verificationToken, CancellationToken cancellationToken = default)
    {
        var endpoint = _configuration["Auth:VerificationEndpoint"];
        var host = _configuration["Email:SmtpHost"];
        if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(host))
        {
            _logger.LogWarning("Email is not configured. Verification token for {Email} was generated but not delivered.", email);
            return;
        }

        var link = $"{endpoint}{(endpoint.Contains('?') ? '&' : '?')}token={Uri.EscapeDataString(verificationToken)}";

        var message = new MimeMessage();

        message.From.Add(new MailboxAddress(_configuration["Email:FromName"] ?? "Smart Night Market", _configuration["Email:FromAddress"]));
        message.To.Add(MailboxAddress.Parse(email));
        message.Subject = "Xác thực tài khoản Smart Night Market";
        message.Body = new TextPart("html") { Text = $"<p>Chào {System.Net.WebUtility.HtmlEncode(fullName)},</p><p>Hãy xác thực tài khoản tại <a href=\"{link}\">đây</a>.</p>" };

        using var client = new SmtpClient();

        await client.ConnectAsync(host, _configuration.GetValue<int>("Email:Port", 587), SecureSocketOptions.StartTls, cancellationToken);

        if (!string.IsNullOrWhiteSpace(_configuration["Email:Username"])) 
            await client.AuthenticateAsync(_configuration["Email:Username"], _configuration["Email:Password"], cancellationToken);

        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);
    }
}
