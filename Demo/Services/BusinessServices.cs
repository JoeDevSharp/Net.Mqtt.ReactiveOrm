using System.Collections.Concurrent;
using Demo.Entities;

namespace Demo.Services;

/// <summary>Defines business service BS1 for environmental operating decisions.</summary>
public interface IBusinessServiceBs1
{
    /// <summary>Evaluates validated environmental telemetry.</summary>
    ValueTask<Bs1OperationalAssessment> AssessAsync(Sensor1Telemetry telemetry, CancellationToken cancellationToken);
}

/// <summary>Applies the BS1 environmental policy independently from MQTT transport concerns.</summary>
public sealed class BusinessServiceBs1 : IBusinessServiceBs1
{
    /// <inheritdoc />
    public ValueTask<Bs1OperationalAssessment> AssessAsync(
        Sensor1Telemetry telemetry, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var critical = telemetry.Temperature >= 35 || telemetry.Humidity >= 90;
        var warning = telemetry.Temperature >= 28 || telemetry.Humidity >= 75;
        return ValueTask.FromResult(new Bs1OperationalAssessment
        {
            AreaId = "warehouse-a",
            RiskLevel = critical ? "critical" : warning ? "warning" : "normal",
            RecommendedAction = critical ? "stop-operations" : warning ? "inspect-ventilation" : "continue",
            AssessedAt = DateTimeOffset.UtcNow
        });
    }
}

/// <summary>Defines business service BS2 for assembling and classifying camera streams.</summary>
public interface IBusinessServiceBs2
{
    /// <summary>Adds one ordered video chunk and returns a result when the stream is complete.</summary>
    ValueTask<Bs2VideoResult?> ProcessChunkAsync(Sensor2VideoChunk chunk, CancellationToken cancellationToken);
}

/// <summary>Provides a thread-safe in-memory implementation of business service BS2.</summary>
public sealed class BusinessServiceBs2 : IBusinessServiceBs2
{
    private readonly ConcurrentDictionary<string, SortedDictionary<int, byte[]>> _streams = new();

    /// <inheritdoc />
    public ValueTask<Bs2VideoResult?> ProcessChunkAsync(
        Sensor2VideoChunk chunk, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var chunks = _streams.GetOrAdd(chunk.StreamId, _ => new());
        lock (chunks)
        {
            chunks.TryAdd(chunk.Sequence, chunk.Payload);
            if (!chunk.IsFinal) return ValueTask.FromResult<Bs2VideoResult?>(null);

            var totalBytes = chunks.Values.Sum(value => value.Length);
            _streams.TryRemove(chunk.StreamId, out _);
            return ValueTask.FromResult<Bs2VideoResult?>(new Bs2VideoResult
            {
                StreamId = chunk.StreamId,
                TotalBytes = totalBytes,
                ChunkCount = chunks.Count,
                Classification = totalBytes > 24 ? "business-activity-detected" : "no-relevant-activity",
                CompletedAt = DateTimeOffset.UtcNow
            });
        }
    }
}
