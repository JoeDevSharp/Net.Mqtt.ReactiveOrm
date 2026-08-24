using MQTTnet;
using Net.Mqtt.ReactiveOrm.Bus.Interfaces;
using Net.Mqtt.ReactiveOrm.Enums;
using Net.Mqtt.ReactiveOrm.Models;
using System.Collections.Concurrent;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace Net.Mqtt.ReactiveOrm.Bus;

/// <summary>MQTTnet-only implementation of the injectable transport boundary.</summary>
public sealed class MqttNetBus : IMqttBus
{
    private readonly IMqttClient _client;
    private readonly MqttClientOptions _options;
    private readonly SemaphoreSlim _lifecycle = new(1, 1);
    private readonly ConcurrentDictionary<Guid, Subscriber> _subscribers = new();
    private int _state = (int)ConnectionState.Disconnected;

    public MqttNetBus(MqttClientOptions options)
        : this(new MqttClientFactory().CreateMqttClient(), options)
    {
    }

    public MqttNetBus(IMqttClient client, MqttClientOptions options)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _client.ApplicationMessageReceivedAsync += OnMessageAsync;
        _client.DisconnectedAsync += _ => { Volatile.Write(ref _state, (int)ConnectionState.Disconnected); return Task.CompletedTask; };
    }

    public ConnectionState State => (ConnectionState)Volatile.Read(ref _state);

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        await _lifecycle.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_client.IsConnected) { Volatile.Write(ref _state, (int)ConnectionState.Connected); return; }
            Volatile.Write(ref _state, (int)ConnectionState.Connecting);
            try
            {
                await _client.ConnectAsync(_options, cancellationToken).ConfigureAwait(false);
                Volatile.Write(ref _state, (int)ConnectionState.Connected);
            }
            catch { Volatile.Write(ref _state, (int)ConnectionState.Faulted); throw; }
        }
        finally { _lifecycle.Release(); }
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        await _lifecycle.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!_client.IsConnected) { Volatile.Write(ref _state, (int)ConnectionState.Disconnected); return; }
            Volatile.Write(ref _state, (int)ConnectionState.Disconnecting);
            await _client.DisconnectAsync(new MqttClientDisconnectOptions(), cancellationToken).ConfigureAwait(false);
            Volatile.Write(ref _state, (int)ConnectionState.Disconnected);
        }
        finally { _lifecycle.Release(); }
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
            await _client.SubscribeAsync(subscription.TopicFilter, (MQTTnet.Protocol.MqttQualityOfServiceLevel)subscription.QoS, cancellationToken).ConfigureAwait(false);
            await foreach (var delivery in subscriber.Channel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false)) yield return delivery;
        }
        finally
        {
            _subscribers.TryRemove(id, out _);
            if (_client.IsConnected && !_subscribers.Values.Any(x => x.Filter == subscription.TopicFilter))
                await _client.UnsubscribeAsync(subscription.TopicFilter, CancellationToken.None).ConfigureAwait(false);
        }
    }

    public async Task<MqttPublishResult> PublishAsync(MqttPublication publication, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(publication);
        await ConnectAsync(cancellationToken).ConfigureAwait(false);
        var message = new MqttApplicationMessageBuilder().WithTopic(publication.Topic)
            .WithPayload(publication.Payload.ToArray()).WithQualityOfServiceLevel((MQTTnet.Protocol.MqttQualityOfServiceLevel)publication.QoS)
            .WithRetainFlag(publication.Retain).Build();
        var result = await _client.PublishAsync(message, cancellationToken).ConfigureAwait(false);
        return new(result.IsSuccess, result.ReasonString);
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var subscriber in _subscribers.Values) subscriber.Channel.Writer.TryComplete();
        await DisconnectAsync().ConfigureAwait(false);
        _client.ApplicationMessageReceivedAsync -= OnMessageAsync;
        _client.Dispose();
        _lifecycle.Dispose();
    }

    private async Task OnMessageAsync(MqttApplicationMessageReceivedEventArgs args)
    {
        var message = args.ApplicationMessage;
        args.AutoAcknowledge = false;
        var subscribers = _subscribers.Values
            .Where(subscriber => MqttTopicFilterComparer.Compare(message.Topic, subscriber.Filter) == MqttTopicFilterCompareResult.IsMatch)
            .ToArray();
        if (subscribers.Length == 0)
        {
            await args.AcknowledgeAsync(CancellationToken.None).ConfigureAwait(false);
            return;
        }

        var acknowledgement = new SharedAcknowledgement(subscribers.Length, args.AcknowledgeAsync);
        var delivery = new MqttDelivery(message.Topic, message.Payload.ToArray(), (QoSLevel)message.QualityOfServiceLevel,
            message.Retain, acknowledgement.AcknowledgeAsync);
        foreach (var subscriber in subscribers)
            await subscriber.Channel.Writer.WriteAsync(delivery).ConfigureAwait(false);
    }

    private sealed record Subscriber(string Filter, Channel<MqttDelivery> Channel);

    private sealed class SharedAcknowledgement(int remaining, Func<CancellationToken, Task> acknowledge)
    {
        private int _remaining = remaining;
        public Task AcknowledgeAsync(CancellationToken cancellationToken) =>
            Interlocked.Decrement(ref _remaining) == 0 ? acknowledge(cancellationToken) : Task.CompletedTask;
    }
}
