using Demo.Entities;
using Demo.Mqtt;
using Microsoft.Extensions.Hosting;
using Net.Mqtt.Infrastructure.Bus.Interfaces;
using Net.Mqtt.Infrastructure.Models;
using Net.Mqtt.Infrastructure.CloudEvents;

namespace Demo;

/// <summary>Demonstrates ordered subscription, publication, consumption, and acknowledgement.</summary>
/// <param name="context">The demo MQTT context.</param>
/// <param name="bus">The shared MQTT transport.</param>
public sealed class SensorWorker(MqttContext context, IMqttBus bus) : BackgroundService
{
    /// <summary>Runs the demo workflow until host shutdown is requested.</summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var subscriptionReady = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        void OnStateChanged(object? _, ConnectionStateChanged change)
        {
            Console.WriteLine($"MQTT: {change.Previous} -> {change.Current}");
            if (change.Previous == ConnectionState.Subscribing && change.Current == ConnectionState.Ready)
                subscriptionReady.TrySetResult();
        }

        bus.StateChanged += OnStateChanged;
        try
        {
            var consumer = ConsumeAsync(stoppingToken);
            await subscriptionReady.Task.WaitAsync(stoppingToken);

            await context.DHT230222_Modules.PublishAsync(
                new DHT230222_Modules
                {
                    Temperature = 30,
                    Humidity = 45,
                    Timestamp = DateTime.UtcNow
                },
                new CloudEventPublishOptions
                {
                    Context = new CloudEventPublishContext
                    {
                        Subject = "sensor-demo-01",
                        Extensions = new CloudEventExtensions
                        {
                            CorrelationId = Guid.NewGuid().ToString("N")
                        }
                    }
                },
                stoppingToken);

            await consumer;
        }
        finally
        {
            bus.StateChanged -= OnStateChanged;
        }
    }

    private async Task ConsumeAsync(CancellationToken stoppingToken)
    {

        await foreach (var message in context.DHT230222_Modules.ReadAllAsync(stoppingToken))
        {
            if (message.Data.Temperature > 20.5)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(
                    $"Alert: Temperature: {message.Data.Temperature}, " +
                    $"Humidity: {message.Data.Humidity}, Timestamp: {message.Data.Timestamp:O}");
                Console.ResetColor();
            }

            // L'acquittement n'a lieu qu'après le traitement applicatif réussi.
            await message.AcknowledgeAsync(stoppingToken);
        }
    }
}
