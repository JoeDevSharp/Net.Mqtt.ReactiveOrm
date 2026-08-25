using System.Collections.ObjectModel;
using Net.Mqtt.ReactiveOrm.Contracts;

namespace Net.Mqtt.ReactiveOrm.CloudEvents;

public sealed record CloudEventMessage<TData>
{
    public required string SpecVersion { get; init; }
    public required string Id { get; init; }
    public required Uri Source { get; init; }
    public required string Type { get; init; }
    public string? Subject { get; init; }
    public DateTimeOffset? Time { get; init; }
    public string? DataContentType { get; init; }
    public Uri? DataSchema { get; init; }
    public required TData Data { get; init; }
    public CloudEventExtensions Extensions { get; init; } = new();
    public CloudEventIdentity Identity => new(Source, Id);
}

public sealed record CloudEventIdentity(Uri Source, string Id);

public sealed class InvalidMqttCloudEventException(string topic, string? contentType, Exception inner)
    : ContractValidationException(
        $"MQTT message on topic '{topic}' is not a valid structured CloudEvent 1.0 (Content Type: '{contentType ?? "implicit/MQTT 3.1.1"}').",
        inner);

public sealed record CloudEventEnvelope(
    string SpecVersion,
    string Id,
    Uri Source,
    string Type,
    string? Subject,
    DateTimeOffset? Time,
    string DataContentType,
    Uri? DataSchema,
    CloudEventExtensions Extensions,
    ReadOnlyMemory<byte> Data)
{
    public CloudEventIdentity Identity => new(Source, Id);
    public CloudEventMessage<TData> WithData<TData>(TData data) => new()
    {
        SpecVersion = SpecVersion, Id = Id, Source = Source, Type = Type, Subject = Subject, Time = Time,
        DataContentType = DataContentType, DataSchema = DataSchema, Extensions = Extensions, Data = data
    };
}

public sealed record CloudEventExtensions
{
    public string? CorrelationId { get; init; }
    public string? CausationId { get; init; }
    public string? TraceParent { get; init; }
    public string? TraceState { get; init; }
    public string? NegotiationId { get; init; }
    public DateTimeOffset? ExpiresAt { get; init; }
    public IReadOnlyDictionary<string, string> Additional { get; init; } =
        ReadOnlyDictionary<string, string>.Empty;
}

public sealed record CloudEventDescriptor(
    Uri Source,
    string Type,
    string DataContentType = "application/json",
    Uri? DataSchema = null,
    string? Subject = null);

public sealed record CloudEventPublishContext
{
    public string? Id { get; init; }
    public string? Subject { get; init; }
    public DateTimeOffset? Time { get; init; }
    public CloudEventExtensions Extensions { get; init; } = new();
}

public interface ICloudEventFactory
{
    CloudEventMessage<TData> Create<TData>(TData data, CloudEventDescriptor descriptor, CloudEventPublishContext context);
}

public interface ICloudEventCodec
{
    ReadOnlyMemory<byte> Serialize<TData>(CloudEventMessage<TData> message);
    ReadOnlyMemory<byte> Serialize<TData>(CloudEventMessage<TData> message, ReadOnlyMemory<byte> serializedData);
    ReadOnlyMemory<byte> SerializeData<TData>(TData data);
    CloudEventEnvelope ReadEnvelope(ReadOnlyMemory<byte> payload, string? contentType);
    CloudEventMessage<TData> Deserialize<TData>(ReadOnlyMemory<byte> payload, string? contentType);
}
