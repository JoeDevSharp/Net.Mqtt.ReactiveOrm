using Net.Mqtt.Infrastructure.Contracts;

namespace Demo.Entities;

/// <summary>Represents environmental telemetry supplied by sensor1 to business process BS1.</summary>
[EventType("com.factory.bs1.environment.input.v1")]
[DataSchema("urn:schema:factory:bs1-environment-input:v1")]
[EventVersion("1.0.0")]
[MaximumDataSize(16 * 1024)]
public sealed record Sensor1Telemetry
{
    /// <summary>Gets the stable source identifier.</summary>
    public required string SensorId { get; init; }
    /// <summary>Gets the measured temperature in degrees Celsius.</summary>
    public double Temperature { get; init; }
    /// <summary>Gets the measured relative humidity percentage.</summary>
    public double Humidity { get; init; }
    /// <summary>Gets the UTC occurrence time.</summary>
    public DateTimeOffset ObservedAt { get; init; }
}

/// <summary>Represents one binary video fragment supplied by sensor2 to business process BS2.</summary>
[EventType("com.factory.bs2.video.chunk.v1")]
[DataSchema("urn:schema:factory:bs2-video-chunk:v1")]
[EventVersion("1.0.0")]
[MaximumDataSize(256 * 1024)]
public sealed record Sensor2VideoChunk
{
    /// <summary>Gets the camera identifier.</summary>
    public required string CameraId { get; init; }
    /// <summary>Gets the identifier shared by all chunks in one video stream.</summary>
    public required string StreamId { get; init; }
    /// <summary>Gets the zero-based chunk sequence.</summary>
    public int Sequence { get; init; }
    /// <summary>Gets whether this is the final stream chunk.</summary>
    public bool IsFinal { get; init; }
    /// <summary>Gets the video media type.</summary>
    public required string MediaType { get; init; }
    /// <summary>Gets the binary chunk, represented as Base64 in structured JSON.</summary>
    public required byte[] Payload { get; init; }
    /// <summary>Gets the UTC capture time.</summary>
    public DateTimeOffset CapturedAt { get; init; }
}

/// <summary>Represents the operational decision produced by business service BS1.</summary>
[EventType("com.factory.bs1.operational-assessment.v1")]
[DataSchema("urn:schema:factory:bs1-operational-assessment:v1")]
[EventVersion("1.0.0")]
public sealed record Bs1OperationalAssessment
{
    /// <summary>Gets the evaluated business area.</summary>
    public required string AreaId { get; init; }
    /// <summary>Gets the calculated risk level.</summary>
    public required string RiskLevel { get; init; }
    /// <summary>Gets the recommended business action.</summary>
    public required string RecommendedAction { get; init; }
    /// <summary>Gets the decision time.</summary>
    public DateTimeOffset AssessedAt { get; init; }
}

/// <summary>Represents the completed video-processing result produced by business service BS2.</summary>
[EventType("com.factory.bs2.video-result.v1")]
[DataSchema("urn:schema:factory:bs2-video-result:v1")]
[EventVersion("1.0.0")]
public sealed record Bs2VideoResult
{
    /// <summary>Gets the processed stream identifier.</summary>
    public required string StreamId { get; init; }
    /// <summary>Gets the total number of assembled bytes.</summary>
    public int TotalBytes { get; init; }
    /// <summary>Gets the number of received chunks.</summary>
    public int ChunkCount { get; init; }
    /// <summary>Gets the business classification assigned to the stream.</summary>
    public required string Classification { get; init; }
    /// <summary>Gets the completion time.</summary>
    public DateTimeOffset CompletedAt { get; init; }
}
