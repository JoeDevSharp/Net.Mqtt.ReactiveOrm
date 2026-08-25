using System.Collections.ObjectModel;
using Net.Mqtt.Infrastructure.Contracts;

namespace Net.Mqtt.Infrastructure.CloudEvents;

/// <summary>Represents a typed CloudEvents 1.0 message in structured content mode.</summary>
/// <typeparam name="TData">The type of the CloudEvent data.</typeparam>
public sealed record CloudEventMessage<TData>
{
    /// <summary>Gets the CloudEvents specification version.</summary>
    public required string SpecVersion { get; init; }
    /// <summary>Gets the event identifier, unique within its source.</summary>
    public required string Id { get; init; }
    /// <summary>Gets the URI identifying the event producer.</summary>
    public required Uri Source { get; init; }
    /// <summary>Gets the governed event type.</summary>
    public required string Type { get; init; }
    /// <summary>Gets the optional subject within the event source.</summary>
    public string? Subject { get; init; }
    /// <summary>Gets the time at which the event occurred.</summary>
    public DateTimeOffset? Time { get; init; }
    /// <summary>Gets the media type of the data value.</summary>
    public string? DataContentType { get; init; }
    /// <summary>Gets the schema URI governing the data value.</summary>
    public Uri? DataSchema { get; init; }
    /// <summary>Gets the typed business data.</summary>
    public required TData Data { get; init; }
    /// <summary>Gets the standard and custom extension attributes.</summary>
    public CloudEventExtensions Extensions { get; init; } = new();
    /// <summary>Gets the composite event identity formed from source and identifier.</summary>
    public CloudEventIdentity Identity => new(Source, Id);
}

/// <summary>Identifies a CloudEvent by its source and source-scoped identifier.</summary>
/// <param name="Source">The event source.</param>
/// <param name="Id">The source-scoped event identifier.</param>
public sealed record CloudEventIdentity(Uri Source, string Id);

/// <summary>Represents an MQTT payload that is not a valid structured CloudEvent.</summary>
/// <param name="topic">The topic on which the invalid payload arrived.</param>
/// <param name="contentType">The declared MQTT content type.</param>
/// <param name="inner">The codec error that explains why validation failed.</param>
public sealed class InvalidMqttCloudEventException(string topic, string? contentType, Exception inner)
    : ContractValidationException(
        $"MQTT message on topic '{topic}' is not a valid structured CloudEvent 1.0 (Content Type: '{contentType ?? "implicit/MQTT 3.1.1"}').",
        inner);

/// <summary>Represents validated CloudEvent metadata and raw JSON data.</summary>
/// <param name="SpecVersion">The CloudEvents specification version.</param>
/// <param name="Id">The source-scoped event identifier.</param>
/// <param name="Source">The event source.</param>
/// <param name="Type">The governed event type.</param>
/// <param name="Subject">The optional event subject.</param>
/// <param name="Time">The optional occurrence time.</param>
/// <param name="DataContentType">The media type of the data.</param>
/// <param name="DataSchema">The optional schema URI.</param>
/// <param name="Extensions">The extension attributes.</param>
/// <param name="Data">The raw JSON data bytes.</param>
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
    /// <summary>Gets the composite event identity.</summary>
    public CloudEventIdentity Identity => new(Source, Id);
    /// <summary>Creates a typed CloudEvent by attaching deserialized data to this envelope.</summary>
    public CloudEventMessage<TData> WithData<TData>(TData data) => new()
    {
        SpecVersion = SpecVersion, Id = Id, Source = Source, Type = Type, Subject = Subject, Time = Time,
        DataContentType = DataContentType, DataSchema = DataSchema, Extensions = Extensions, Data = data
    };
}

/// <summary>Contains common CloudEvent extension attributes.</summary>
public sealed record CloudEventExtensions
{
    /// <summary>Gets the end-to-end correlation identifier.</summary>
    public string? CorrelationId { get; init; }
    /// <summary>Gets the identifier of the event or command that caused this event.</summary>
    public string? CausationId { get; init; }
    /// <summary>Gets the W3C trace parent value.</summary>
    public string? TraceParent { get; init; }
    /// <summary>Gets the W3C trace state value.</summary>
    public string? TraceState { get; init; }
    /// <summary>Gets the capability negotiation identifier.</summary>
    public string? NegotiationId { get; init; }
    /// <summary>Gets the functional expiration time.</summary>
    public DateTimeOffset? ExpiresAt { get; init; }
    /// <summary>Gets additional lowercase CloudEvent extension attributes.</summary>
    public IReadOnlyDictionary<string, string> Additional { get; init; } =
        ReadOnlyDictionary<string, string>.Empty;
}

/// <summary>Defines the governed CloudEvent attributes for a topic set.</summary>
/// <param name="Source">The producer source URI.</param>
/// <param name="Type">The governed event type.</param>
/// <param name="DataContentType">The media type used for data.</param>
/// <param name="DataSchema">The optional governed schema URI.</param>
/// <param name="Subject">The optional default event subject.</param>
public sealed record CloudEventDescriptor(
    Uri Source,
    string Type,
    string DataContentType = "application/json",
    Uri? DataSchema = null,
    string? Subject = null);

/// <summary>Supplies per-publication CloudEvent attributes.</summary>
public sealed record CloudEventPublishContext
{
    /// <summary>Gets an optional caller-provided event identifier.</summary>
    public string? Id { get; init; }
    /// <summary>Gets the concrete functional subject.</summary>
    public string? Subject { get; init; }
    /// <summary>Gets the event occurrence time.</summary>
    public DateTimeOffset? Time { get; init; }
    /// <summary>Gets extension attributes for the publication.</summary>
    public CloudEventExtensions Extensions { get; init; } = new();
}

/// <summary>Creates valid typed CloudEvents from governed descriptors and publication context.</summary>
public interface ICloudEventFactory
{
    /// <summary>Creates a CloudEvent around the supplied data.</summary>
    CloudEventMessage<TData> Create<TData>(TData data, CloudEventDescriptor descriptor, CloudEventPublishContext context);
}

/// <summary>Serializes and validates CloudEvents 1.0 in structured JSON content mode.</summary>
public interface ICloudEventCodec
{
    /// <summary>Serializes a typed CloudEvent and its data.</summary>
    ReadOnlyMemory<byte> Serialize<TData>(CloudEventMessage<TData> message);
    /// <summary>Serializes a typed CloudEvent using pre-serialized governed data.</summary>
    ReadOnlyMemory<byte> Serialize<TData>(CloudEventMessage<TData> message, ReadOnlyMemory<byte> serializedData);
    /// <summary>Serializes a data value using the common deterministic JSON profile.</summary>
    ReadOnlyMemory<byte> SerializeData<TData>(TData data);
    /// <summary>Validates and reads CloudEvent metadata while preserving raw data bytes.</summary>
    CloudEventEnvelope ReadEnvelope(ReadOnlyMemory<byte> payload, string? contentType);
    /// <summary>Deserializes a structured payload into a typed CloudEvent.</summary>
    CloudEventMessage<TData> Deserialize<TData>(ReadOnlyMemory<byte> payload, string? contentType);
}
