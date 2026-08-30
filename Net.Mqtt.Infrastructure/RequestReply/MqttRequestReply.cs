using System.Collections.Concurrent;
using Net.Mqtt.Infrastructure.CloudEvents;
using Net.Mqtt.Infrastructure.Enums;
using Net.Mqtt.Infrastructure.Models;

namespace Net.Mqtt.Infrastructure.RequestReply;

/// <summary>Configures one MQTT request/reply invocation.</summary>
public sealed record MqttRequestOptions
{
    /// <summary>Gets the maximum time to wait for a correlated response.</summary>
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(30);
    /// <summary>Gets the optional request publication QoS override.</summary>
    public QoSLevel? QoS { get; init; }
    /// <summary>Gets an optional caller-provided correlation identifier.</summary>
    public string? CorrelationId { get; init; }
}

/// <summary>Contains a correlated MQTT response and its complete CloudEvent.</summary>
public sealed record MqttResponse<TResponse>(
    TResponse Data,
    string CorrelationId,
    CloudEventMessage<TResponse> CloudEvent);

/// <summary>Raised when no correlated MQTT response arrives before the configured timeout.</summary>
public sealed class MqttRequestTimeoutException(string correlationId, TimeSpan timeout)
    : TimeoutException($"MQTT request '{correlationId}' did not receive a response within {timeout}.");

/// <summary>Publishes requests and dispatches correlated responses through one shared subscription.</summary>
public sealed class MqttRequestSet<TRequest, TResponse>
{
    private readonly TopicSet<TRequest> _requests;
    private readonly TopicSet<TResponse> _responses;
    private readonly ConcurrentDictionary<string, TaskCompletionSource<MqttResponse<TResponse>>> _pending = new(StringComparer.Ordinal);
    private readonly object _startLock = new();
    private readonly CancellationTokenSource _lifetime = new();
    private TaskCompletionSource _started = NewCompletionSource();
    private Task? _pump;

    internal MqttRequestSet(TopicSet<TRequest> requests, TopicSet<TResponse> responses)
    {
        _requests = requests;
        _responses = responses;
    }

    /// <summary>Sends a request after the shared response subscription is ready.</summary>
    public Task<MqttResponse<TResponse>> SendAsync(TRequest request, CancellationToken cancellationToken = default) =>
        SendAsync(request, new MqttRequestOptions(), cancellationToken);

    /// <summary>Sends a request and waits for its correlated response.</summary>
    public async Task<MqttResponse<TResponse>> SendAsync(
        TRequest request,
        MqttRequestOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(options);
        if (options.Timeout <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(options), "Timeout must be greater than zero.");

        var correlationId = string.IsNullOrWhiteSpace(options.CorrelationId)
            ? Guid.NewGuid().ToString("N")
            : options.CorrelationId;
        var completion = new TaskCompletionSource<MqttResponse<TResponse>>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!_pending.TryAdd(correlationId, completion))
            throw new InvalidOperationException($"MQTT correlation id '{correlationId}' is already pending.");

        try
        {
            await EnsureStartedAsync(cancellationToken).ConfigureAwait(false);
            await _requests.PublishAsync(request, new CloudEventPublishOptions
            {
                QoS = options.QoS,
                Context = new CloudEventPublishContext
                {
                    Extensions = new CloudEventExtensions { CorrelationId = correlationId }
                }
            }, cancellationToken).ConfigureAwait(false);

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(options.Timeout);
            try
            {
                return await completion.Task.WaitAsync(timeout.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new MqttRequestTimeoutException(correlationId, options.Timeout);
            }
        }
        finally
        {
            _pending.TryRemove(correlationId, out _);
        }
    }

    /// <summary>Consumes requests, publishes correlated responses, and acknowledges successful requests.</summary>
    public async Task HandleAsync(
        Func<MqttMessageContext<TRequest>, CancellationToken, Task<TResponse>> handler,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(handler);
        await foreach (var request in _requests.ReadAllAsync(cancellationToken).ConfigureAwait(false))
        {
            var correlationId = request.CloudEvent.Extensions.CorrelationId;
            if (string.IsNullOrWhiteSpace(correlationId))
                throw new InvalidOperationException($"MQTT request '{request.CloudEvent.Id}' has no correlationid extension.");

            var response = await handler(request, cancellationToken).ConfigureAwait(false);
            await _responses.PublishAsync(response, new CloudEventPublishOptions
            {
                Context = new CloudEventPublishContext
                {
                    Extensions = new CloudEventExtensions
                    {
                        CorrelationId = correlationId,
                        CausationId = request.CloudEvent.Id,
                        TraceParent = request.CloudEvent.Extensions.TraceParent,
                        TraceState = request.CloudEvent.Extensions.TraceState
                    }
                }
            }, cancellationToken).ConfigureAwait(false);
            await request.AcknowledgeAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private Task EnsureStartedAsync(CancellationToken cancellationToken)
    {
        lock (_startLock)
            _pump ??= PumpResponsesAsync(_lifetime.Token);
        return _started.Task.WaitAsync(cancellationToken);
    }

    private async Task PumpResponsesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var response in _responses.ReadAllAsync(
                SubscriptionOptions.Default,
                subscription => _ = SignalReadyAsync(subscription),
                cancellationToken).ConfigureAwait(false))
            {
                var correlationId = response.CloudEvent.Extensions.CorrelationId;
                if (!string.IsNullOrWhiteSpace(correlationId)
                    && _pending.TryRemove(correlationId, out var completion))
                {
                    await response.AcknowledgeAsync(cancellationToken).ConfigureAwait(false);
                    completion.TrySetResult(new(response.Data, correlationId, response.CloudEvent));
                }
                else
                {
                    await response.AcknowledgeAsync(cancellationToken).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception exception)
        {
            _started.TrySetException(exception);
            foreach (var pending in _pending.Values) pending.TrySetException(exception);
        }
    }

    private async Task SignalReadyAsync(MqttSubscription subscription)
    {
        try
        {
            await subscription.WaitUntilReadyAsync(_lifetime.Token).ConfigureAwait(false);
            _started.TrySetResult();
        }
        catch (Exception exception) { _started.TrySetException(exception); }
    }

    private static TaskCompletionSource NewCompletionSource() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);
}
