using Net.Mqtt.Infrastructure.Models;
using Net.Mqtt.Infrastructure.Security;

namespace Net.Mqtt.Infrastructure.Bus.Interfaces;

/// <summary>Defines the injectable asynchronous MQTT transport used by contexts and topic sets.</summary>
public interface IMqttBus : IAsyncDisposable
{
    /// <summary>Gets the current connection lifecycle state.</summary>
    ConnectionState State { get; }
    /// <summary>Gets a value indicating whether the bus can currently receive messages.</summary>
    bool IsReady { get; }
    /// <summary>Gets a value indicating whether the broker restored the previous persistent session.</summary>
    bool WasSessionRestored { get; }
    /// <summary>Occurs when the connection lifecycle state changes.</summary>
    event EventHandler<ConnectionStateChanged>? StateChanged;
    /// <summary>Occurs when the client certificate approaches its expiration date.</summary>
    event EventHandler<CertificateExpiringEvent>? CertificateExpiring;
    /// <summary>Connects the transport to the configured broker.</summary>
    Task ConnectAsync(CancellationToken cancellationToken = default);
    /// <summary>Drains and disconnects the transport.</summary>
    Task DisconnectAsync(CancellationToken cancellationToken = default);
    /// <summary>Subscribes to a topic filter and asynchronously yields deliveries.</summary>
    IAsyncEnumerable<MqttDelivery> SubscribeAsync(MqttSubscription subscription, CancellationToken cancellationToken = default);
    /// <summary>Publishes an encoded MQTT message.</summary>
    Task<MqttPublishResult> PublishAsync(MqttPublication publication, CancellationToken cancellationToken = default);
}
