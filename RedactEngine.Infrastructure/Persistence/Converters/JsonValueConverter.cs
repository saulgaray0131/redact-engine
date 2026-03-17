using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using System.Text.Json;

namespace RedactEngine.Infrastructure.Persistence.Converters;

/// <summary>
/// Generic EF Core value converter that serializes/deserializes objects as JSON.
/// Useful for storing complex types (dictionaries, nested objects) as JSONB columns.
/// </summary>
/// <typeparam name="T">The type to convert to/from JSON.</typeparam>
public class JsonValueConverter<T> : ValueConverter<T, string> where T : class
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };

    public JsonValueConverter() : base(
        v => Serialize(v),
        v => Deserialize(v))
    {
    }

    private static string Serialize(T? value)
    {
        return value is null ? "{}" : JsonSerializer.Serialize(value, JsonOptions);
    }

    private static T Deserialize(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Activator.CreateInstance<T>();
        }

        return JsonSerializer.Deserialize<T>(json, JsonOptions) ?? Activator.CreateInstance<T>();
    }
}

/// <summary>
/// EF Core value converter for nullable types that serializes/deserializes objects as JSON.
/// </summary>
/// <typeparam name="T">The type to convert to/from JSON.</typeparam>
public class NullableJsonValueConverter<T> : ValueConverter<T?, string?> where T : class
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };

    public NullableJsonValueConverter() : base(
        v => v == null ? null : JsonSerializer.Serialize(v, JsonOptions),
        v => string.IsNullOrWhiteSpace(v) ? null : JsonSerializer.Deserialize<T>(v, JsonOptions))
    {
    }
}
