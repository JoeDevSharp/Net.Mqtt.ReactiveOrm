using Net.Mqtt.ReactiveOrm.Enums;

namespace Net.Mqtt.ReactiveOrm.Models;

public sealed record TopicDefinition(string Template, QoSLevel QoS = QoSLevel.AtMostOnce, bool Retain = false)
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
    public TopicModelBuilder Add<T>(string setName, string template, QoSLevel qos = QoSLevel.AtMostOnce, bool retain = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(setName);
        ArgumentException.ThrowIfNullOrWhiteSpace(template);
        if (!_topics.TryAdd((typeof(T), setName), new(template, qos, retain)))
            throw new InvalidOperationException($"TopicSet '{setName}' for {typeof(T).Name} is already registered.");
        return this;
    }
    public TopicModel Build() => new(new Dictionary<(Type, string), TopicDefinition>(_topics));
}
