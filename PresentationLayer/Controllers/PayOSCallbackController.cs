using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PresentationLayer.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/payment-callback/payos")]
public sealed class PayOSCallbackController : ControllerBase
{
    [HttpGet("return")]
    public ContentResult Return([FromQuery] Guid? orderId) => AppRedirect(orderId, "return");

    [HttpGet("cancel")]
    public ContentResult Cancel([FromQuery] Guid? orderId) => AppRedirect(orderId, "cancel");

    private static ContentResult AppRedirect(Guid? orderId, string result)
    {
        var target = $"snmcustomer://payment/result?result={Uri.EscapeDataString(result)}"
            + (orderId.HasValue ? $"&orderId={orderId.Value:D}" : string.Empty);
        var encoded = WebUtility.HtmlEncode(target);
        return new ContentResult
        {
            ContentType = "text/html; charset=utf-8",
            StatusCode = StatusCodes.Status200OK,
            Content = $"<!doctype html><html><head><meta name=\"viewport\" content=\"width=device-width\"><meta http-equiv=\"refresh\" content=\"0;url={encoded}\"></head><body><p>Đang quay lại ứng dụng…</p><p><a href=\"{encoded}\">Mở Smart Night Market</a></p></body></html>"
        };
    }
}
