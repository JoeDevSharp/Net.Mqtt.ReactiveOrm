using Net.Mqtt.ReactiveOrm.Enums;
using Net.Mqtt.ReactiveOrm.CloudEvents;

namespace Net.Mqtt.ReactiveOrm.Models;

public sealed record TopicDefinition(string Template, QoSLevel QoS = QoSLevel.AtMostOnce, bool Retain = false,
    CloudEventDescriptor? CloudEvent = null)
{
    public string Resolve<T>() => Template.Replace("@", typeof(T).Name, StringComparison.Ordinal);
}

public interface ITopicModel { TopicDefinition GetTopic(Type dataType, string setName); }

public sealed class TopicModel : ITopicModel
{
    private readonly IReadOnlyDictionary<(Type Type, string Name), TopicDefinition> _topics;
    internal TopicModel(IReadOnlyDictionary<(Type, string), TopicDefinition> topics) => _topics = topics;
    public TopicDefinition GetTopic(Type dataType, string setName) =>
        _topics.TryGetValue((dataType, setName), out var topic) ? topic
        : throw new InvalidOperationException($"TopicSet '{setName}' for {dataType.Name} is not registered.");
}

public sealed class TopicModelBuilder
{
    private readonly Dictionary<(Type, string), TopicDefinition> _topics = new();
    public TopicModelBuilder Add<T>(string setName, string template, QoSLevel qos = QoSLevel.AtMostOnce, bool retain = false,
        CloudEventDescriptor? cloudEvent = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(setName);
        ArgumentException.ThrowIfNullOrWhiteSpace(template);
        cloudEvent ??= new CloudEventDescriptor(
            new Uri($"urn:net.mqtt.reactiveorm:{Uri.EscapeDataString(typeof(T).FullName ?? typeof(T).Name)}"),
            typeof(T).FullName ?? typeof(T).Name);
        CloudEventValidation.ValidateDescriptor(cloudEvent);
        if (!_topics.TryAdd((typeof(T), setName), new(template, qos, retain, cloudEvent)))
            throw new InvalidOperationException($"TopicSet '{setName}' for {typeof(T).Name} is already registered.");
        return this;
    }
    public TopicModel Build() => new(new Dictionary<(Type, string), TopicDefinition>(_topics));
}
