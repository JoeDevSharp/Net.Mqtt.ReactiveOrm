using System.Text;
using Net.Mqtt.Infrastructure.CloudEvents;

namespace Net.Mqtt.Infrastructure.Models;

/// <summary>Defines itopic resolver&lt;tdata&gt;.</summary>
public interface ITopicResolver<TData>
{
    /// <summary>Resolves the resolve publish topic operation.</summary>
    string ResolvePublishTopic(TData data);
    /// <summary>Executes the matches subscription operation.</summary>
    bool MatchesSubscription(string topic);
}

/// <summary>Defines imqtt topic policy.</summary>
public interface IMqttTopicPolicy
{
    /// <summary>Validates the validate definition operation.</summary>
    void ValidateDefinition(string? publishTopic, string subscribeFilter, CloudEventDescriptor descriptor, bool dynamic);
    /// <summary>Validates the validate resolved publish topic operation.</summary>
    void ValidateResolvedPublishTopic(string publishTopic, CloudEventDescriptor descriptor);
}

/// <summary>Represents mqtt topic policy options.</summary>
public sealed class MqttTopicPolicyOptions
{
    /// <summary>Gets or sets module namespace.</summary>
    public required string ModuleNamespace { get; set; }
    /// <summary>Gets or sets cloud event source.</summary>
    public required Uri CloudEventSource { get; set; }
    /// <summary>Gets forbidden values.</summary>
    public ISet<string> ForbiddenValues { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    /// <summary>Gets forbidden segment names.</summary>
    public ISet<string> ForbiddenSegmentNames { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    { "secret", "password", "passwd", "token", "credential", "apikey", "connectionstring", "hostname", "host" };
}

/// <summary>Represents mqtt topic policy.</summary>
public sealed class MqttTopicPolicy(MqttTopicPolicyOptions options) : IMqttTopicPolicy
{
    /// <summary>Validates the validate definition operation.</summary>
    public void ValidateDefinition(string? publishTopic, string subscribeFilter, CloudEventDescriptor descriptor, bool dynamic)
    {
        ValidateOptions();
        MqttTopicSyntax.ValidateSubscriptionFilter(subscribeFilter);
        ValidateNamespaceAndSensitiveData(subscribeFilter);
        if (!dynamic)
        {
            if (publishTopic is null) throw new InvalidOperationException("A static topic definition requires PublishTopic.");
            MqttTopicSyntax.ValidatePublishTopic(publishTopic);
            ValidateNamespaceAndSensitiveData(publishTopic);
        }
        else if (publishTopic is not null)
        {
            MqttTopicSyntax.ValidatePublishTopic(publishTopic);
            ValidateNamespaceAndSensitiveData(publishTopic);
        }

        if (descriptor.Source != options.CloudEventSource)
            throw new InvalidOperationException($"CloudEvent source '{descriptor.Source}' does not match configured module identity '{options.CloudEventSource}'.");
    }

    /// <summary>Validates the validate resolved publish topic operation.</summary>
    public void ValidateResolvedPublishTopic(string publishTopic, CloudEventDescriptor descriptor)
    {
        MqttTopicSyntax.ValidatePublishTopic(publishTopic);
        ValidateNamespaceAndSensitiveData(publishTopic);
        if (descriptor.Source != options.CloudEventSource)
            throw new InvalidOperationException($"CloudEvent source '{descriptor.Source}' does not match configured module identity '{options.CloudEventSource}'.");
    }

    private void ValidateNamespaceAndSensitiveData(string topic)
    {
        var expected = options.ModuleNamespace.Trim('/');
        if (!topic.Equals(expected, StringComparison.Ordinal) && !topic.StartsWith(expected + "/", StringComparison.Ordinal))
            throw new InvalidOperationException($"Topic '{topic}' is outside module namespace '{expected}'.");
        foreach (var segment in topic.Split('/'))
        {
            if (options.ForbiddenSegmentNames.Contains(segment) || options.ForbiddenValues.Contains(segment))
                throw new InvalidOperationException($"Topic '{topic}' contains forbidden or sensitive segment '{segment}'.");
            if (Guid.TryParse(segment, out _))
                throw new InvalidOperationException($"Topic '{topic}' contains an instance identifier. Route by stable business topics instead.");
            if (segment.Contains('.') && Uri.CheckHostName(segment) != UriHostNameType.Unknown)
                throw new InvalidOperationException($"Topic '{topic}' contains hostname-like segment '{segment}'.");
        }
    }

    private void ValidateOptions()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(options.ModuleNamespace);
        if (!options.CloudEventSource.IsAbsoluteUri) throw new InvalidOperationException("CloudEventSource must be an absolute URI.");
    }
}

/// <summary>Represents mqtt topic syntax.</summary>
public static class MqttTopicSyntax
{
    /// <summary>Validates the validate publish topic operation.</summary>
    public static void ValidatePublishTopic(string topic)
    {
        ValidateCommon(topic);
        if (topic.Contains('+') || topic.Contains('#') || topic.Contains('@'))
            throw new ArgumentException("PublishTopic cannot contain '+', '#' or '@'.", nameof(topic));
    }

    /// <summary>Validates the validate subscription filter operation.</summary>
    public static void ValidateSubscriptionFilter(string filter)
    {
        ValidateCommon(filter);
        if (filter.Contains('@')) throw new ArgumentException("SubscribeFilter cannot contain ambiguous '@' placeholders.", nameof(filter));
        var levels = filter.Split('/');
        for (var index = 0; index < levels.Length; index++)
        {
            var level = levels[index];
            if (level.Contains('#') && (level != "#" || index != levels.Length - 1))
                throw new ArgumentException("'#' must occupy the final filter level.", nameof(filter));
            if (level.Contains('+') && level != "+")
                throw new ArgumentException("'+' must occupy an entire filter level.", nameof(filter));
        }
    }

    private static void ValidateCommon(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (value.Contains('\0') || Encoding.UTF8.GetByteCount(value) > ushort.MaxValue)
            throw new ArgumentException("MQTT topic is invalid or exceeds 65,535 UTF-8 bytes.", nameof(value));
        if (value.StartsWith('/') || value.EndsWith('/') || value.Contains("//", StringComparison.Ordinal))
            throw new ArgumentException("Empty MQTT topic levels are forbidden by the common profile.", nameof(value));
    }
}
