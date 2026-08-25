using Net.Mqtt.Infrastructure.Interfaces;
using System.Text.Json;

namespace Net.Mqtt.Infrastructure;

/// <summary>Represents mqtt serializer.</summary>
public sealed class MqttSerializer : IMqttCodec
{
    private readonly JsonSerializerOptions _options;
    /// <summary>Executes the mqtt serializer operation.</summary>
    public MqttSerializer(JsonSerializerOptions? options = null) =>
        _options = options ?? new JsonSerializerOptions(JsonSerializerDefaults.Web);
    /// <summary>Serializes the encode&lt;t&gt; operation.</summary>
    public ReadOnlyMemory<byte> Encode<T>(T value) =>
        JsonSerializer.SerializeToUtf8Bytes(value ?? throw new ArgumentNullException(nameof(value)), _options);
    /// <summary>Deserializes the decode&lt;t&gt; operation.</summary>
    public T Decode<T>(ReadOnlyMemory<byte> payload) =>
        JsonSerializer.Deserialize<T>(payload.Span, _options)
        ?? throw new InvalidOperationException($"Failed to deserialize payload as {typeof(T).Name}.");
    /// <summary>Serializes the serialize&lt;t&gt; operation.</summary>
    public string Serialize<T>(T message) => JsonSerializer.Serialize(message, _options);
    /// <summary>Deserializes the deserialize&lt;t&gt; operation.</summary>
    public T Deserialize<T>(string payload) => JsonSerializer.Deserialize<T>(payload, _options)
        ?? throw new InvalidOperationException("Failed to deserialize payload.");
}
