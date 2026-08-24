using Net.Mqtt.ReactiveOrm.Bus.Interfaces;
using Net.Mqtt.ReactiveOrm.Enums;
using Net.Mqtt.ReactiveOrm.Interfaces;
using Net.Mqtt.ReactiveOrm.Models;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Runtime.CompilerServices;

namespace Net.Mqtt.ReactiveOrm;

public sealed class TopicSet<T> : ITopicSet<T>
{
    private readonly IMqttCodec _codec;
    public IMqttBus MqttBus { get; }
    public TopicDefinition Definition { get; }
    public string Template => Definition.Template;

    public TopicSet(IMqttBus mqttBus, IMqttCodec codec, TopicDefinition definition)
    {
        MqttBus = mqttBus ?? throw new ArgumentNullException(nameof(mqttBus));
        _codec = codec ?? throw new ArgumentNullException(nameof(codec));
        Definition = definition ?? throw new ArgumentNullException(nameof(definition));
    }

    public Task PublishAsync(T data, CancellationToken cancellationToken = default) =>
        PublishAsync(data, CloudEventPublishOptions.Default, cancellationToken);

    public async Task PublishAsync(T data, CloudEventPublishOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(options);
        var publication = new MqttPublication(
            Definition.Resolve<T>(), _codec.Encode(data), options.QoS ?? Definition.QoS, options.Retain ?? Definition.Retain);
        var result = await MqttBus.PublishAsync(publication, cancellationToken).ConfigureAwait(false);
        if (!result.IsSuccess) throw new InvalidOperationException(result.Reason ?? "The MQTT publication failed.");
    }

    public async IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        await foreach (var message in ReadAllAsync(SubscriptionOptions.Default, cancellationToken).ConfigureAwait(false))
        {
            yield return message.Data;
            await message.AcknowledgeAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public async IAsyncEnumerable<MqttMessageContext<T>> ReadAllAsync(
        SubscriptionOptions options,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.Capacity <= 0) throw new ArgumentOutOfRangeException(nameof(options), "Capacity must be greater than zero.");
        var subscription = new MqttSubscription(Definition.Resolve<T>(), options.QoS ?? Definition.QoS, options.Capacity);
        await foreach (var delivery in MqttBus.SubscribeAsync(subscription, cancellationToken).ConfigureAwait(false))
            yield return new MqttMessageContext<T>(_codec.Decode<T>(delivery.Payload), delivery);
    }

    public IDisposable Subscribe(IObserver<T> observer)
    {
        ArgumentNullException.ThrowIfNull(observer);
        var cancellation = new CancellationTokenSource();
        _ = ObserveAsync(observer, cancellation.Token);
        return Disposable.Create(cancellation, static source => { source.Cancel(); source.Dispose(); });
    }

    public IObservable<T> Where(Func<T, bool> predicate) => Observable.Create<T>(Subscribe).Where(predicate);

    private async Task ObserveAsync(IObserver<T> observer, CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var message in ReadAllAsync(SubscriptionOptions.Default, cancellationToken).ConfigureAwait(false))
            {
                observer.OnNext(message.Data);
                await message.AcknowledgeAsync(cancellationToken).ConfigureAwait(false);
            }
            observer.OnCompleted();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception exception) { observer.OnError(exception); }
    }
}
