using Demo.Mqtt;
using Demo.Services;
using Microsoft.Extensions.Hosting;
using Net.Mqtt.Infrastructure.CloudEvents;
using Net.Mqtt.Infrastructure.Models;

namespace Demo.Workers;

/// <summary>Runs business process BS2 over a backpressured binary camera stream.</summary>
public sealed class BusinessWorkerBs2(MqttContext context, IBusinessServiceBs2 service) : BackgroundService
{
    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var message in context.Sensor2VideoChunks.ReadAllAsync(
            new SubscriptionOptions { Capacity = 4 }, stoppingToken))
        {
            var result = await service.ProcessChunkAsync(message.Data, stoppingToken);
            if (result is not null)
            {
                await context.Bs2VideoResults.PublishAsync(result,
                    new CloudEventPublishOptions
                    {
                        Context = new CloudEventPublishContext
                        {
                            Subject = result.StreamId,
                            Extensions = new CloudEventExtensions
                            {
                                CorrelationId = message.CloudEvent.Extensions.CorrelationId,
                                CausationId = message.CloudEvent.Id
                            }
                        }
                    }, stoppingToken);
                Console.WriteLine($"BS2: stream={result.StreamId}, bytes={result.TotalBytes}, classification={result.Classification}");
            }
            await message.AcknowledgeAsync(stoppingToken);
        }
    }
}
