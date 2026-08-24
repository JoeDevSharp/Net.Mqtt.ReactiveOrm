using Net.Mqtt.ReactiveOrm.Models;

namespace Net.Mqtt.ReactiveOrm.Bus.Interfaces;

public interface IMqttBus : IAsyncDisposable
{
    ConnectionState State { get; }
    Task ConnectAsync(CancellationToken cancellationToken = default);
    Task DisconnectAsync(CancellationToken cancellationToken = default);
    IAsyncEnumerable<MqttDelivery> SubscribeAsync(MqttSubscription subscription, CancellationToken cancellationToken = default);
    Task<MqttPublishResult> PublishAsync(MqttPublication publication, CancellationToken cancellationToken = default);
}
