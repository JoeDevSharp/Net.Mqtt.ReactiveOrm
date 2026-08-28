using System.Reflection;
using Net.Mqtt.Infrastructure.Attributes;
using Net.Mqtt.Infrastructure.CloudEvents;
using Net.Mqtt.Infrastructure.Enums;
using Net.Mqtt.Infrastructure.Contracts;

namespace Net.Mqtt.Infrastructure.Models;

/// <summary>Represents topic definition.</summary>
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

    /// <summary>Gets publish topic.</summary>
    public string? PublishTopic { get; }
    /// <summary>Gets subscribe filter.</summary>
    public string SubscribeFilter { get; }
    /// <summary>Gets qo s.</summary>
    public QoSLevel QoS { get; }
    /// <summary>Gets retain.</summary>
    public bool Retain { get; }
    /// <summary>Gets cloud event.</summary>
    public CloudEventDescriptor CloudEvent { get; }
    /// <summary>Gets template.</summary>
    [Obsolete("Use SubscribeFilter or PublishTopic explicitly.")]
    public string Template => SubscribeFilter;

    /// <summary>Resolves the resolve publish topic&lt;tdata&gt; operation.</summary>
    public string ResolvePublishTopic<TData>(TData data)
    {
        var relativeTopic = _resolver is ITopicResolver<TData> resolver
            ? resolver.ResolvePublishTopic(data)
            : PublishTopic ?? throw new InvalidOperationException("Dynamic topic definition has no compatible resolver.");
        var topic = _policy.ResolveTopic(relativeTopic);
        _policy.ValidateResolvedPublishTopic(topic, CloudEvent);
        return topic;
    }

    /// <summary>Executes the matches subscription&lt;tdata&gt; operation.</summary>
    public bool MatchesSubscription<TData>(string topic) =>
        _resolver is not ITopicResolver<TData> resolver || resolver.MatchesSubscription(_policy.ToRelativeTopic(topic));
}

/// <summary>Defines itopic model.</summary>
public interface ITopicModel
{
    /// <summary>Gets the topic definition registered for a CLR type and context property.</summary>
    TopicDefinition GetTopic(Type dataType, string setName);
}

/// <summary>Represents topic model.</summary>
public sealed class TopicModel : ITopicModel
{
    private readonly IReadOnlyDictionary<(Type Type, string Name), TopicDefinition> _topics;
    internal TopicModel(IReadOnlyDictionary<(Type, string), TopicDefinition> topics) => _topics = topics;
    /// <summary>Gets the get topic operation.</summary>
    public TopicDefinition GetTopic(Type dataType, string setName) =>
        _topics.TryGetValue((dataType, setName), out var topic) ? topic
        : throw new InvalidOperationException($"TopicSet '{setName}' for {dataType.Name} is not registered.");
}

/// <summary>Represents topic model builder.</summary>
public sealed class TopicModelBuilder(IMqttTopicPolicy policy)
{
    private readonly Dictionary<(Type, string), TopicDefinition> _topics = new();

    /// <summary>Adds the add&lt;tdata&gt; operation.</summary>
    [Obsolete("Use the overload that declares PublishTopic and SubscribeFilter separately.")]
    public TopicModelBuilder Add<TData>(string setName, string topic, QoSLevel qos = QoSLevel.AtMostOnce,
        bool retain = false, CloudEventDescriptor? cloudEvent = null) =>
        Add<TData>(setName, topic, topic, qos, retain, cloudEvent);

    /// <summary>Adds the add&lt;tdata&gt; operation.</summary>
    public TopicModelBuilder Add<TData>(string setName, string? publishTopic, string subscribeFilter,
        QoSLevel qos = QoSLevel.AtMostOnce, bool retain = false, CloudEventDescriptor? cloudEvent = null,
        ITopicResolver<TData>? resolver = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(setName);
        if (cloudEvent is null)
            throw new InvalidOperationException("An explicit CloudEventDescriptor is required; event type cannot be derived from TData.");
        CloudEventValidation.ValidateDescriptor(cloudEvent);
        var dynamic = resolver is not null;
        var resolvedPublishTopic = publishTopic is null ? null : policy.ResolveTopic(publishTopic);
        var resolvedSubscribeFilter = policy.ResolveTopic(subscribeFilter);
        policy.ValidateDefinition(resolvedPublishTopic, resolvedSubscribeFilter, cloudEvent, dynamic);
        var definition = new TopicDefinition(resolvedPublishTopic, resolvedSubscribeFilter, qos, retain, cloudEvent, resolver, policy);
        if (!_topics.TryAdd((typeof(TData), setName), definition))
            throw new InvalidOperationException($"TopicSet '{setName}' for {typeof(TData).Name} is already registered.");
        return this;
    }

    /// <summary>Adds the add attributed context&lt;tcontext&gt; operation.</summary>
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

    /// <summary>Adds the add attributed context&lt;tcontext&gt; operation.</summary>
    public TopicModelBuilder AddAttributedContext<TContext>(
        IEventEntityRegistry eventEntities,
        Uri cloudEventSource,
        Func<Type, object>? resolverFactory = null) =>
        AddAttributedContext<TContext>(dataType =>
        {
            var contract = eventEntities.GetByDataType(dataType);
            return new CloudEventDescriptor(cloudEventSource, contract.EventType, DataSchema: contract.DataSchema);
        }, resolverFactory);

    /// <summary>Creates the build operation.</summary>
    public TopicModel Build() => new(new Dictionary<(Type, string), TopicDefinition>(_topics));

    private void AddUntyped(Type dataType, string setName, MqttTopicAttribute attribute,
        CloudEventDescriptor descriptor, object? resolver)
    {
        if (resolver is not null && !typeof(ITopicResolver<>).MakeGenericType(dataType).IsInstanceOfType(resolver))
            throw new InvalidOperationException($"Resolver '{resolver.GetType()}' does not implement ITopicResolver<{dataType.Name}>.");
        var publishTopic = attribute.PublishTopic is null ? null : policy.ResolveTopic(attribute.PublishTopic);
        var subscribeFilter = policy.ResolveTopic(attribute.SubscribeFilter);
        policy.ValidateDefinition(publishTopic, subscribeFilter, descriptor, resolver is not null);
        if (!_topics.TryAdd((dataType, setName), new(publishTopic, subscribeFilter,
            (QoSLevel)attribute.QoS, attribute.Retain, descriptor, resolver, policy)))
            throw new InvalidOperationException($"TopicSet '{setName}' for {dataType.Name} is already registered.");
    }
}
