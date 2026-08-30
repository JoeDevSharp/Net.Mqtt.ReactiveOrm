using DemoApiExpose.EventEntities;
using DemoApiExpose.Mqtt;

namespace DemoApiExpose.Workers;

public sealed class DemoMqttResponder(ApiMqttContext mqtt, ILogger<DemoMqttResponder> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var controller = mqtt.ControllerRequestReply.HandleAsync(
            (request, _) => Task.FromResult(new ControllerMessageResponse
            {
                Response = $"controller-ack:{request.Data.Message}"
            }), stoppingToken);

        var minimal = mqtt.MinimalRequestReply.HandleAsync(
            (request, _) => Task.FromResult(new MinimalMessageResponse
            {
                Response = $"minimal-ack:{request.Data.Message}"
            }), stoppingToken);

        logger.LogInformation("Demo MQTT responders started for controller and Minimal API topics.");
        await Task.WhenAll(controller, minimal);
    }
}
