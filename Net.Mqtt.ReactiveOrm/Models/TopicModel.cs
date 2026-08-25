using System.Reflection;
using Net.Mqtt.ReactiveOrm.Attributes;
using Net.Mqtt.ReactiveOrm.CloudEvents;
using Net.Mqtt.ReactiveOrm.Enums;
using Net.Mqtt.ReactiveOrm.Contracts;

namespace Net.Mqtt.ReactiveOrm.Models;

public sealed record TopicDefinition
{
    private readonly object? _resolver;
    private readonly IMqttTopicPolicy _policy;

    internal TopicDefinition(string? publishTopic, string subscribeFilter, QoSLevel qos, bool retain,
        CloudEventDescriptor cloudEvent, object? resolver, IMqttTopicPolicy policy)
    {
        PublishTopic = publishTopic;
        SubscribeFilter = subscribeFilter;
        QoS = qos;
        Retain = retain;
        CloudEvent = cloudEvent;
        _resolver = resolver;
        _policy = policy;
    }

    public string? PublishTopic { get; }
    public string SubscribeFilter { get; }
    public QoSLevel QoS { get; }
    public bool Retain { get; }
    public CloudEventDescriptor CloudEvent { get; }
    [Obsolete("Use SubscribeFilter or PublishTopic explicitly.")]
    public string Template => SubscribeFilter;

    public string ResolvePublishTopic<TData>(TData data)
    {
        var topic = _resolver is ITopicResolver<TData> resolver
            ? resolver.ResolvePublishTopic(data)
            : PublishTopic ?? throw new InvalidOperationException("Dynamic topic definition has no compatible resolver.");
        _policy.ValidateResolvedPublishTopic(topic, CloudEvent);
        return topic;
    }

    public bool MatchesSubscription<TData>(string topic) =>
        _resolver is not ITopicResolver<TData> resolver || resolver.MatchesSubscription(topic);
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

public sealed class TopicModelBuilder(IMqttTopicPolicy policy)
{
    private readonly Dictionary<(Type, string), TopicDefinition> _topics = new();

    [Obsolete("Use the overload that declares PublishTopic and SubscribeFilter separately.")]
    public TopicModelBuilder Add<TData>(string setName, string topic, QoSLevel qos = QoSLevel.AtMostOnce,
        bool retain = false, CloudEventDescriptor? cloudEvent = null) =>
        Add<TData>(setName, topic, topic, qos, retain, cloudEvent);

    public TopicModelBuilder Add<TData>(string setName, string? publishTopic, string subscribeFilter,
        QoSLevel qos = QoSLevel.AtMostOnce, bool retain = false, CloudEventDescriptor? cloudEvent = null,
        ITopicResolver<TData>? resolver = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(setName);
        if (cloudEvent is null)
            throw new InvalidOperationException("An explicit CloudEventDescriptor is required; event type cannot be derived from TData.");
        CloudEventValidation.ValidateDescriptor(cloudEvent);
        var dynamic = resolver is not null;
        policy.ValidateDefinition(publishTopic, subscribeFilter, cloudEvent, dynamic);
        var definition = new TopicDefinition(publishTopic, subscribeFilter, qos, retain, cloudEvent, resolver, policy);
        if (!_topics.TryAdd((typeof(TData), setName), definition))
            throw new InvalidOperationException($"TopicSet '{setName}' for {typeof(TData).Name} is already registered.");
        return this;
    }

    public TopicModelBuilder AddAttributedContext<TContext>(
        Func<Type, CloudEventDescriptor> descriptorResolver,
        Func<Type, object>? resolverFactory = null)
    {
        foreach (var property in typeof(TContext).GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            var attribute = property.GetCustomAttribute<MqttTopicAttribute>();
            if (attribute is null) continue;
            if (!property.PropertyType.IsGenericType || property.PropertyType.GetGenericTypeDefinition() != typeof(TopicSet<>))
                throw new InvalidOperationException($"[MqttTopic] property '{property.Name}' must be TopicSet<TData>.");
            var dataType = property.PropertyType.GetGenericArguments()[0];
            var descriptor = descriptorResolver(dataType)
                ?? throw new InvalidOperationException($"No CloudEvent descriptor was supplied for '{dataType.FullName}'.");
            object? resolver = null;
            if (attribute.ResolverType is not null)
                resolver = resolverFactory?.Invoke(attribute.ResolverType) ?? Activator.CreateInstance(attribute.ResolverType)
                    ?? throw new InvalidOperationException($"Cannot create topic resolver '{attribute.ResolverType}'.");
            AddUntyped(dataType, property.Name, attribute, descriptor, resolver);
        }
        return this;
    }

    public TopicModelBuilder AddAttributedContext<TContext>(
        IEventContractRegistry contracts,
        Uri cloudEventSource,
        Func<Type, object>? resolverFactory = null) =>
        AddAttributedContext<TContext>(dataType =>
        {
            var contract = contracts.GetByDataType(dataType);
            return new CloudEventDescriptor(cloudEventSource, contract.EventType, DataSchema: contract.DataSchema);
        }, resolverFactory);

    public TopicModel Build() => new(new Dictionary<(Type, string), TopicDefinition>(_topics));

    private void AddUntyped(Type dataType, string setName, MqttTopicAttribute attribute,
        CloudEventDescriptor descriptor, object? resolver)
    {
        if (resolver is not null && !typeof(ITopicResolver<>).MakeGenericType(dataType).IsInstanceOfType(resolver))
            throw new InvalidOperationException($"Resolver '{resolver.GetType()}' does not implement ITopicResolver<{dataType.Name}>.");
        policy.ValidateDefinition(attribute.PublishTopic, attribute.SubscribeFilter, descriptor, resolver is not null);
        if (!_topics.TryAdd((dataType, setName), new(attribute.PublishTopic, attribute.SubscribeFilter,
            (QoSLevel)attribute.QoS, attribute.Retain, descriptor, resolver, policy)))
            throw new InvalidOperationException($"TopicSet '{setName}' for {dataType.Name} is already registered.");
    }
}
