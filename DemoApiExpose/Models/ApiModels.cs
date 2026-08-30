namespace DemoApiExpose.Models;

public sealed record SendMessageHttpRequest(string Message);

public sealed record SendMessageHttpResponse(
    string Response,
    string CorrelationId,
    string CloudEventId,
    Uri Source,
    DateTimeOffset? Time);
