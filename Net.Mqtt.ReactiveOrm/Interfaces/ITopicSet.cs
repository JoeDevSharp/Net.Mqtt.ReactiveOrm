using Net.Mqtt.ReactiveOrm.Bus.Interfaces;
using Net.Mqtt.ReactiveOrm.Models;

namespace Net.Mqtt.ReactiveOrm.Interfaces;

public interface ITopicSet<T> : IObservable<T>, IAsyncEnumerable<T>
{
    IMqttBus MqttBus { get; }
    TopicDefinition Definition { get; }
    string Template { get; }
    Task PublishAsync(T data, CancellationToken cancellationToken = default);
    Task PublishAsync(T data, CloudEventPublishOptions options, CancellationToken cancellationToken);
    IAsyncEnumerable<MqttMessageContext<T>> ReadAllAsync(CancellationToken cancellationToken = default);
    IAsyncEnumerable<MqttMessageContext<T>> ReadAllAsync(SubscriptionOptions options, CancellationToken cancellationToken);
}
