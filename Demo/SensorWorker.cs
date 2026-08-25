using Demo.Entities;
using Demo.Mqtt;
using Microsoft.Extensions.Hosting;
using Net.Mqtt.ReactiveOrm.Bus.Interfaces;
using Net.Mqtt.ReactiveOrm.Models;
using Net.Mqtt.ReactiveOrm.CloudEvents;

namespace Demo;

public sealed class SensorWorker(MqttContext context, IMqttBus bus) : BackgroundService
{
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

        var options = new SubscriptionOptions { Capacity = 32 };
        await foreach (var message in context.DHT230222_Modules.ReadAllAsync(options, stoppingToken))
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
