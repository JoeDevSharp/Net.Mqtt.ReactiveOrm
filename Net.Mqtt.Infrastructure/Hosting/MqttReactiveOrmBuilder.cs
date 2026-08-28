using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MQTTnet.Formatter;
using Net.Mqtt.Infrastructure.Bus;
using Net.Mqtt.Infrastructure.Bus.Interfaces;
using Net.Mqtt.Infrastructure.CloudEvents;
using Net.Mqtt.Infrastructure.Contracts;
using Net.Mqtt.Infrastructure.Models;

namespace Net.Mqtt.Infrastructure.Hosting;

/// <summary>Defines a package of attributed Event Entities and their schemas.</summary>
public interface IMqttEventEntityPackage
{
    /// <summary>Registers the register operation.</summary>
    void Register(EventEntityRegistryBuilder eventEntities, MqttSchemaBuilder schemas);
}

/// <summary>Represents mqtt schema builder.</summary>
public sealed class MqttSchemaBuilder
{
    private readonly InMemoryJsonSchemaResolver _inline = new();
    private readonly Dictionary<Uri, string> _files = [];
    private readonly List<IJsonSchemaResolver> _resolvers = [];

    /// <summary>Adds the add inline operation.</summary>
    public MqttSchemaBuilder AddInline(string uri, string jsonSchema, string version)
    {
        _inline.Add(new Uri(uri, UriKind.Absolute), jsonSchema, version);
        return this;
    }

    /// <summary>Registers a governed JSON Schema stored in a local file.</summary>
    /// <param name="uri">The absolute dataschema URI used by CloudEvents.</param>
    /// <param name="filePath">The path of the JSON Schema file.</param>
    /// <returns>This builder so additional schemas can be registered.</returns>
    public MqttSchemaBuilder Add(string uri, string filePath)
    {
        var schemaUri = new Uri(uri, UriKind.Absolute);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        _files[schemaUri] = Path.GetFullPath(filePath);
        return this;
    }

    /// <summary>Configures the use operation.</summary>
    public MqttSchemaBuilder Use(IJsonSchemaResolver resolver)
    {
        _resolvers.Add(resolver);
        return this;
    }

    internal IJsonSchemaResolver Build()
    {
        var all = new List<IJsonSchemaResolver> { _inline };
        if (_files.Count > 0) all.Add(new FileJsonSchemaResolver(_files));
        all.AddRange(_resolvers);
        return new CompositeJsonSchemaResolver([.. all]);
    }
}

/// <summary>Represents mqtt reactive orm builder&lt;tcontext&gt;.</summary>
public sealed class MqttReactiveOrmBuilder<TContext> where TContext : MqttOrmContext
{
    private readonly IServiceCollection _services;
    private readonly MqttReactiveOrmOptions _mqtt = new() { ClientId = string.Empty };
    private readonly EventEntityRegistryBuilder _eventEntities = new();
    private readonly MqttSchemaBuilder _schemas = new();
    private readonly MqttTopicPolicyOptions _topics = new()
    {
        ModuleNamespace = string.Empty,
        CloudEventSource = new Uri("urn:unset")
    };
    private bool _inMemory;
    private int _schemaCacheCapacity = 64;

    internal MqttReactiveOrmBuilder(IServiceCollection services) => _services = services;
    /// <summary>Gets advanced.</summary>
    public MqttReactiveOrmOptions Advanced => _mqtt;

    /// <summary>Connects the connect to operation.</summary>
    public MqttReactiveOrmBuilder<TContext> ConnectTo(string server, int port = 1883, MqttTransport transport = MqttTransport.Tcp)
    { _mqtt.Server = server; _mqtt.Port = port; _mqtt.Transport = transport; return this; }
    /// <summary>Executes the identify as operation.</summary>
    public MqttReactiveOrmBuilder<TContext> IdentifyAs(string clientId)
    { _mqtt.ClientId = clientId; return this; }
    /// <summary>Configures the use mqtt5 operation.</summary>
    public MqttReactiveOrmBuilder<TContext> UseMqtt5()
    { _mqtt.ProtocolVersion = MqttProtocolVersion.V500; return this; }
    /// <summary>Configures the use mqtt311 operation.</summary>
    public MqttReactiveOrmBuilder<TContext> UseMqtt311()
    { _mqtt.ProtocolVersion = MqttProtocolVersion.V311; return this; }
    /// <summary>Executes the for module operation.</summary>
    public MqttReactiveOrmBuilder<TContext> ForModule(string moduleNamespace)
    { _topics.ModuleNamespace = moduleNamespace; return this; }
    /// <summary>Prefixes every relative publish topic and subscription filter.</summary>
    public MqttReactiveOrmBuilder<TContext> WithBaseTopic(string baseTopic)
    { _topics.BaseTopic = baseTopic; return this; }
    /// <summary>Executes the with cloud event source operation.</summary>
    public MqttReactiveOrmBuilder<TContext> WithCloudEventSource(string source)
    { _topics.CloudEventSource = new Uri(source, UriKind.Absolute); return this; }
    /// <summary>Registers attributed Event Entity types.</summary>
    public MqttReactiveOrmBuilder<TContext> UseEventEntities(Action<EventEntityRegistryBuilder> configure)
    { configure(_eventEntities); return this; }
    /// <summary>Configures the use schemas operation.</summary>
    public MqttReactiveOrmBuilder<TContext> UseSchemas(Action<MqttSchemaBuilder> configure)
    { configure(_schemas); return this; }
    /// <summary>Configures the use schema resolver operation.</summary>
    public MqttReactiveOrmBuilder<TContext> UseSchemaResolver(IJsonSchemaResolver resolver, int cacheCapacity = 64)
    { _schemas.Use(resolver); _schemaCacheCapacity = cacheCapacity; return this; }
    /// <summary>Registers an Event Entity package and its schemas.</summary>
    public MqttReactiveOrmBuilder<TContext> UseEventEntityPackage<TPackage>() where TPackage : IMqttEventEntityPackage, new()
    { new TPackage().Register(_eventEntities, _schemas); return this; }
    /// <summary>Configures the use persistent session operation.</summary>
    public MqttReactiveOrmBuilder<TContext> UsePersistentSession(TimeSpan expiry)
    { _mqtt.Session.CleanStart = false; _mqtt.Session.Expiry = expiry; return this; }
    /// <summary>Configures the use exponential reconnect operation.</summary>
    public MqttReactiveOrmBuilder<TContext> UseExponentialReconnect(TimeSpan? initial = null, TimeSpan? maximum = null)
    { _mqtt.Reconnect.UseExponentialBackoff(initial, maximum); return this; }
    /// <summary>Configures the use unavailable last will operation.</summary>
    public MqttReactiveOrmBuilder<TContext> UseUnavailableLastWill(string? topic = null)
    { _mqtt.LastWill.UseServiceUnavailableCloudEvent(topic); return this; }
    /// <summary>Configures the use mutual tls operation.</summary>
    public MqttReactiveOrmBuilder<TContext> UseMutualTls(Action<MutualTlsOptions> configure)
    { _mqtt.Security.UseMutualTls(configure); return this; }
    /// <summary>Configures the use in memory transport operation.</summary>
    public MqttReactiveOrmBuilder<TContext> UseInMemoryTransport()
    { _inMemory = true; return this; }
    /// <summary>Forbids the forbid topic value operation.</summary>
    public MqttReactiveOrmBuilder<TContext> ForbidTopicValue(string value)
    { _topics.ForbiddenValues.Add(value); return this; }

    /// <summary>Configures the use production defaults operation.</summary>
    public MqttReactiveOrmBuilder<TContext> UseProductionDefaults()
    {
        UseMqtt5();
        UsePersistentSession(TimeSpan.FromHours(24));
        UseExponentialReconnect(TimeSpan.FromSeconds(1), TimeSpan.FromMinutes(1));
        UseUnavailableLastWill();
        _mqtt.KeepAlive = TimeSpan.FromSeconds(30);
        _mqtt.Timeout = TimeSpan.FromSeconds(10);
        _mqtt.MaximumPacketSize = 1024 * 1024;
        _mqtt.ReceiveMaximum = 32;
        return this;
    }

    /// <summary>Configures the use development defaults operation.</summary>
    public MqttReactiveOrmBuilder<TContext> UseDevelopmentDefaults()
    {
        UseMqtt5();
        // Development frequently changes Event Entities and wire formats. A clean session
        // prevents queued messages from an older build being dispatched to the new codec.
        _mqtt.Session.CleanStart = true;
        _mqtt.Session.Expiry = TimeSpan.Zero;
        UseExponentialReconnect(TimeSpan.FromMilliseconds(500), TimeSpan.FromSeconds(10));
        return this;
    }

    internal void Register()
    {
        if (!_inMemory) _mqtt.Validate();
        if (string.IsNullOrWhiteSpace(_topics.ModuleNamespace)) throw new InvalidOperationException("ForModule() is required.");
        if (_topics.CloudEventSource.OriginalString == "urn:unset") throw new InvalidOperationException("WithCloudEventSource() is required.");
        var registry = _eventEntities.Build();
        var schemaResolver = new CachingJsonSchemaResolver(_schemas.Build(), _schemaCacheCapacity);

        _services.TryAddSingleton(_mqtt);
        _services.TryAddSingleton<ICloudEventFactory, CloudEventFactory>();
        _services.TryAddSingleton<ICloudEventCodec, JsonCloudEventCodec>();
        _services.AddSingleton<IEventEntityRegistry>(registry);
        _services.AddSingleton<IJsonSchemaResolver>(schemaResolver);
        _services.AddSingleton<IEventDataValidator, JsonSchemaEventDataValidator>();
        if (_inMemory) _services.TryAddSingleton<IMqttBus, InMemoryMqttBus>();
        else _services.TryAddSingleton<IMqttBus>(provider => new MqttNetBus(provider.GetRequiredService<MqttReactiveOrmOptions>()));
        _services.AddSingleton<ITopicModel>(provider => new TopicModelBuilder(new MqttTopicPolicy(_topics))
            .AddAttributedContext<TContext>(registry, _topics.CloudEventSource,
                resolverType => provider.GetRequiredService(resolverType)).Build());
        _services.TryAddSingleton<MqttContextDependencies>();
        _services.TryAddSingleton<TContext>();
        _services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, MqttReactiveOrmHostedService>());
    }
}
