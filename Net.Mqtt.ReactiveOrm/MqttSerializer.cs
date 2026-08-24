using Net.Mqtt.ReactiveOrm.Interfaces;
using System.Text.Json;

namespace Net.Mqtt.ReactiveOrm;

public sealed class MqttSerializer : IMqttCodec
{
    private readonly JsonSerializerOptions _options;
    public MqttSerializer(JsonSerializerOptions? options = null) =>
        _options = options ?? new JsonSerializerOptions(JsonSerializerDefaults.Web);
    public ReadOnlyMemory<byte> Encode<T>(T value) =>
        JsonSerializer.SerializeToUtf8Bytes(value ?? throw new ArgumentNullException(nameof(value)), _options);
    public T Decode<T>(ReadOnlyMemory<byte> payload) =>
        JsonSerializer.Deserialize<T>(payload.Span, _options)
        ?? throw new InvalidOperationException($"Failed to deserialize payload as {typeof(T).Name}.");
    public string Serialize<T>(T message) => JsonSerializer.Serialize(message, _options);
    public T Deserialize<T>(string payload) => JsonSerializer.Deserialize<T>(payload, _options)
        ?? throw new InvalidOperationException("Failed to deserialize payload.");
}
