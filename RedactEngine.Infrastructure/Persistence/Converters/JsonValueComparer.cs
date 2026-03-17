using Microsoft.EntityFrameworkCore.ChangeTracking;
using System.Text.Json;

namespace RedactEngine.Infrastructure.Persistence.Converters;

/// <summary>
/// Provides JSON-based value comparers for EF Core collections stored as JSONB.
/// </summary>
public static class JsonValueComparer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };

    public static ValueComparer<T> Create<T>() where T : class
    {
        return new ValueComparer<T>(
            (left, right) => AreEqual(left, right),
            value => GetHashCode(value),
            value => Snapshot(value)!);
    }

    public static ValueComparer<T?> CreateNullable<T>() where T : class
    {
        return new ValueComparer<T?>(
            (left, right) => AreEqual(left, right),
            value => GetHashCode(value),
            value => Snapshot(value));
    }

    private static bool AreEqual<T>(T? left, T? right) where T : class
    {
        if (ReferenceEquals(left, right))
        {
            return true;
        }

        if (left is null || right is null)
        {
            return false;
        }

        return Serialize(left) == Serialize(right);
    }

    private static int GetHashCode<T>(T? value) where T : class
    {
        if (value is null)
        {
            return 0;
        }

        return Serialize(value).GetHashCode(StringComparison.Ordinal);
    }

    private static T? Snapshot<T>(T? value) where T : class
    {
        if (value is null)
        {
            return null;
        }

        var json = Serialize(value);
        return Deserialize<T>(json);
    }

    private static string Serialize<T>(T value) where T : class
    {
        return JsonSerializer.Serialize(value, JsonOptions);
    }

    private static T Deserialize<T>(string json) where T : class
    {
        return JsonSerializer.Deserialize<T>(json, JsonOptions) ?? Activator.CreateInstance<T>();
    }
}
