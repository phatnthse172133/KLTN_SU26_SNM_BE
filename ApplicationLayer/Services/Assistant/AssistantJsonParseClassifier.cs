using System.Text.Json;
using ApplicationLayer.Exceptions;
using Microsoft.Extensions.Logging;

namespace ApplicationLayer.Services.Assistant;

public static class AssistantJsonParseClassifier
{
    public const string OutputTruncated = "OUTPUT_TRUNCATED";
    public const string MarkdownCodeFence = "MARKDOWN_CODE_FENCE";
    public const string ExtraTextAroundJson = "EXTRA_TEXT_AROUND_JSON";
    public const string SchemaMismatch = "SCHEMA_MISMATCH";
    public const string MissingRequiredField = "MISSING_REQUIRED_FIELD";
    public const string WrongFieldType = "WRONG_FIELD_TYPE";
    public const string InvalidEnum = "INVALID_ENUM";
    public const string EmptyProviderResponse = "EMPTY_PROVIDER_RESPONSE";
    public const string MalformedJson = "MALFORMED_JSON";
    public const string Other = "OTHER";

    public static string Classify(
        string? content,
        string? finishReason,
        Exception? exception = null,
        bool missingRequiredField = false)
    {
        if (string.Equals(finishReason, "length", StringComparison.OrdinalIgnoreCase))
            return OutputTruncated;

        if (string.IsNullOrWhiteSpace(content))
            return EmptyProviderResponse;

        var trimmed = content.Trim();
        if (trimmed.StartsWith("```", StringComparison.Ordinal))
            return MarkdownCodeFence;

        if (HasLeadingOrTrailingText(trimmed))
            return ExtraTextAroundJson;

        if (LooksTruncated(trimmed, exception))
            return OutputTruncated;

        if (missingRequiredField)
            return MissingRequiredField;

        if (exception is JsonException json)
        {
            var message = json.Message;
            if (Contains(message, "Unknown") && Contains(message, "value"))
                return InvalidEnum;
            if (Contains(message, "could not be converted") || Contains(message, "cannot be converted"))
            {
                if (Contains(message, "List") || Contains(message, "IReadOnlyList") || Contains(message, "[]"))
                    return SchemaMismatch;
                return WrongFieldType;
            }

            if (Contains(message, "was null") || Contains(message, "required"))
                return MissingRequiredField;

            if (trimmed.StartsWith('{') || trimmed.StartsWith('['))
                return SchemaMismatch;
            return MalformedJson;
        }

        if (trimmed.StartsWith('{') || trimmed.StartsWith('['))
            return SchemaMismatch;
        return Other;
    }

    public static T DeserializeOrThrow<T>(
        LanguageModelJsonCompletion completion,
        string stage,
        ILogger? logger = null)
        where T : class
    {
        try
        {
            var parsed = JsonSerializer.Deserialize<T>(completion.Content, AssistantJson.Options);
            if (parsed is null)
                throw AssistantErrors.InvalidProviderJson(
                    stage,
                    completion,
                    MissingRequiredField,
                    new JsonException("Payload was null."),
                    logger);
            return parsed;
        }
        catch (AppException)
        {
            throw;
        }
        catch (JsonException exception)
        {
            throw AssistantErrors.InvalidProviderJson(
                stage,
                completion,
                Classify(completion.Content, completion.FinishReason, exception),
                exception,
                logger);
        }
    }

    private static bool HasLeadingOrTrailingText(string trimmed)
    {
        var start = IndexOfJsonStart(trimmed);
        if (start < 0)
            return false;
        if (start > 0)
        {
            var possible = trimmed[start..];
            return !IsUnbalanced(possible);
        }

        return HasTrailingTextAfterJson(trimmed);
    }

    private static int IndexOfJsonStart(string value)
    {
        var objectStart = value.IndexOf('{');
        var arrayStart = value.IndexOf('[');
        if (objectStart < 0) return arrayStart;
        if (arrayStart < 0) return objectStart;
        return Math.Min(objectStart, arrayStart);
    }

    private static bool HasTrailingTextAfterJson(string trimmed)
    {
        if (trimmed.Length == 0 || (trimmed[0] != '{' && trimmed[0] != '['))
            return false;
        if (IsUnbalanced(trimmed))
            return false;

        try
        {
            using var document = JsonDocument.Parse(trimmed);
            return false;
        }
        catch (JsonException)
        {
            return trimmed.TrimEnd().Length > 0 && !trimmed.TrimEnd().EndsWith('}') && !trimmed.TrimEnd().EndsWith(']');
        }
    }

    private static bool LooksTruncated(string trimmed, Exception? exception)
    {
        if (trimmed.Length == 0 || (trimmed[0] != '{' && trimmed[0] != '['))
            return false;
        if (IsUnbalanced(trimmed))
            return true;
        var message = exception?.Message ?? string.Empty;
        return Contains(message, "end of data")
            || Contains(message, "end of the JSON")
            || Contains(message, "expected depth to be zero");
    }

    internal static bool IsUnbalanced(string json)
    {
        var depth = 0;
        var inString = false;
        var escaped = false;
        foreach (var ch in json)
        {
            if (inString)
            {
                if (escaped)
                {
                    escaped = false;
                    continue;
                }
                if (ch == '\\')
                {
                    escaped = true;
                    continue;
                }
                if (ch == '"')
                    inString = false;
                continue;
            }

            if (ch == '"')
            {
                inString = true;
                continue;
            }
            if (ch is '{' or '[')
                depth++;
            else if (ch is '}' or ']')
                depth--;
            if (depth < 0)
                return true;
        }

        return inString || depth != 0;
    }

    private static bool Contains(string message, string token)
        => message.Contains(token, StringComparison.OrdinalIgnoreCase);
}
