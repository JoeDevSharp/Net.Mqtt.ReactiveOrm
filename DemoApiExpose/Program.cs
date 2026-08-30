using DemoApiExpose.EventEntities;
using DemoApiExpose.Models;
using DemoApiExpose.Mqtt;
using DemoApiExpose.Workers;
using Net.Mqtt.Infrastructure.RequestReply;

var builder = WebApplication.CreateBuilder(args);
var mqttSection = builder.Configuration.GetSection("Mqtt");

builder.Services.AddControllers();
builder.Services.AddMqttReactiveOrm<ApiMqttContext>(mqtt =>
{
    if (mqttSection.GetValue("UseInMemory", true)) mqtt.UseInMemoryTransport();
    else mqtt.ConnectTo(mqttSection["Host"] ?? "localhost", mqttSection.GetValue("Port", 1883));

    var clientId = mqttSection["ClientId"] ?? "demo-api-expose";
    mqtt.IdentifyAs(clientId)
        .WithBaseTopic(mqttSection["BaseTopic"] ?? "mint/v1.0.0")
        .ForModule(mqttSection["Module"] ?? "demo-api")
        .ForService(mqttSection["Service"] ?? "http-expose")
        .WithCloudEventSource($"urn:mint:demo-api:{clientId}")
        .UseUnavailableLastWill()
        .UseDevelopmentDefaults();

    mqtt.UseEventEntities(entities =>
    {
        entities.Add<ControllerMessageRequest>();
        entities.Add<ControllerMessageResponse>();
        entities.Add<MinimalMessageRequest>();
        entities.Add<MinimalMessageResponse>();
    });

    mqtt.UseSchemas(schemas =>
    {
        schemas.AddInline("urn:schema:mint:demo:controller-message-request:v1", MessageSchema("message"), "1.0.0");
        schemas.AddInline("urn:schema:mint:demo:controller-message-response:v1", MessageSchema("response"), "1.0.0");
        schemas.AddInline("urn:schema:mint:demo:minimal-message-request:v1", MessageSchema("message"), "1.0.0");
        schemas.AddInline("urn:schema:mint:demo:minimal-message-response:v1", MessageSchema("response"), "1.0.0");
    });
});

builder.Services.AddHostedService<DemoMqttResponder>();

var app = builder.Build();
app.MapControllers();

app.MapPost("/api/minimal/messages", async (
    SendMessageHttpRequest request,
    ApiMqttContext mqtt,
    CancellationToken cancellationToken) =>
{
    try
    {
        var response = await mqtt.MinimalRequestReply.SendAsync(
            new MinimalMessageRequest { Message = request.Message },
            new MqttRequestOptions { Timeout = TimeSpan.FromSeconds(10) },
            cancellationToken);
        return Results.Ok(new SendMessageHttpResponse(
            response.Data.Response,
            response.CorrelationId,
            response.CloudEvent.Id,
            response.CloudEvent.Source,
            response.CloudEvent.Time));
    }
    catch (MqttRequestTimeoutException error)
    {
        return Results.Problem(error.Message, statusCode: StatusCodes.Status504GatewayTimeout);
    }
});

app.Run();

static string MessageSchema(string propertyName) => $$$"""
    {
      "$schema":"https://json-schema.org/draft/2020-12/schema",
      "type":"object",
      "required":["{{{propertyName}}}"],
      "properties":{"{{{propertyName}}}":{"type":"string","minLength":1}},
      "additionalProperties":false
    }
    """;
