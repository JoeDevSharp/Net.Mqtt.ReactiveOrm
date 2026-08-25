using System.Diagnostics;

namespace Net.Mqtt.ReactiveOrm.CloudEvents;

public sealed class CloudEventFactory : ICloudEventFactory
{
    public CloudEventMessage<TData> Create<TData>(TData data, CloudEventDescriptor descriptor, CloudEventPublishContext context)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(context);
        CloudEventValidation.ValidateDescriptor(descriptor);
        var activity = Activity.Current;
        var supplied = context.Extensions;
        var extensions = supplied with
        {
            TraceParent = supplied.TraceParent ?? activity?.Id,
            TraceState = supplied.TraceState ?? activity?.TraceStateString
        };
        CloudEventValidation.ValidateExtensions(extensions);
        return new CloudEventMessage<TData>
        {
            SpecVersion = "1.0",
            Id = context.Id ?? Guid.NewGuid().ToString("N"),
            Source = descriptor.Source,
            Type = descriptor.Type,
            Subject = context.Subject ?? descriptor.Subject,
            Time = context.Time ?? DateTimeOffset.UtcNow,
            DataContentType = descriptor.DataContentType,
            DataSchema = descriptor.DataSchema,
            Data = data,
            Extensions = extensions
        };
    }
}

internal static class CloudEventValidation
{
    private static readonly HashSet<string> Reserved = new(StringComparer.Ordinal)
    {
        "specversion", "id", "source", "type", "subject", "time", "datacontenttype", "dataschema", "data",
        "correlationid", "causationid", "traceparent", "tracestate", "negotiationid", "expiresat"
    };

    public static void ValidateDescriptor(CloudEventDescriptor descriptor)
    {
        if (!descriptor.Source.IsAbsoluteUri) throw new ArgumentException("CloudEvent source must be an absolute URI.");
        if (descriptor.DataSchema is { IsAbsoluteUri: false }) throw new ArgumentException("CloudEvent dataschema must be an absolute URI.");
        ArgumentException.ThrowIfNullOrWhiteSpace(descriptor.Type);
        ArgumentException.ThrowIfNullOrWhiteSpace(descriptor.DataContentType);
    }

    public static void ValidateExtensions(CloudEventExtensions extensions)
    {
        foreach (var name in extensions.Additional.Keys)
        {
            if (string.IsNullOrWhiteSpace(name) || name.Any(character => character is not (>= 'a' and <= 'z') and not (>= '0' and <= '9')) || Reserved.Contains(name))
                throw new ArgumentException($"CloudEvent extension name '{name}' must contain only lowercase ASCII letters or digits and must not be reserved.");
        }
    }
}
