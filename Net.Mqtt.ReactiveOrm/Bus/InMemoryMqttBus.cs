using MQTTnet;
using Net.Mqtt.ReactiveOrm.Bus.Interfaces;
using Net.Mqtt.ReactiveOrm.Models;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace Net.Mqtt.ReactiveOrm.Bus;

/// <summary>A deterministic process-local transport for tests and development.</summary>
public sealed class InMemoryMqttBus : IMqttBus
{
    private readonly ConcurrentDictionary<Guid, Subscriber> _subscribers = new();
    private volatile ConnectionState _state = ConnectionState.Created;
    public ConnectionState State => _state;
    public bool IsReady => State == ConnectionState.Ready;
    public bool WasSessionRestored => false;
    public event EventHandler<ConnectionStateChanged>? StateChanged;

    public Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        SetState(ConnectionState.Ready);
        return Task.CompletedTask;
    }

    public Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        SetState(ConnectionState.Stopped);
        return Task.CompletedTask;
    }

    public async IAsyncEnumerable<MqttDelivery> SubscribeAsync(MqttSubscription subscription, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(subscription);
        await ConnectAsync(cancellationToken).ConfigureAwait(false);
        var id = Guid.NewGuid();
        if (subscription.Capacity <= 0) throw new ArgumentOutOfRangeException(nameof(subscription));
        var subscriber = new Subscriber(subscription.TopicFilter, Channel.CreateBounded<MqttDelivery>(new BoundedChannelOptions(subscription.Capacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true
        }));
        _subscribers[id] = subscriber;
        try
        {
            await foreach (var delivery in subscriber.Channel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false)) yield return delivery;
        }
        finally { _subscribers.TryRemove(id, out _); }
    }

    public async Task<MqttPublishResult> PublishAsync(MqttPublication publication, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(publication);
        await ConnectAsync(cancellationToken).ConfigureAwait(false);
        var delivery = new MqttDelivery(publication.Topic, publication.Payload, publication.QoS, publication.Retain);
        foreach (var subscriber in _subscribers.Values)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (MqttTopicFilterComparer.Compare(publication.Topic, subscriber.Filter) == MqttTopicFilterCompareResult.IsMatch)
                await subscriber.Channel.Writer.WriteAsync(delivery, cancellationToken).ConfigureAwait(false);
        }
        return new(true);
    }

    public ValueTask DisposeAsync()
    {
        foreach (var subscriber in _subscribers.Values) subscriber.Channel.Writer.TryComplete();
        _subscribers.Clear();
        SetState(ConnectionState.Stopped);
        return ValueTask.CompletedTask;
    }

    private void SetState(ConnectionState state)
    {
        var previous = _state;
        _state = state;
        if (previous != state) StateChanged?.Invoke(this, new(previous, state));
    }

    private sealed record Subscriber(string Filter, Channel<MqttDelivery> Channel);
}
