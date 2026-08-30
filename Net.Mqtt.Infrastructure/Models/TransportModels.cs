using Net.Mqtt.Infrastructure.Enums;
using Net.Mqtt.Infrastructure.CloudEvents;

namespace Net.Mqtt.Infrastructure.Models;

/// <summary>Defines the states of the MQTT connection lifecycle.</summary>
public enum ConnectionState
{
    /// <summary>The bus has been created but has not started connecting.</summary>
    Created,
    /// <summary>The bus is establishing a connection.</summary>
    Connecting,
    /// <summary>The transport connection is established.</summary>
    Connected,
    /// <summary>The bus is restoring subscriptions.</summary>
    Subscribing,
    /// <summary>The bus is connected and ready to receive messages.</summary>
    Ready,
    /// <summary>The bus is recovering a lost connection.</summary>
    Reconnecting,
    /// <summary>The bus is completing in-flight work before shutdown.</summary>
    Draining,
    /// <summary>The bus has stopped.</summary>
    Stopped,
    /// <summary>The bus encountered a terminal failure.</summary>
    Faulted
}
/// <summary>Describes a connection lifecycle transition.</summary>
/// <param name="Previous">The state before the transition.</param>
/// <param name="Current">The state after the transition.</param>
/// <param name="Error">The optional error that caused the transition.</param>
public sealed record ConnectionStateChanged(ConnectionState Previous, ConnectionState Current, Exception? Error = null);
/// <summary>Describes an MQTT subscription request.</summary>
/// <param name="TopicFilter">The MQTT subscription filter.</param>
/// <param name="QoS">The requested quality of service.</param>
/// <param name="Capacity">The bounded delivery buffer capacity.</param>
public sealed record MqttSubscription(string TopicFilter, QoSLevel QoS = QoSLevel.AtMostOnce, int Capacity = 128)
{
    private readonly TaskCompletionSource _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    /// <summary>Waits until the transport has installed and confirmed the subscription.</summary>
    public Task WaitUntilReadyAsync(CancellationToken cancellationToken = default) =>
        _ready.Task.WaitAsync(cancellationToken);
    internal void MarkReady() => _ready.TrySetResult();
    internal void MarkFailed(Exception exception) => _ready.TrySetException(exception);
}
/// <summary>Describes an encoded MQTT publication.</summary>
/// <param name="Topic">The concrete publication topic.</param>
/// <param name="Payload">The encoded payload.</param>
/// <param name="QoS">The publication quality of service.</param>
/// <param name="Retain">Whether the broker should retain the message.</param>
/// <param name="ContentType">The optional MQTT 5 content type.</param>
public sealed record MqttPublication(string Topic, ReadOnlyMemory<byte> Payload, QoSLevel QoS = QoSLevel.AtMostOnce,
    bool Retain = false, string? ContentType = null);
/// <summary>Represents a message delivered by an MQTT transport.</summary>
/// <param name="Topic">The concrete topic on which the message arrived.</param>
/// <param name="Payload">The encoded payload.</param>
/// <param name="QoS">The delivery quality of service.</param>
/// <param name="Retain">Whether the message was retained.</param>
/// <param name="ContentType">The optional MQTT 5 content type.</param>
/// <param name="Acknowledge">The transport acknowledgement callback.</param>
public sealed record MqttDelivery(
    string Topic,
    ReadOnlyMemory<byte> Payload,
    QoSLevel QoS,
    bool Retain,
    string? ContentType = null,
    Func<CancellationToken, Task>? Acknowledge = null)
{
    /// <summary>Acknowledges the transport delivery.</summary>
    public Task AcknowledgeAsync(CancellationToken cancellationToken = default) =>
        Acknowledge?.Invoke(cancellationToken) ?? Task.CompletedTask;
}
/// <summary>Reports the outcome of an MQTT publication.</summary>
/// <param name="IsSuccess">Whether the broker accepted the publication.</param>
/// <param name="Reason">An optional broker reason.</param>
public sealed record MqttPublishResult(bool IsSuccess, string? Reason = null);

/// <summary>Configures a typed CloudEvent publication.</summary>
public sealed record CloudEventPublishOptions
{
    /// <summary>Gets the default publication options.</summary>
    public static CloudEventPublishOptions Default { get; } = new();
    /// <summary>Gets the optional quality-of-service override.</summary>
    public QoSLevel? QoS { get; init; }
    /// <summary>Gets the optional retained-message override.</summary>
    public bool? Retain { get; init; }
    /// <summary>Gets CloudEvent attributes supplied by the caller.</summary>
    public CloudEventPublishContext Context { get; init; } = new();
}

/// <summary>Configures asynchronous subscription buffering and quality of service.</summary>
public sealed record SubscriptionOptions
{
    /// <summary>Gets the default subscription options.</summary>
    public static SubscriptionOptions Default { get; } = new();
    /// <summary>Gets the optional quality-of-service override.</summary>
    public QoSLevel? QoS { get; init; }
    /// <summary>Gets the bounded delivery buffer capacity.</summary>
    public int Capacity { get; init; } = 128;
}

/// <summary>Provides validated CloudEvent data and delivery metadata to an application.</summary>
/// <typeparam name="TData">The CloudEvent data type.</typeparam>
public sealed class MqttMessageContext<TData>
{
    private readonly Func<CancellationToken, Task> _acknowledge;
    private int _acknowledged;

    internal MqttMessageContext(CloudEventMessage<TData> cloudEvent, MqttDelivery delivery)
    {
        CloudEvent = cloudEvent;
        Data = cloudEvent.Data;
        Topic = delivery.Topic;
        QoS = delivery.QoS;
        Retain = delivery.Retain;
        _acknowledge = delivery.AcknowledgeAsync;
    }

    /// <summary>Gets the validated event data.</summary>
    public TData Data { get; }
    /// <summary>Gets the complete typed CloudEvent.</summary>
    public CloudEventMessage<TData> CloudEvent { get; }
    /// <summary>Gets the idempotency identity formed from CloudEvent source and identifier.</summary>
    public CloudEventIdentity Identity => CloudEvent.Identity;
    /// <summary>Gets the topic on which the message arrived.</summary>
    public string Topic { get; }
    /// <summary>Gets the delivery quality of service.</summary>
    public QoSLevel QoS { get; }
    /// <summary>Gets a value indicating whether the message was retained.</summary>
    public bool Retain { get; }
    /// <summary>Gets a value indicating whether the delivery has been acknowledged.</summary>
    public bool IsAcknowledged => Volatile.Read(ref _acknowledged) != 0;

    /// <summary>Acknowledges the delivery once, after successful application processing.</summary>
    public Task AcknowledgeAsync(CancellationToken cancellationToken = default) =>
        Interlocked.Exchange(ref _acknowledged, 1) == 0 ? _acknowledge(cancellationToken) : Task.CompletedTask;
}
