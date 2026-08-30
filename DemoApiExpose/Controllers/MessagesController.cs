using DemoApiExpose.EventEntities;
using DemoApiExpose.Models;
using DemoApiExpose.Mqtt;
using Microsoft.AspNetCore.Mvc;
using Net.Mqtt.Infrastructure.RequestReply;

namespace DemoApiExpose.Controllers;

[ApiController]
[Route("api/controller/messages")]
public sealed class MessagesController(ApiMqttContext mqtt) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<SendMessageHttpResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status504GatewayTimeout)]
    public async Task<ActionResult<SendMessageHttpResponse>> SendAsync(
        SendMessageHttpRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await mqtt.ControllerRequestReply.SendAsync(
                new ControllerMessageRequest { Message = request.Message },
                new MqttRequestOptions { Timeout = TimeSpan.FromSeconds(10) },
                cancellationToken);
            return Ok(new SendMessageHttpResponse(
                response.Data.Response,
                response.CorrelationId,
                response.CloudEvent.Id,
                response.CloudEvent.Source,
                response.CloudEvent.Time));
        }
        catch (MqttRequestTimeoutException error)
        {
            return Problem(error.Message, statusCode: StatusCodes.Status504GatewayTimeout);
        }
    }
}
