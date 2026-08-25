using System.Text;
using Demo.Entities;
using Demo.Mqtt;
using Microsoft.Extensions.Hosting;
using Net.Mqtt.Infrastructure.Bus.Interfaces;
using Net.Mqtt.Infrastructure.CloudEvents;
using Net.Mqtt.Infrastructure.Models;

namespace Demo.Workers;

/// <summary>Simulates the two external data sources consumed by the business Workers.</summary>
public sealed class BusinessInputSimulatorWorker(MqttContext context, IMqttBus bus) : BackgroundService
{
    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!bus.IsReady) await Task.Delay(100, stoppingToken);
        var correlationId = Guid.NewGuid().ToString("N");

        await context.Sensor1Telemetry.PublishAsync(new Sensor1Telemetry
        {
            SensorId = "sensor1",
            Temperature = 36,
            Humidity = 91,
            ObservedAt = DateTimeOffset.UtcNow
        }, Options("sensor1", correlationId), stoppingToken);

        var streamId = Guid.NewGuid().ToString("N");
        var chunks = new[]
        {
            Encoding.UTF8.GetBytes("video-binary-chunk-0001"),
            Encoding.UTF8.GetBytes("video-binary-chunk-0002"),
            Encoding.UTF8.GetBytes("video-binary-chunk-0003")
        };
        for (var sequence = 0; sequence < chunks.Length; sequence++)
        {
            await context.Sensor2VideoChunks.PublishAsync(new Sensor2VideoChunk
            {
                CameraId = "sensor2",
                StreamId = streamId,
                Sequence = sequence,
                IsFinal = sequence == chunks.Length - 1,
                MediaType = "video/mp4",
                Payload = chunks[sequence],
                CapturedAt = DateTimeOffset.UtcNow
            }, Options(streamId, correlationId), stoppingToken);
        }
    }

    private static CloudEventPublishOptions Options(string subject, string correlationId) => new()
    {
        Context = new CloudEventPublishContext
        {
            Subject = subject,
            Extensions = new CloudEventExtensions { CorrelationId = correlationId }
        }
    };
}
