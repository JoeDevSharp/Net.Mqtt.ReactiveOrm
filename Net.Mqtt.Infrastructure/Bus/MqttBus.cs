using MQTTnet;
using Net.Mqtt.Infrastructure.Bus.Interfaces;
using Net.Mqtt.Infrastructure.Enums;
using Net.Mqtt.Infrastructure.Models;
using Net.Mqtt.Infrastructure.Security;
using System.Collections.Concurrent;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace Net.Mqtt.Infrastructure.Bus;

/// <summary>MQTTnet-only implementation of the injectable transport boundary.</summary>
/// <summary>Implements the production MQTT transport by using MQTTnet.</summary>
public sealed class MqttNetBus : IMqttBus
{
    private readonly IMqttClient _client;
    private MqttClientOptions _options = null!;
    private readonly Func<CancellationToken, ValueTask<MqttClientOptions>> _optionsFactory;
    private readonly MqttReconnectOptions _reconnect;
    private readonly MqttLastWillOptions? _lastWill;
    private readonly string _clientId;
    private readonly MutualTlsOptions? _mutualTls;
    private readonly SemaphoreSlim _lifecycle = new(1, 1);
    private readonly CancellationTokenSource _lifetime = new();
    private readonly ConcurrentDictionary<Guid, Subscriber> _subscribers = new();
    private int _state = (int)ConnectionState.Created;
    private int _manualDisconnect;
    private Task? _reconnectTask;
    private int _certificateRotation;

    /// <inheritdoc />
    public event EventHandler<ConnectionStateChanged>? StateChanged;
    /// <inheritdoc />
    public event EventHandler<CertificateExpiringEvent>? CertificateExpiring;
    /// <inheritdoc />
    public bool IsReady => State == ConnectionState.Ready;
    /// <inheritdoc />
    public bool WasSessionRestored { get; private set; }

    /// <summary>Initializes a bus from high-level reactive ORM options.</summary>
    public MqttNetBus(MqttReactiveOrmOptions options)
        : this(new MqttClientFactory().CreateMqttClient(), options.BuildClientOptionsAsync, options.Reconnect, options.LastWill, options.ClientId, options.Security.MutualTls)
    {
    }

    /// <summary>Initializes a bus from prebuilt MQTTnet client options.</summary>
    public MqttNetBus(MqttClientOptions options)
        : this(new MqttClientFactory().CreateMqttClient(), _ => ValueTask.FromResult(options), new MqttReconnectOptions(), null, options.ClientId, null)
    {
    }

    /// <summary>Initializes a bus with an injected MQTTnet client and options.</summary>
    public MqttNetBus(IMqttClient client, MqttClientOptions options)
        : this(client, _ => ValueTask.FromResult(options), new MqttReconnectOptions(), null, options.ClientId, null)
    {
    }

    private MqttNetBus(IMqttClient client, Func<CancellationToken, ValueTask<MqttClientOptions>> optionsFactory,
        MqttReconnectOptions reconnect, MqttLastWillOptions? lastWill, string clientId, MutualTlsOptions? mutualTls)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _optionsFactory = optionsFactory ?? throw new ArgumentNullException(nameof(optionsFactory));
        _reconnect = reconnect;
        _lastWill = lastWill;
        _clientId = clientId;
        _mutualTls = mutualTls;
        _client.ApplicationMessageReceivedAsync += OnMessageAsync;
        _client.DisconnectedAsync += OnDisconnectedAsync;
        if (_mutualTls is not null)
        {
            _mutualTls.ClientCertificateProvider.CertificateChanged += OnCertificateChanged;
            _ = MonitorCertificateExpirationAsync(_lifetime.Token);
        }
    }

    /// <inheritdoc />
    public ConnectionState State => (ConnectionState)Volatile.Read(ref _state);

    /// <inheritdoc />
    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        Interlocked.Exchange(ref _manualDisconnect, 0);
        await _lifecycle.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_client.IsConnected) { SetState(ConnectionState.Ready); return; }
            SetState(ConnectionState.Connecting);
            try
            {
                _options = await _optionsFactory(cancellationToken).ConfigureAwait(false);
                var result = await _client.ConnectAsync(_options, cancellationToken).ConfigureAwait(false);
                WasSessionRestored = result.IsSessionPresent;
                SetState(ConnectionState.Connected);
                if (!WasSessionRestored) await RestoreSubscriptionsAsync(cancellationToken).ConfigureAwait(false);
                SetState(ConnectionState.Ready);
            }
            catch (Exception error) { SetState(ConnectionState.Faulted, error); throw; }
        }
        finally { _lifecycle.Release(); }
    }

    /// <inheritdoc />
    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        await _lifecycle.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            Interlocked.Exchange(ref _manualDisconnect, 1);
            if (!_client.IsConnected) { SetState(ConnectionState.Stopped); return; }
            SetState(ConnectionState.Draining);
            await PublishNormalShutdownStateAsync(cancellationToken).ConfigureAwait(false);
            await _client.DisconnectAsync(new MqttClientDisconnectOptions(), cancellationToken).ConfigureAwait(false);
            SetState(ConnectionState.Stopped);
        }
        finally { _lifecycle.Release(); }
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<MqttDelivery> SubscribeAsync(MqttSubscription subscription, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(subscription);
        await ConnectAsync(cancellationToken).ConfigureAwait(false);
        var id = Guid.NewGuid();
        if (subscription.Capacity <= 0) throw new ArgumentOutOfRangeException(nameof(subscription));
        var subscriber = new Subscriber(subscription.TopicFilter, subscription.QoS, Channel.CreateBounded<MqttDelivery>(new BoundedChannelOptions(subscription.Capacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true
        }));
        _subscribers[id] = subscriber;
        try
        {
            SetState(ConnectionState.Subscribing);
            await _client.SubscribeAsync(subscription.TopicFilter, (MQTTnet.Protocol.MqttQualityOfServiceLevel)subscription.QoS, cancellationToken).ConfigureAwait(false);
            subscription.MarkReady();
            SetState(ConnectionState.Ready);
            await foreach (var delivery in subscriber.Channel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false)) yield return delivery;
        }
        finally
        {
            _subscribers.TryRemove(id, out _);
            if (_client.IsConnected && !_subscribers.Values.Any(x => x.Filter == subscription.TopicFilter))
                await _client.UnsubscribeAsync(subscription.TopicFilter, CancellationToken.None).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async Task<MqttPublishResult> PublishAsync(MqttPublication publication, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(publication);
        await ConnectAsync(cancellationToken).ConfigureAwait(false);
        var message = new MqttApplicationMessageBuilder().WithTopic(publication.Topic)
            .WithPayload(publication.Payload.ToArray()).WithQualityOfServiceLevel((MQTTnet.Protocol.MqttQualityOfServiceLevel)publication.QoS)
            .WithRetainFlag(publication.Retain);
        if (_options.ProtocolVersion == MQTTnet.Formatter.MqttProtocolVersion.V500 && publication.ContentType is not null)
            message.WithContentType(publication.ContentType);
        var result = await _client.PublishAsync(message.Build(), cancellationToken).ConfigureAwait(false);
        return new(result.IsSuccess, result.ReasonString);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        foreach (var subscriber in _subscribers.Values) subscriber.Channel.Writer.TryComplete();
        await DisconnectAsync().ConfigureAwait(false);
        _client.ApplicationMessageReceivedAsync -= OnMessageAsync;
        _client.DisconnectedAsync -= OnDisconnectedAsync;
        if (_mutualTls is not null) _mutualTls.ClientCertificateProvider.CertificateChanged -= OnCertificateChanged;
        _client.Dispose();
        _mutualTls?.ClientCertificateProvider.Dispose();
        _lifetime.Cancel();
        _lifetime.Dispose();
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
            message.Retain, message.ContentType, acknowledgement.AcknowledgeAsync);
        foreach (var subscriber in subscribers)
            await subscriber.Channel.Writer.WriteAsync(delivery).ConfigureAwait(false);
    }

    private sealed record Subscriber(string Filter, QoSLevel QoS, Channel<MqttDelivery> Channel);

    private Task OnDisconnectedAsync(MqttClientDisconnectedEventArgs args)
    {
        if (Volatile.Read(ref _manualDisconnect) != 0 || _lifetime.IsCancellationRequested)
        {
            SetState(ConnectionState.Stopped);
            return Task.CompletedTask;
        }
        SetState(ConnectionState.Reconnecting, args.Exception);
        lock (_subscribers)
            _reconnectTask ??= ReconnectAsync(_lifetime.Token);
        return Task.CompletedTask;
    }

    private async Task ReconnectAsync(CancellationToken cancellationToken)
    {
        var started = DateTimeOffset.UtcNow;
        var attempt = 0;
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                attempt++;
                if (_reconnect.MaximumAttempts is { } max && attempt > max) break;
                if (_reconnect.MaximumDuration is { } duration && DateTimeOffset.UtcNow - started > duration) break;
                var exponential = _reconnect.InitialDelay.TotalMilliseconds * Math.Pow(_reconnect.Multiplier, attempt - 1);
                var capped = Math.Min(exponential, _reconnect.MaximumDelay.TotalMilliseconds);
                var jitter = 1 + ((Random.Shared.NextDouble() * 2 - 1) * _reconnect.JitterRatio);
                await Task.Delay(TimeSpan.FromMilliseconds(Math.Max(0, capped * jitter)), cancellationToken).ConfigureAwait(false);
                try
                {
                    SetState(ConnectionState.Reconnecting);
                    await ConnectAsync(cancellationToken).ConfigureAwait(false);
                    return;
                }
                catch when (!cancellationToken.IsCancellationRequested) { }
            }
            SetState(ConnectionState.Faulted);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        finally { lock (_subscribers) _reconnectTask = null; }
    }

    private async Task RestoreSubscriptionsAsync(CancellationToken cancellationToken)
    {
        var subscriptions = _subscribers.Values.GroupBy(x => x.Filter, StringComparer.Ordinal).Select(x => x.First()).ToArray();
        if (subscriptions.Length == 0) return;
        SetState(ConnectionState.Subscribing);
        foreach (var subscription in subscriptions)
            await _client.SubscribeAsync(subscription.Filter, (MQTTnet.Protocol.MqttQualityOfServiceLevel)subscription.QoS, cancellationToken).ConfigureAwait(false);
    }

    private async Task PublishNormalShutdownStateAsync(CancellationToken cancellationToken)
    {
        if (_lastWill?.Enabled != true) return;
        var message = new MqttApplicationMessageBuilder().WithTopic(_lastWill.Topic ?? $"services/{_clientId}/availability")
            .WithPayload(_lastWill.Payload ?? MqttReactiveOrmOptions.CreateUnavailableCloudEvent(_clientId))
            .WithContentType("application/cloudevents+json").WithRetainFlag(_lastWill.Retain)
            .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce)
            .WithMessageExpiryInterval(MqttReactiveOrmOptions.ToSeconds(_lastWill.MessageExpiry)).Build();
        await _client.PublishAsync(message, cancellationToken).ConfigureAwait(false);
    }

    private void SetState(ConnectionState state, Exception? error = null)
    {
        var previous = (ConnectionState)Interlocked.Exchange(ref _state, (int)state);
        if (previous != state) StateChanged?.Invoke(this, new(previous, state, error));
    }

    private void OnCertificateChanged(object? sender, EventArgs args)
    {
        if (Interlocked.Exchange(ref _certificateRotation, 1) == 0)
            _ = RotateCertificateAsync(_lifetime.Token);
    }

    private async Task RotateCertificateAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken).ConfigureAwait(false);
            await DisconnectAsync(cancellationToken).ConfigureAwait(false);
            await ConnectAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception error) { SetState(ConnectionState.Faulted, error); }
        finally { Interlocked.Exchange(ref _certificateRotation, 0); }
    }

    private async Task MonitorCertificateExpirationAsync(CancellationToken cancellationToken)
    {
        if (_mutualTls is null) return;
        using var timer = new PeriodicTimer(_mutualTls.ExpirationCheckInterval);
        string? lastWarning = null;
        try
        {
            do
            {
                var certificate = await _mutualTls.ClientCertificateProvider.GetCertificateAsync(cancellationToken).ConfigureAwait(false);
                var remaining = certificate.NotAfter.ToUniversalTime() - DateTime.UtcNow;
                if (remaining <= _mutualTls.ExpirationWarningThreshold && lastWarning != certificate.Thumbprint)
                {
                    lastWarning = certificate.Thumbprint;
                    CertificateExpiring?.Invoke(this, new(certificate.Subject, certificate.Thumbprint,
                        certificate.NotAfter.ToUniversalTime(), remaining));
                }
            }
            while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception error) { SetState(ConnectionState.Faulted, error); }
    }

    private sealed class SharedAcknowledgement(int remaining, Func<CancellationToken, Task> acknowledge)
    {
        private int _remaining = remaining;
        public Task AcknowledgeAsync(CancellationToken cancellationToken) =>
            Interlocked.Decrement(ref _remaining) == 0 ? acknowledge(cancellationToken) : Task.CompletedTask;
    }
}
