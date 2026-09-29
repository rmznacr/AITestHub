using System.Text.Json;
using System.Text.Json.Serialization;

namespace AITestHub.Tests.Infrastructure;

/// <summary>
/// JSON options for test deserialization. Matches the API's settings:
/// enums are serialized as strings, property names are case-insensitive.
/// </summary>
public static class TestJsonOptions
{
    public static JsonSerializerOptions Default { get; } = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };
}
