using System.Text.Json;
using System.Text.Json.Serialization;

namespace Net.Mqtt.Infrastructure.Contracts;

/// <summary>Represents mqtt json profile.</summary>
public static class MqttJsonProfile
{
    /// <summary>Creates the create operation.</summary>
    public static JsonSerializerOptions Create() => new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        NumberHandling = JsonNumberHandling.Strict,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        WriteIndented = false
    };

    /// <summary>Serializes the serialize&lt;tdata&gt; operation.</summary>
    public static ReadOnlyMemory<byte> Serialize<TData>(TData data, JsonSerializerOptions? options = null)
    {
        var json = JsonSerializer.SerializeToUtf8Bytes(data, options ?? Create());
        using var document = JsonDocument.Parse(json);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream)) WriteCanonical(writer, document.RootElement);
        return stream.ToArray();
    }

    private static void WriteCanonical(Utf8JsonWriter writer, JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            writer.WriteStartObject();
            foreach (var property in value.EnumerateObject().OrderBy(x => x.Name, StringComparer.Ordinal))
            {
                writer.WritePropertyName(property.Name);
                WriteCanonical(writer, property.Value);
            }
            writer.WriteEndObject();
        }
        else if (value.ValueKind == JsonValueKind.Array)
        {
            writer.WriteStartArray();
            foreach (var item in value.EnumerateArray()) WriteCanonical(writer, item);
            writer.WriteEndArray();
        }
        else value.WriteTo(writer);
    }
}

/// <summary>Explicit adapter for generated Protobuf contracts that need a governed JSON projection.</summary>
public sealed class DelegateContractJsonMapper<TData>(
    Func<TData, ReadOnlyMemory<byte>> serialize,
    Func<ReadOnlyMemory<byte>, TData> deserialize) : IContractJsonMapper
{
    /// <summary>Serializes the serialize operation.</summary>
    public ReadOnlyMemory<byte> Serialize(object data, Type dataType)
    {
        if (dataType != typeof(TData) || data is not TData typed) throw new ArgumentException("Mapper data type mismatch.");
        return serialize(typed);
    }

    /// <summary>Deserializes the deserialize operation.</summary>
    public object Deserialize(ReadOnlyMemory<byte> json, Type dataType)
    {
        if (dataType != typeof(TData)) throw new ArgumentException("Mapper data type mismatch.");
        return deserialize(json)!;
    }
}
