namespace ApplicationLayer.Exceptions;

public class AppException : Exception
{
    public int StatusCode { get; }
    public string ErrorCode { get; }

    public AppException(string message, int statusCode = 400, string errorCode = "APP_ERROR")
        : base(message)
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
}
