using System.Security.Claims;
using ApplicationLayer.Exceptions;
using InfrastructureLayer.Data;
using Microsoft.EntityFrameworkCore;

namespace PresentationLayer.Middlewares;

/// <summary>
/// Prevents a Booth Owner who is still using a system-generated password from
/// accessing business APIs before setting a personal password.
/// </summary>
public sealed class TemporaryPasswordGuardMiddleware
{
    private readonly RequestDelegate _next;

    public TemporaryPasswordGuardMiddleware(RequestDelegate next)
        => _next = next;

    public async Task InvokeAsync(HttpContext context, SNMDbContext database)
    {
        if (!ShouldInspect(context))
        {
            await _next(context);
            return;
        }

        var userIdValue = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdValue, out var userId))
        {
            await _next(context);
            return;
        }

        var mustChangePassword = await database.Users
            .AsNoTracking()
            .Where(user => user.Id == userId)
            .Select(user => user.MustChangePassword)
            .SingleOrDefaultAsync(context.RequestAborted);

        if (mustChangePassword)
        {
            throw AppException.Forbidden(
                "Change your temporary password before using Booth Owner features.",
                "PASSWORD_CHANGE_REQUIRED");
        }

        await _next(context);
    }

    private static bool ShouldInspect(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated != true || !context.User.IsInRole("BoothOwner"))
            return false;

        var path = (context.Request.Path.Value ?? string.Empty).TrimEnd('/');
        if (!path.StartsWith("/api", StringComparison.OrdinalIgnoreCase))
            return false;

        if (HttpMethods.IsGet(context.Request.Method)
            && string.Equals(path, "/api/account", StringComparison.OrdinalIgnoreCase))
            return false;

        if (HttpMethods.IsPost(context.Request.Method)
            && string.Equals(path, "/api/account/change-password", StringComparison.OrdinalIgnoreCase))
            return false;

        return !(HttpMethods.IsPost(context.Request.Method)
            && string.Equals(path, "/api/auth/logout", StringComparison.OrdinalIgnoreCase));
    }
}
