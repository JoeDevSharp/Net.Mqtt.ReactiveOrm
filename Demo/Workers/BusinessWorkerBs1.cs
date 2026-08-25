using Demo.Mqtt;
using Demo.Services;
using Microsoft.Extensions.Hosting;
using Net.Mqtt.Infrastructure.CloudEvents;
using Net.Mqtt.Infrastructure.Models;

namespace Demo.Workers;

/// <summary>Runs business process BS1 over temperature and humidity telemetry.</summary>
public sealed class BusinessWorkerBs1(MqttContext context, IBusinessServiceBs1 service) : BackgroundService
{
    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var message in context.Sensor1Telemetry.ReadAllAsync(
            new SubscriptionOptions { Capacity = 16 }, stoppingToken))
        {
            var assessment = await service.AssessAsync(message.Data, stoppingToken);
            await context.Bs1Assessments.PublishAsync(assessment,
                new CloudEventPublishOptions
                {
                    Context = new CloudEventPublishContext
                    {
                        Subject = assessment.AreaId,
                        Extensions = new CloudEventExtensions
                        {
                            CorrelationId = message.CloudEvent.Extensions.CorrelationId,
                            CausationId = message.CloudEvent.Id
                        }
                    }
                }, stoppingToken);
            await message.AcknowledgeAsync(stoppingToken);
            Console.WriteLine($"BS1: area={assessment.AreaId}, risk={assessment.RiskLevel}, action={assessment.RecommendedAction}");
        }
    }
}
