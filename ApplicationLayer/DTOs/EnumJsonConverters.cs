using System.Text.Json;
using System.Text.Json.Serialization;

namespace ApplicationLayer.DTOs;

public sealed class StrictEnumJsonConverter<TEnum> : JsonConverter<TEnum> where TEnum : struct, Enum
{
    public override TEnum Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
        => reader.TokenType == JsonTokenType.String && Enum.TryParse<TEnum>(reader.GetString(), false, out var value) && Enum.IsDefined(value)
            ? value : throw new JsonException($"Unknown {typeof(TEnum).Name} value.");
    public override void Write(Utf8JsonWriter writer, TEnum value, JsonSerializerOptions options) => writer.WriteStringValue(value.ToString());
}

public sealed class NullableEnumJsonConverter<TEnum> : JsonConverter<TEnum?> where TEnum : struct, Enum
{
    public override TEnum? Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
        => reader.TokenType == JsonTokenType.Null ? null
            : reader.TokenType == JsonTokenType.String && Enum.TryParse<TEnum>(reader.GetString(), false, out var value) && Enum.IsDefined(value)
                ? value : throw new JsonException($"Unknown {typeof(TEnum).Name} value.");
    public override void Write(Utf8JsonWriter writer, TEnum? value, JsonSerializerOptions options)
    { if (value.HasValue) writer.WriteStringValue(value.Value.ToString()); else writer.WriteNullValue(); }
}

public sealed class EnumCollectionJsonConverter<TEnum> : JsonConverter<IReadOnlyCollection<TEnum>> where TEnum : struct, Enum
{
    public override IReadOnlyCollection<TEnum> Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartArray) throw new JsonException("Expected an enum array.");
        var result = new List<TEnum>();
        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            if (reader.TokenType != JsonTokenType.String || !Enum.TryParse<TEnum>(reader.GetString(), false, out var value) || !Enum.IsDefined(value)) throw new JsonException($"Unknown {typeof(TEnum).Name} value.");
            result.Add(value);
        }
        return result;
    }
    public override void Write(Utf8JsonWriter writer, IReadOnlyCollection<TEnum> values, JsonSerializerOptions options)
    { writer.WriteStartArray(); foreach (var value in values) writer.WriteStringValue(value.ToString()); writer.WriteEndArray(); }
}
