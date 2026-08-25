using Net.Mqtt.ReactiveOrm.Models;
using Net.Mqtt.ReactiveOrm.Security;

namespace Net.Mqtt.ReactiveOrm.Bus.Interfaces;

public interface IMqttBus : IAsyncDisposable
{
    ConnectionState State { get; }
    bool IsReady { get; }
    bool WasSessionRestored { get; }
    event EventHandler<ConnectionStateChanged>? StateChanged;
    event EventHandler<CertificateExpiringEvent>? CertificateExpiring;
    Task ConnectAsync(CancellationToken cancellationToken = default);
    Task DisconnectAsync(CancellationToken cancellationToken = default);
    IAsyncEnumerable<MqttDelivery> SubscribeAsync(MqttSubscription subscription, CancellationToken cancellationToken = default);
    Task<MqttPublishResult> PublishAsync(MqttPublication publication, CancellationToken cancellationToken = default);
}
