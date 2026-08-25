using Net.Mqtt.Infrastructure.Bus.Interfaces;
using Net.Mqtt.Infrastructure.Models;

namespace Net.Mqtt.Infrastructure.Interfaces;

/// <summary>Represents a strongly typed MQTT publication and subscription endpoint.</summary>
/// <typeparam name="T">The CloudEvent data type.</typeparam>
public interface ITopicSet<T> : IObservable<T>, IAsyncEnumerable<T>
{
    /// <summary>Gets the shared MQTT transport.</summary>
    IMqttBus MqttBus { get; }
    /// <summary>Gets the topic and CloudEvent definition.</summary>
    TopicDefinition Definition { get; }
    /// <summary>Gets the subscription filter retained for backward compatibility.</summary>
    string Template { get; }
    /// <summary>Publishes data using the topic defaults.</summary>
    Task PublishAsync(T data, CancellationToken cancellationToken = default);
    /// <summary>Publishes data using the supplied CloudEvent and MQTT options.</summary>
    Task PublishAsync(T data, CloudEventPublishOptions options, CancellationToken cancellationToken);
    /// <summary>Reads validated messages using the default subscription options.</summary>
    IAsyncEnumerable<MqttMessageContext<T>> ReadAllAsync(CancellationToken cancellationToken = default);
    /// <summary>Reads validated messages using explicit subscription and backpressure options.</summary>
    IAsyncEnumerable<MqttMessageContext<T>> ReadAllAsync(SubscriptionOptions options, CancellationToken cancellationToken);
}
