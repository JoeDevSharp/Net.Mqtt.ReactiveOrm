using Net.Mqtt.ReactiveOrm.Enums;

namespace Net.Mqtt.ReactiveOrm.Models;

public enum ConnectionState { Created, Connecting, Connected, Subscribing, Ready, Reconnecting, Draining, Stopped, Faulted }
public sealed record ConnectionStateChanged(ConnectionState Previous, ConnectionState Current, Exception? Error = null);
public sealed record MqttSubscription(string TopicFilter, QoSLevel QoS = QoSLevel.AtMostOnce, int Capacity = 128);
public sealed record MqttPublication(string Topic, ReadOnlyMemory<byte> Payload, QoSLevel QoS = QoSLevel.AtMostOnce, bool Retain = false);
public sealed record MqttDelivery(
    string Topic,
    ReadOnlyMemory<byte> Payload,
    QoSLevel QoS,
    bool Retain,
    Func<CancellationToken, Task>? Acknowledge = null)
{
    public Task AcknowledgeAsync(CancellationToken cancellationToken = default) =>
        Acknowledge?.Invoke(cancellationToken) ?? Task.CompletedTask;
}
public sealed record MqttPublishResult(bool IsSuccess, string? Reason = null);

public sealed record CloudEventPublishOptions
{
    public static CloudEventPublishOptions Default { get; } = new();
    public QoSLevel? QoS { get; init; }
    public bool? Retain { get; init; }
}

public sealed record SubscriptionOptions
{
    public static SubscriptionOptions Default { get; } = new();
    public QoSLevel? QoS { get; init; }
    public int Capacity { get; init; } = 128;
}

public sealed class MqttMessageContext<TData>
{
    private readonly Func<CancellationToken, Task> _acknowledge;
    private int _acknowledged;

    internal MqttMessageContext(TData data, MqttDelivery delivery)
    {
        Data = data;
        Topic = delivery.Topic;
        QoS = delivery.QoS;
        Retain = delivery.Retain;
        _acknowledge = delivery.AcknowledgeAsync;
    }

    public TData Data { get; }
    public string Topic { get; }
    public QoSLevel QoS { get; }
    public bool Retain { get; }
    public bool IsAcknowledged => Volatile.Read(ref _acknowledged) != 0;

    public Task AcknowledgeAsync(CancellationToken cancellationToken = default) =>
        Interlocked.Exchange(ref _acknowledged, 1) == 0 ? _acknowledge(cancellationToken) : Task.CompletedTask;
}
