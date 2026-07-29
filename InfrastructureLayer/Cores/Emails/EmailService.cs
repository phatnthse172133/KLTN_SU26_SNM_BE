using DomainLayer.InterfaceCore.Email;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MimeKit;
using System.Net;

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
        var smtp = GetSmtpConfiguration();
        if (string.IsNullOrWhiteSpace(endpoint) || smtp is null)
        {
            _logger.LogWarning("Email is not configured. Verification token for {Email} was generated but not delivered.", email);
            return;
        }

        var link = $"{endpoint}{(endpoint.Contains('?') ? '&' : '?')}token={Uri.EscapeDataString(verificationToken)}";
        var encodedName = WebUtility.HtmlEncode(fullName);
        var encodedLink = WebUtility.HtmlEncode(link);

        var message = new MimeMessage();

        message.From.Add(new MailboxAddress(smtp.FromName, smtp.FromAddress));
        message.To.Add(MailboxAddress.Parse(email));
        message.Subject = "Verify your Smart Night Market account";
        message.Body = new TextPart("html")
        {
            Text = $$"""
                <!doctype html>
                <html lang="en">
                <head>
                    <meta charset="utf-8">
                    <meta name="viewport" content="width=device-width, initial-scale=1">
                    <title>Verify your Smart Night Market account</title>
                </head>
                <body style="margin:0;padding:0;background-color:#070b1a;font-family:Arial,'Helvetica Neue',sans-serif;color:#f8fafc;">
                    <div style="display:none;max-height:0;overflow:hidden;opacity:0;">
                        Just one more step to start exploring Smart Night Market.
                    </div>
                    <table role="presentation" width="100%" cellspacing="0" cellpadding="0" border="0" style="background-color:#070b1a;">
                        <tr>
                            <td align="center" style="padding:32px 12px;">
                                <table role="presentation" width="100%" cellspacing="0" cellpadding="0" border="0" style="max-width:600px;background-color:#11182d;border:1px solid #273657;border-radius:20px;overflow:hidden;">
                                    <tr>
                                        <td style="height:6px;background-color:#f6b73c;font-size:0;line-height:0;">&nbsp;</td>
                                    </tr>
                                    <tr>
                                        <td align="center" style="padding:38px 36px 24px;background-color:#0d1428;">
                                            <div style="display:inline-block;padding:9px 15px;border:1px solid #f6b73c;border-radius:999px;color:#f6b73c;font-size:12px;font-weight:700;letter-spacing:2px;text-transform:uppercase;">
                                                SMART NIGHT MARKET
                                            </div>
                                            <h1 style="margin:22px 0 10px;color:#ffffff;font-size:30px;line-height:1.25;font-weight:800;">
                                                Welcome to<br><span style="color:#f6b73c;">Smart Night Market</span>
                                            </h1>
                                            <p style="margin:0;color:#9fb0cf;font-size:15px;line-height:1.6;">
                                                Discover great food, booths, and vibrant night experiences.
                                            </p>
                                        </td>
                                    </tr>
                                    <tr>
                                        <td style="padding:32px 36px 36px;">
                                            <p style="margin:0 0 14px;color:#f8fafc;font-size:17px;line-height:1.6;">
                                                Hello <strong>{{encodedName}}</strong>,
                                            </p>
                                            <p style="margin:0 0 26px;color:#c3cee2;font-size:15px;line-height:1.7;">
                                                Thank you for signing up. Please verify your email address to activate your Smart Night Market account.
                                            </p>
                                            <table role="presentation" cellspacing="0" cellpadding="0" border="0" align="center">
                                                <tr>
                                                    <td align="center" bgcolor="#f6b73c" style="border-radius:12px;">
                                                        <a href="{{encodedLink}}" target="_blank" style="display:inline-block;padding:15px 30px;color:#111827;text-decoration:none;font-size:16px;font-weight:800;line-height:1;">
                                                            Verify Account
                                                        </a>
                                                    </td>
                                                </tr>
                                            </table>
                                            <p style="margin:25px 0 0;color:#7f91b2;font-size:13px;line-height:1.6;text-align:center;">
                                                This link is valid for 24 hours and can only be used once.
                                            </p>
                                            <div style="margin-top:28px;padding-top:22px;border-top:1px solid #273657;">
                                                <p style="margin:0 0 8px;color:#9fb0cf;font-size:12px;line-height:1.5;">
                                                    If the button above doesn't work, copy this link into your browser:
                                                </p>
                                                <p style="margin:0;word-break:break-all;font-size:12px;line-height:1.5;">
                                                    <a href="{{encodedLink}}" style="color:#65d9e8;text-decoration:underline;">{{encodedLink}}</a>
                                                </p>
                                            </div>
                                        </td>
                                    </tr>
                                    <tr>
                                        <td align="center" style="padding:21px 30px;background-color:#0a1021;color:#7182a3;font-size:12px;line-height:1.6;">
                                            You received this email because you signed up for Smart Night Market.<br>
                                            If this wasn't you, you can safely ignore this email.
                                        </td>
                                    </tr>
                                </table>
                            </td>
                        </tr>
                    </table>
                </body>
                </html>
                """
        };

        using var client = new SmtpClient();

        await client.ConnectAsync(smtp.Host, smtp.Port, SecureSocketOptions.StartTls, cancellationToken);

        if (smtp.Username is not null && smtp.Password is not null)
            await client.AuthenticateAsync(smtp.Username, smtp.Password, cancellationToken);

        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);
    }

    public async Task SendPasswordResetOtpAsync(string email, string fullName, string otp, CancellationToken cancellationToken = default)
    {
        var smtp = GetSmtpConfiguration();
        if (smtp is null)
        {
            _logger.LogWarning("Email is not configured. Password reset OTP for {Email} was generated but not delivered.", email);
            return;
        }

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(smtp.FromName, smtp.FromAddress));
        message.To.Add(MailboxAddress.Parse(email));
        message.Subject = "Smart Night Market password reset code";
        message.Body = new TextPart("html")
        {
            Text = $"<p>Hello {System.Net.WebUtility.HtmlEncode(fullName)},</p><p>Your password reset OTP is <strong>{otp}</strong>.</p><p>This code expires in 10 minutes. Do not share it with anyone.</p>"
        };

        using var client = new SmtpClient();
        await client.ConnectAsync(smtp.Host, smtp.Port, SecureSocketOptions.StartTls, cancellationToken);
        if (smtp.Username is not null && smtp.Password is not null)
            await client.AuthenticateAsync(smtp.Username, smtp.Password, cancellationToken);
        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);
    }

    public async Task SendPasswordResetLinkAsync(string email, string fullName, string token, CancellationToken cancellationToken = default)
    {
        var endpoint = _configuration["Auth:PasswordResetEndpoint"];
        var smtp = GetSmtpConfiguration();
        if (string.IsNullOrWhiteSpace(endpoint) || smtp is null)
        {
            _logger.LogWarning("SMTP email configuration or password-reset endpoint is missing. Email skipped.");
            return;
        }

        var link = $"{endpoint}{(endpoint.Contains('?') ? '&' : '?')}token={Uri.EscapeDataString(token)}";
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(smtp.FromName, smtp.FromAddress));
        message.To.Add(MailboxAddress.Parse(email));
        message.Subject = "Reset your Smart Night Market password";
        message.Body = new TextPart("html") { Text = $"<p>Hello {System.Net.WebUtility.HtmlEncode(fullName)},</p><p>Reset your password <a href=\"{link}\">here</a>. This link expires in 15 minutes.</p>" };
        using var client = new SmtpClient();
        await client.ConnectAsync(smtp.Host, smtp.Port, SecureSocketOptions.StartTls, cancellationToken);
        if (smtp.Username is not null && smtp.Password is not null)
            await client.AuthenticateAsync(smtp.Username, smtp.Password, cancellationToken);
        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);
    }

    public async Task SendHtmlEmailAsync(string recipientEmail, string subject, string htmlBody, CancellationToken cancellationToken = default)
    {
        var smtp = GetSmtpConfiguration();
        if (smtp is null)
        {
            throw new InvalidOperationException("SMTP email configuration is missing.");
        }

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(smtp.FromName, smtp.FromAddress));
        message.To.Add(MailboxAddress.Parse(recipientEmail));
        message.Subject = subject;
        message.Body = new TextPart("html")
        {
            Text = htmlBody
        };

        using var client = new SmtpClient();
        try
        {
            await client.ConnectAsync(smtp.Host, smtp.Port, SecureSocketOptions.StartTls, cancellationToken);
            if (smtp.Username is not null && smtp.Password is not null)
            {
                await client.AuthenticateAsync(smtp.Username, smtp.Password, cancellationToken);
            }

            await client.SendAsync(message, cancellationToken);
            _logger.LogInformation("Sent email to {Email}", recipientEmail);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {Email}", recipientEmail);
            throw; // Re-throw to let the worker handle retry
        }
        finally
        {
            if (client.IsConnected)
            {
                try
                {
                    await client.DisconnectAsync(true, cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    // Disposal closes the connection; shutdown cancellation is expected here.
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to disconnect cleanly from the SMTP server.");
                }
            }
        }
    }

    private SmtpConfiguration? GetSmtpConfiguration()
    {
        var host = _configuration["Email:SmtpHost"];
        var fromAddress = _configuration["Email:FromAddress"];
        var username = _configuration["Email:Username"];
        var password = _configuration["Email:Password"];

        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(fromAddress))
            return null;

        var hasUsername = !string.IsNullOrWhiteSpace(username);
        var hasPassword = !string.IsNullOrWhiteSpace(password);
        if (hasUsername != hasPassword)
        {
            _logger.LogWarning("Email SMTP credentials are incomplete.");
            return null;
        }

        return new SmtpConfiguration(
            host,
            _configuration.GetValue<int>("Email:Port", 587),
            _configuration["Email:FromName"] ?? "Smart Night Market",
            fromAddress,
            hasUsername ? username : null,
            hasPassword ? password : null);
    }

    private sealed record SmtpConfiguration(
        string Host,
        int Port,
        string FromName,
        string FromAddress,
        string? Username,
        string? Password);
}
