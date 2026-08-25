using Net.Mqtt.Infrastructure.Bus.Interfaces;
using Net.Mqtt.Infrastructure.Enums;
using Net.Mqtt.Infrastructure.Interfaces;
using Net.Mqtt.Infrastructure.Models;
using Net.Mqtt.Infrastructure.CloudEvents;
using Net.Mqtt.Infrastructure.Contracts;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Runtime.CompilerServices;

namespace Net.Mqtt.Infrastructure;

/// <summary>Publishes and consumes validated CloudEvents whose data is of type <typeparamref name="T"/>.</summary>
/// <typeparam name="T">The governed CloudEvent data type.</typeparam>
public sealed class TopicSet<T> : ITopicSet<T>
{
    private readonly ICloudEventFactory _cloudEventFactory;
    private readonly ICloudEventCodec _cloudEventCodec;
    private readonly IEventContractRegistry _contractRegistry;
    private readonly IEventDataValidator _dataValidator;
    /// <inheritdoc />
    public IMqttBus MqttBus { get; }
    /// <inheritdoc />
    public TopicDefinition Definition { get; }
    /// <inheritdoc />
    [Obsolete("Use Definition.PublishTopic or Definition.SubscribeFilter.")]
    public string Template => Definition.SubscribeFilter;

    /// <summary>Initializes a typed topic set from its transport and governed message services.</summary>
    public TopicSet(IMqttBus mqttBus, ICloudEventFactory cloudEventFactory, ICloudEventCodec cloudEventCodec,
        IEventContractRegistry contractRegistry, IEventDataValidator dataValidator, TopicDefinition definition)
    {
        MqttBus = mqttBus ?? throw new ArgumentNullException(nameof(mqttBus));
        _cloudEventFactory = cloudEventFactory ?? throw new ArgumentNullException(nameof(cloudEventFactory));
        _cloudEventCodec = cloudEventCodec ?? throw new ArgumentNullException(nameof(cloudEventCodec));
        _contractRegistry = contractRegistry ?? throw new ArgumentNullException(nameof(contractRegistry));
        _dataValidator = dataValidator ?? throw new ArgumentNullException(nameof(dataValidator));
        Definition = definition ?? throw new ArgumentNullException(nameof(definition));
    }

    /// <inheritdoc />
    public Task PublishAsync(T data, CancellationToken cancellationToken = default) =>
        PublishAsync(data, CloudEventPublishOptions.Default, cancellationToken);

    /// <inheritdoc />
    public async Task PublishAsync(T data, CloudEventPublishOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(options);
        var descriptor = Definition.CloudEvent ?? throw new InvalidOperationException("The TopicSet has no CloudEvent descriptor.");
        var contract = _contractRegistry.GetByDataType(typeof(T));
        EventContractGuard.EnsureCompatible(contract, descriptor.Type, descriptor.DataSchema, typeof(T));
        var cloudEvent = _cloudEventFactory.Create(data, descriptor, options.Context);
        var serializedData = contract.JsonMapper?.Serialize(data!, typeof(T)) ?? _cloudEventCodec.SerializeData(data);
        await ValidateDataAsync(contract, serializedData, cancellationToken).ConfigureAwait(false);
        var publication = new MqttPublication(Definition.ResolvePublishTopic(data), _cloudEventCodec.Serialize(cloudEvent, serializedData),
            options.QoS ?? Definition.QoS, options.Retain ?? Definition.Retain, JsonCloudEventCodec.StructuredContentType);
        var result = await MqttBus.PublishAsync(publication, cancellationToken).ConfigureAwait(false);
        if (!result.IsSuccess) throw new InvalidOperationException(result.Reason ?? "The MQTT publication failed.");
    }

    /// <summary>Returns an asynchronous enumerator over validated data using default subscription options.</summary>
    public async IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        await foreach (var message in ReadAllAsync(SubscriptionOptions.Default, cancellationToken).ConfigureAwait(false))
        {
            yield return message.Data;
            await message.AcknowledgeAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<MqttMessageContext<T>> ReadAllAsync(
        SubscriptionOptions options,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.Capacity <= 0) throw new ArgumentOutOfRangeException(nameof(options), "Capacity must be greater than zero.");
        var subscription = new MqttSubscription(Definition.SubscribeFilter, options.QoS ?? Definition.QoS, options.Capacity);
        await foreach (var delivery in MqttBus.SubscribeAsync(subscription, cancellationToken).ConfigureAwait(false))
        {
            if (!Definition.MatchesSubscription<T>(delivery.Topic)) continue;
            CloudEventEnvelope envelope;
            try
            {
                envelope = _cloudEventCodec.ReadEnvelope(delivery.Payload, delivery.ContentType);
            }
            catch (InvalidDataException error)
            {
                throw new InvalidMqttCloudEventException(delivery.Topic, delivery.ContentType, error);
            }
            var contract = _contractRegistry.GetByEventType(envelope.Type);
            EventContractGuard.EnsureCompatible(contract, envelope.Type, envelope.DataSchema, typeof(T));
            await ValidateDataAsync(contract, envelope.Data, cancellationToken).ConfigureAwait(false);
            var cloudEvent = contract.JsonMapper is null
                ? _cloudEventCodec.Deserialize<T>(delivery.Payload, delivery.ContentType)
                : envelope.WithData((T)contract.JsonMapper.Deserialize(envelope.Data, typeof(T)));
            yield return new MqttMessageContext<T>(cloudEvent, delivery);
        }
    }

    /// <inheritdoc />
    public IAsyncEnumerable<MqttMessageContext<T>> ReadAllAsync(CancellationToken cancellationToken = default) =>
        ReadAllAsync(SubscriptionOptions.Default, cancellationToken);

    private async ValueTask ValidateDataAsync(EventContractDescriptor contract, ReadOnlyMemory<byte> data, CancellationToken cancellationToken)
    {
        var limits = EventContractGuard.ValidateLimits(contract, data);
        if (!limits.IsValid) throw new EventDataValidationException(limits);
        var schema = await _dataValidator.ValidateAsync(contract.DataSchema, data, cancellationToken).ConfigureAwait(false);
        if (!schema.IsValid) throw new EventDataValidationException(schema);
    }

    /// <summary>Subscribes an observer to the compatibility reactive data stream.</summary>
    public IDisposable Subscribe(IObserver<T> observer)
    {
        ArgumentNullException.ThrowIfNull(observer);
        var cancellation = new CancellationTokenSource();
        _ = ObserveAsync(observer, cancellation.Token);
        return Disposable.Create(cancellation, static source => { source.Cancel(); source.Dispose(); });
    }

    /// <summary>Creates a filtered compatibility observable.</summary>
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
