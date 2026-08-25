using Microsoft.Extensions.Hosting;
using Net.Mqtt.Infrastructure.Bus.Interfaces;

namespace Demo.Workers;

/// <summary>Observes connection state and certificate-expiration events for operational visibility.</summary>
public sealed class MqttLifecycleWorker(IMqttBus bus) : IHostedService
{
    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        bus.StateChanged += OnStateChanged;
        bus.CertificateExpiring += OnCertificateExpiring;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        bus.StateChanged -= OnStateChanged;
        bus.CertificateExpiring -= OnCertificateExpiring;
        return Task.CompletedTask;
    }

    private static void OnStateChanged(object? sender, Net.Mqtt.Infrastructure.Models.ConnectionStateChanged change) =>
        Console.WriteLine($"MQTT lifecycle: {change.Previous} -> {change.Current}; ready={change.Current == Net.Mqtt.Infrastructure.Models.ConnectionState.Ready}");

    private static void OnCertificateExpiring(object? sender, Net.Mqtt.Infrastructure.Security.CertificateExpiringEvent warning) =>
        Console.WriteLine($"Certificate {warning.Subject} expires in {warning.Remaining}.");
}
