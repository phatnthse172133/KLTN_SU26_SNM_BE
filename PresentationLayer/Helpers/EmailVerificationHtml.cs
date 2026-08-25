using Microsoft.AspNetCore.Mvc;

namespace PresentationLayer.Helpers;

internal static class EmailVerificationHtml
{
    public static bool WantsHtml(HttpRequest request)
    {
        var accept = request.Headers.Accept.ToString();
        if (string.IsNullOrWhiteSpace(accept))
        {
            return false;
        }

        var htmlIndex = accept.IndexOf("text/html", StringComparison.OrdinalIgnoreCase);
        if (htmlIndex < 0)
        {
            return false;
        }

        var jsonIndex = accept.IndexOf("application/json", StringComparison.OrdinalIgnoreCase);
        return jsonIndex < 0 || htmlIndex <= jsonIndex;
    }

    public static IActionResult Page(bool success, int statusCode)
    {
        var title = success ? "Email verified" : "Email verification failed";
        var body = success
            ? "Your Smart Night Market account has been activated. Return to the app and sign in."
            : "This verification link is invalid or has expired. Open the app and request a new verification email.";

        var html = $$"""
            <!doctype html>
            <html lang="en">
            <head>
                <meta charset="utf-8">
                <meta name="viewport" content="width=device-width, initial-scale=1">
                <title>{{title}} | Smart Night Market</title>
            </head>
            <body style="margin:0;padding:0;background-color:#070b1a;font-family:Arial,'Helvetica Neue',sans-serif;color:#f8fafc;">
                <table role="presentation" width="100%" cellspacing="0" cellpadding="0" border="0" style="background-color:#070b1a;min-height:100vh;">
                    <tr>
                        <td align="center" style="padding:48px 16px;">
                            <table role="presentation" width="100%" cellspacing="0" cellpadding="0" border="0" style="max-width:520px;background-color:#11182d;border:1px solid #273657;border-radius:20px;overflow:hidden;">
                                <tr>
                                    <td style="height:6px;background-color:#f6b73c;font-size:0;line-height:0;">&nbsp;</td>
                                </tr>
                                <tr>
                                    <td style="padding:36px 32px 40px;text-align:center;">
                                        <div style="display:inline-block;padding:8px 14px;border:1px solid #f6b73c;border-radius:999px;color:#f6b73c;font-size:12px;font-weight:700;letter-spacing:2px;text-transform:uppercase;">
                                            SMART NIGHT MARKET
                                        </div>
                                        <h1 style="margin:22px 0 12px;color:#ffffff;font-size:26px;line-height:1.3;">{{title}}</h1>
                                        <p style="margin:0;color:#c3cee2;font-size:16px;line-height:1.7;">{{body}}</p>
                                    </td>
                                </tr>
                            </table>
                        </td>
                    </tr>
                </table>
            </body>
            </html>
            """;

        return new ContentResult
        {
            StatusCode = statusCode,
            ContentType = "text/html; charset=utf-8",
            Content = html
        };
    }
}
