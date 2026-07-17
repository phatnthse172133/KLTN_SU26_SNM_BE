using System.Collections;
using System.Reflection;
using ApplicationLayer.Helppers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace PresentationLayer.Filters;

public class EnumValidationFilter : IActionFilter
{
    private const int MaxDepth = 6;

    public void OnActionExecuting(ActionExecutingContext context)
    {
        var errors = new List<string>();
        foreach (var argument in context.ActionArguments)
            ValidateValue(argument.Value, argument.Key, errors, 0, new HashSet<object>(ReferenceEqualityComparer.Instance));

        if (errors.Count == 0)
            return;

        context.Result = new BadRequestObjectResult(ApiResponse<ErrorResponse>.Failure(
            "Request validation failed.",
            new ErrorResponse
            {
                TraceId = context.HttpContext.TraceIdentifier,
                ErrorCode = "VALIDATION_ERROR",
                Details = string.Join(" ", errors)
            }));
    }

    public void OnActionExecuted(ActionExecutedContext context)
    {
    }

    private static void ValidateValue(
        object? value,
        string path,
        ICollection<string> errors,
        int depth,
        HashSet<object> visited)
    {
        if (value is null || depth > MaxDepth)
            return;

        var type = value.GetType();
        var enumType = Nullable.GetUnderlyingType(type) ?? type;
        if (enumType.IsEnum)
        {
            if (!Enum.IsDefined(enumType, value))
                errors.Add($"{path} has an invalid value.");
            return;
        }

        if (IsSimple(type))
            return;

        if (!type.IsValueType && !visited.Add(value))
            return;

        if (value is IEnumerable enumerable and not string)
        {
            var index = 0;
            foreach (var item in enumerable)
            {
                ValidateValue(item, $"{path}[{index}]", errors, depth + 1, visited);
                index++;
            }
            return;
        }

        foreach (var property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                     .Where(property => property.GetIndexParameters().Length == 0))
        {
            ValidateValue(
                property.GetValue(value),
                $"{path}.{property.Name}",
                errors,
                depth + 1,
                visited);
        }
    }

    private static bool IsSimple(Type type)
        => type.IsPrimitive
            || type == typeof(string)
            || type == typeof(decimal)
            || type == typeof(Guid)
            || type == typeof(DateTime)
            || type == typeof(DateOnly)
            || type == typeof(TimeOnly)
            || type == typeof(CancellationToken)
            || Nullable.GetUnderlyingType(type) is { } inner && IsSimple(inner);
}
