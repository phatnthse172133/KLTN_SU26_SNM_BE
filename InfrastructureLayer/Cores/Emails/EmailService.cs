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
        var host = _configuration["Email:SmtpHost"];
        if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(host))
        {
            _logger.LogWarning("Email is not configured. Verification token for {Email} was generated but not delivered.", email);
            return;
        }

        var link = $"{endpoint}{(endpoint.Contains('?') ? '&' : '?')}token={Uri.EscapeDataString(verificationToken)}";
        var encodedName = WebUtility.HtmlEncode(fullName);
        var encodedLink = WebUtility.HtmlEncode(link);

        var message = new MimeMessage();

        message.From.Add(new MailboxAddress(_configuration["Email:FromName"] ?? "Smart Night Market", _configuration["Email:FromAddress"]));
        message.To.Add(MailboxAddress.Parse(email));
        message.Subject = "Xác thực tài khoản Smart Night Market";
        message.Body = new TextPart("html")
        {
            Text = $$"""
                <!doctype html>
                <html lang="vi">
                <head>
                    <meta charset="utf-8">
                    <meta name="viewport" content="width=device-width, initial-scale=1">
                    <title>Xác thực tài khoản Smart Night Market</title>
                </head>
                <body style="margin:0;padding:0;background-color:#070b1a;font-family:Arial,'Helvetica Neue',sans-serif;color:#f8fafc;">
                    <div style="display:none;max-height:0;overflow:hidden;opacity:0;">
                        Chỉ còn một bước để bắt đầu khám phá Smart Night Market.
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
                                                Chào mừng đến với<br><span style="color:#f6b73c;">khu chợ đêm thông minh</span>
                                            </h1>
                                            <p style="margin:0;color:#9fb0cf;font-size:15px;line-height:1.6;">
                                                Khám phá món ngon, gian hàng và những trải nghiệm rực rỡ về đêm.
                                            </p>
                                        </td>
                                    </tr>
                                    <tr>
                                        <td style="padding:32px 36px 36px;">
                                            <p style="margin:0 0 14px;color:#f8fafc;font-size:17px;line-height:1.6;">
                                                Xin chào <strong>{{encodedName}}</strong>,
                                            </p>
                                            <p style="margin:0 0 26px;color:#c3cee2;font-size:15px;line-height:1.7;">
                                                Cảm ơn bạn đã đăng ký. Vui lòng xác thực địa chỉ email để kích hoạt tài khoản Smart Night Market của bạn.
                                            </p>
                                            <table role="presentation" cellspacing="0" cellpadding="0" border="0" align="center">
                                                <tr>
                                                    <td align="center" bgcolor="#f6b73c" style="border-radius:12px;">
                                                        <a href="{{encodedLink}}" target="_blank" style="display:inline-block;padding:15px 30px;color:#111827;text-decoration:none;font-size:16px;font-weight:800;line-height:1;">
                                                            Xác thực tài khoản
                                                        </a>
                                                    </td>
                                                </tr>
                                            </table>
                                            <p style="margin:25px 0 0;color:#7f91b2;font-size:13px;line-height:1.6;text-align:center;">
                                                Liên kết có hiệu lực trong 24 giờ và chỉ sử dụng được một lần.
                                            </p>
                                            <div style="margin-top:28px;padding-top:22px;border-top:1px solid #273657;">
                                                <p style="margin:0 0 8px;color:#9fb0cf;font-size:12px;line-height:1.5;">
                                                    Nếu nút phía trên không hoạt động, hãy sao chép liên kết này vào trình duyệt:
                                                </p>
                                                <p style="margin:0;word-break:break-all;font-size:12px;line-height:1.5;">
                                                    <a href="{{encodedLink}}" style="color:#65d9e8;text-decoration:underline;">{{encodedLink}}</a>
                                                </p>
                                            </div>
                                        </td>
                                    </tr>
                                    <tr>
                                        <td align="center" style="padding:21px 30px;background-color:#0a1021;color:#7182a3;font-size:12px;line-height:1.6;">
                                            Bạn nhận được email này vì đã đăng ký Smart Night Market.<br>
                                            Nếu không phải bạn, bạn có thể bỏ qua email này.
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

        await client.ConnectAsync(host, _configuration.GetValue<int>("Email:Port", 587), SecureSocketOptions.StartTls, cancellationToken);

        if (!string.IsNullOrWhiteSpace(_configuration["Email:Username"])) 
            await client.AuthenticateAsync(_configuration["Email:Username"], _configuration["Email:Password"], cancellationToken);

        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);
    }

    public async Task SendPasswordResetOtpAsync(string email, string fullName, string otp, CancellationToken cancellationToken = default)
    {
        var host = _configuration["Email:SmtpHost"];
        if (string.IsNullOrWhiteSpace(host))
        {
            _logger.LogWarning("Email is not configured. Password reset OTP for {Email} was generated but not delivered.", email);
            return;
        }

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_configuration["Email:FromName"] ?? "Smart Night Market", _configuration["Email:FromAddress"]));
        message.To.Add(MailboxAddress.Parse(email));
        message.Subject = "Smart Night Market password reset code";
        message.Body = new TextPart("html")
        {
            Text = $"<p>Hello {System.Net.WebUtility.HtmlEncode(fullName)},</p><p>Your password reset OTP is <strong>{otp}</strong>.</p><p>This code expires in 10 minutes. Do not share it with anyone.</p>"
        };

        using var client = new SmtpClient();
        await client.ConnectAsync(host, _configuration.GetValue<int>("Email:Port", 587), SecureSocketOptions.StartTls, cancellationToken);
        if (!string.IsNullOrWhiteSpace(_configuration["Email:Username"]))
            await client.AuthenticateAsync(_configuration["Email:Username"], _configuration["Email:Password"], cancellationToken);
        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);
    }

    public async Task SendPasswordResetLinkAsync(string email, string fullName, string token, CancellationToken cancellationToken = default)
    {
        var endpoint = _configuration["Auth:PasswordResetEndpoint"];
        var host = _configuration["Email:SmtpHost"];
        if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(host))
        {
            _logger.LogWarning("Password-reset endpoint or email is not configured; reset link for {Email} was not delivered.", email);
            return;
        }

        var link = $"{endpoint}{(endpoint.Contains('?') ? '&' : '?')}token={Uri.EscapeDataString(token)}";
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_configuration["Email:FromName"] ?? "Smart Night Market", _configuration["Email:FromAddress"]));
        message.To.Add(MailboxAddress.Parse(email));
        message.Subject = "Reset your Smart Night Market password";
        message.Body = new TextPart("html") { Text = $"<p>Hello {System.Net.WebUtility.HtmlEncode(fullName)},</p><p>Reset your password <a href=\"{link}\">here</a>. This link expires in 15 minutes.</p>" };
        using var client = new SmtpClient();
        await client.ConnectAsync(host, _configuration.GetValue<int>("Email:Port", 587), SecureSocketOptions.StartTls, cancellationToken);
        if (!string.IsNullOrWhiteSpace(_configuration["Email:Username"]))
            await client.AuthenticateAsync(_configuration["Email:Username"], _configuration["Email:Password"], cancellationToken);
        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);
    }
}
