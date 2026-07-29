namespace ApplicationLayer.Exceptions;

public class AppException : Exception
{
    public int StatusCode { get; }
    public string ErrorCode { get; }

    public AppException(
        string message,
        int statusCode = 400,
        string errorCode = "APP_ERROR",
        Exception? innerException = null)
        : base(message, innerException)
    {
        StatusCode = statusCode;
        ErrorCode = errorCode;
    }

    public static AppException BadRequest(string message, string errorCode = "BAD_REQUEST")
        => new(message, 400, errorCode);

    public static AppException Unauthorized(string message, string errorCode = "UNAUTHORIZED")
        => new(message, 401, errorCode);

    public static AppException Forbidden(string message, string errorCode = "FORBIDDEN")
        => new(message, 403, errorCode);

    public static AppException NotFound(string message, string errorCode = "NOT_FOUND")
        => new(message, 404, errorCode);

    public static AppException Conflict(string message, string errorCode = "CONFLICT")
        => new(message, 409, errorCode);

    public static AppException UnprocessableEntity(string message, string errorCode = "BUSINESS_VALIDATION_FAILED")
        => new(message, 422, errorCode);

    public static AppException ServiceUnavailable(
        string message,
        string errorCode = "SERVICE_UNAVAILABLE",
        Exception? innerException = null)
        => new(message, 503, errorCode, innerException);

    public static AppException BadGateway(
        string message,
        string errorCode = "BAD_GATEWAY",
        Exception? innerException = null)
        => new(message, 502, errorCode, innerException);

    public static AppException PayloadTooLarge(string message, string errorCode = "PAYLOAD_TOO_LARGE")
        => new(message, 413, errorCode);
}
