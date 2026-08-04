using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ApplicationLayer.Serialization;

public sealed class UpperSnakeCaseEnumConverter<TEnum> : JsonConverter<TEnum> where TEnum : struct, Enum
{
    public override TEnum Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
            throw new JsonException($"{typeof(TEnum).Name} must be a string.");
        var normalized = (reader.GetString() ?? string.Empty).Replace("_", "", StringComparison.Ordinal);
        if (Enum.TryParse<TEnum>(normalized, true, out var value)) return value;
        throw new JsonException($"Invalid {typeof(TEnum).Name} value.");
    }

    public override void Write(Utf8JsonWriter writer, TEnum value, JsonSerializerOptions options)
    {
        var name = value.ToString();
        var output = new StringBuilder(name.Length + 8);
        for (var index = 0; index < name.Length; index++)
        {
            if (index > 0 && char.IsUpper(name[index]) && !char.IsUpper(name[index - 1])) output.Append('_');
            output.Append(char.ToUpperInvariant(name[index]));
        }
        writer.WriteStringValue(output.ToString());
    }
}
