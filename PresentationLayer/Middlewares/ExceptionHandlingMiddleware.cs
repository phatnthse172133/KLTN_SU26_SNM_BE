using ApplicationLayer.Exceptions;
using ApplicationLayer.Helppers;
using Microsoft.EntityFrameworkCore;

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
        catch (DbUpdateException exception)
        {
            await WriteErrorAsync(
                context,
                exception,
                StatusCodes.Status409Conflict,
                "DATABASE_CONFLICT",
                "The request conflicts with existing data.");
        }
        catch (Npgsql.PostgresException exception)
        {
            await WriteErrorAsync(
                context,
                exception,
                StatusCodes.Status500InternalServerError,
                "DATABASE_SCHEMA_ERROR",
                "A database configuration issue occurred. Please contact the system administrator.");
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
        if (statusCode >= StatusCodes.Status500InternalServerError)
        {
            _logger.LogError(exception, "Request failed with trace id {TraceId}", traceId);
        }
        else
        {
            _logger.LogInformation(
                "Request returned {StatusCode} ({ErrorCode}) with trace id {TraceId}: {Message}",
                statusCode,
                errorCode,
                traceId,
                message);
        }

        if (context.Response.HasStarted)
        {
            throw exception;
        }

        context.Response.Clear();
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";

        var response = ApiResponse<ErrorResponse>.Failure(
            message,
            errorCode,
            new ErrorResponse
        {
            TraceId = traceId,
            ErrorCode = errorCode,
            Details = _environment.IsDevelopment() ? exception.ToString() : null
        });

        await context.Response.WriteAsJsonAsync(response);
    }
}
