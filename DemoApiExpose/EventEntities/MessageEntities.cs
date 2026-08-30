using Net.Mqtt.Infrastructure.Contracts;

namespace DemoApiExpose.EventEntities;

[EventType("com.mint.demo.controller.message.request.v1")]
[DataSchema("urn:schema:mint:demo:controller-message-request:v1")]
[EventVersion("1.0.0")]
public sealed record ControllerMessageRequest
{
    public required string Message { get; init; }
}

[EventType("com.mint.demo.controller.message.response.v1")]
[DataSchema("urn:schema:mint:demo:controller-message-response:v1")]
[EventVersion("1.0.0")]
public sealed record ControllerMessageResponse
{
    public required string Response { get; init; }
}

[EventType("com.mint.demo.minimal.message.request.v1")]
[DataSchema("urn:schema:mint:demo:minimal-message-request:v1")]
[EventVersion("1.0.0")]
public sealed record MinimalMessageRequest
{
    public required string Message { get; init; }
}

[EventType("com.mint.demo.minimal.message.response.v1")]
[DataSchema("urn:schema:mint:demo:minimal-message-response:v1")]
[EventVersion("1.0.0")]
public sealed record MinimalMessageResponse
{
    public required string Response { get; init; }
}
