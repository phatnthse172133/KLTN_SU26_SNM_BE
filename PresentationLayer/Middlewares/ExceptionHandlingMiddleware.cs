using ApplicationLayer.Exceptions;
using ApplicationLayer.Helppers;

namespace PresentationLayer.Middlewares;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly IHostEnvironment _environment;

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger,
        IHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (AppException exception)
        {
            await WriteErrorAsync(context, exception, exception.StatusCode, exception.ErrorCode, exception.Message);
        }
        catch (Exception exception)
        {
            await WriteErrorAsync(
                context,
                exception,
                StatusCodes.Status500InternalServerError,
                "INTERNAL_SERVER_ERROR",
                "An unexpected error occurred.");
        }
    }

    private async Task WriteErrorAsync(
        HttpContext context,
        Exception exception,
        int statusCode,
        string errorCode,
        string message)
    {
        var traceId = context.TraceIdentifier;
        _logger.LogError(exception, "Request failed with trace id {TraceId}", traceId);

        if (context.Response.HasStarted)
        {
            throw exception;
        }

        context.Response.Clear();
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";

        var response = ApiResponse<ErrorResponse>.Failure(
            message,
            new ErrorResponse
        {
            TraceId = traceId,
            ErrorCode = errorCode,
            Details = _environment.IsDevelopment() ? exception.ToString() : null
        });

        await context.Response.WriteAsJsonAsync(response);
    }
}
