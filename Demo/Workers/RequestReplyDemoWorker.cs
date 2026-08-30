using Demo.Entities;
using Demo.Mqtt;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Net.Mqtt.Infrastructure.RequestReply;

namespace Demo.Workers;

/// <summary>Runs both sides of the correlated request/reply demo and verifies concurrent dispatch.</summary>
public sealed class RequestReplyDemoWorker(MqttContext context, ILogger<RequestReplyDemoWorker> logger)
    : BackgroundService
{
    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var handler = context.SimpleMessageRequests.HandleAsync(
            (request, cancellationToken) => Task.FromResult(new SimpleMessageResponse
            {
                Response = $"ACK:{request.Data.Message}"
            }),
            stoppingToken);

        var messages = new[] { "alpha", "beta", "gamma" };
        var calls = messages.Select(message => SendAndVerifyAsync(message, stoppingToken)).ToArray();
        await Task.WhenAll(calls).ConfigureAwait(false);

        logger.LogInformation(
            "MQTT request/reply demo completed: {Count} concurrent responses were correctly correlated.",
            calls.Length);

        await handler.ConfigureAwait(false);
    }

    private async Task SendAndVerifyAsync(string message, CancellationToken cancellationToken)
    {
        var response = await context.SimpleMessageRequests.SendAsync(
            new SimpleMessage { Message = message },
            new MqttRequestOptions { Timeout = TimeSpan.FromSeconds(10) },
            cancellationToken).ConfigureAwait(false);
        var expected = $"ACK:{message}";
        if (!string.Equals(response.Data.Response, expected, StringComparison.Ordinal))
            throw new InvalidOperationException(
                $"Request/reply correlation failed. Expected '{expected}', received '{response.Data.Response}'.");

        logger.LogInformation(
            "MQTT request/reply correlation {CorrelationId}: {Response}",
            response.CorrelationId,
            response.Data.Response);
    }
}
