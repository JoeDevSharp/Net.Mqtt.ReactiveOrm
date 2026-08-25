using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MQTTnet.Formatter;
using Net.Mqtt.ReactiveOrm.Bus;
using Net.Mqtt.ReactiveOrm.Bus.Interfaces;
using Net.Mqtt.ReactiveOrm.CloudEvents;
using Net.Mqtt.ReactiveOrm.Contracts;
using Net.Mqtt.ReactiveOrm.Models;

namespace Net.Mqtt.ReactiveOrm.Hosting;

public interface IMqttContractPackage
{
    void Register(EventContractRegistryBuilder contracts, MqttSchemaBuilder schemas);
}

public sealed class MqttSchemaBuilder
{
    private readonly InMemoryJsonSchemaResolver _inline = new();
    private readonly List<IJsonSchemaResolver> _resolvers = [];

    public MqttSchemaBuilder AddInline(string uri, string jsonSchema, string version)
    {
        _inline.Add(new Uri(uri, UriKind.Absolute), jsonSchema, version);
        return this;
    }

    public MqttSchemaBuilder Use(IJsonSchemaResolver resolver)
    {
        _resolvers.Add(resolver);
        return this;
    }

    internal IJsonSchemaResolver Build()
    {
        var all = new List<IJsonSchemaResolver> { _inline };
        all.AddRange(_resolvers);
        return new CompositeJsonSchemaResolver([.. all]);
    }
}

public sealed class MqttReactiveOrmBuilder<TContext> where TContext : MqttOrmContext
{
    private readonly IServiceCollection _services;
    private readonly MqttReactiveOrmOptions _mqtt = new() { ClientId = string.Empty };
    private readonly EventContractRegistryBuilder _contracts = new();
    private readonly MqttSchemaBuilder _schemas = new();
    private readonly MqttTopicPolicyOptions _topics = new()
    {
        ModuleNamespace = string.Empty,
        CloudEventSource = new Uri("urn:unset")
    };
    private bool _inMemory;
    private int _schemaCacheCapacity = 64;

    internal MqttReactiveOrmBuilder(IServiceCollection services) => _services = services;
    public MqttReactiveOrmOptions Advanced => _mqtt;

    public MqttReactiveOrmBuilder<TContext> ConnectTo(string server, int port = 1883, MqttTransport transport = MqttTransport.Tcp)
    { _mqtt.Server = server; _mqtt.Port = port; _mqtt.Transport = transport; return this; }
    public MqttReactiveOrmBuilder<TContext> IdentifyAs(string clientId)
    { _mqtt.ClientId = clientId; return this; }
    public MqttReactiveOrmBuilder<TContext> UseMqtt5()
    { _mqtt.ProtocolVersion = MqttProtocolVersion.V500; return this; }
    public MqttReactiveOrmBuilder<TContext> UseMqtt311()
    { _mqtt.ProtocolVersion = MqttProtocolVersion.V311; return this; }
    public MqttReactiveOrmBuilder<TContext> ForModule(string moduleNamespace)
    { _topics.ModuleNamespace = moduleNamespace; return this; }
    public MqttReactiveOrmBuilder<TContext> WithCloudEventSource(string source)
    { _topics.CloudEventSource = new Uri(source, UriKind.Absolute); return this; }
    public MqttReactiveOrmBuilder<TContext> UseContracts(Action<EventContractRegistryBuilder> configure)
    { configure(_contracts); return this; }
    public MqttReactiveOrmBuilder<TContext> UseSchemas(Action<MqttSchemaBuilder> configure)
    { configure(_schemas); return this; }
    public MqttReactiveOrmBuilder<TContext> UseSchemaResolver(IJsonSchemaResolver resolver, int cacheCapacity = 64)
    { _schemas.Use(resolver); _schemaCacheCapacity = cacheCapacity; return this; }
    public MqttReactiveOrmBuilder<TContext> UseContractPackage<TPackage>() where TPackage : IMqttContractPackage, new()
    { new TPackage().Register(_contracts, _schemas); return this; }
    public MqttReactiveOrmBuilder<TContext> UsePersistentSession(TimeSpan expiry)
    { _mqtt.Session.CleanStart = false; _mqtt.Session.Expiry = expiry; return this; }
    public MqttReactiveOrmBuilder<TContext> UseExponentialReconnect(TimeSpan? initial = null, TimeSpan? maximum = null)
    { _mqtt.Reconnect.UseExponentialBackoff(initial, maximum); return this; }
    public MqttReactiveOrmBuilder<TContext> UseUnavailableLastWill(string? topic = null)
    { _mqtt.LastWill.UseServiceUnavailableCloudEvent(topic); return this; }
    public MqttReactiveOrmBuilder<TContext> UseMutualTls(Action<MutualTlsOptions> configure)
    { _mqtt.Security.UseMutualTls(configure); return this; }
    public MqttReactiveOrmBuilder<TContext> UseInMemoryTransport()
    { _inMemory = true; return this; }
    public MqttReactiveOrmBuilder<TContext> ForbidTopicValue(string value)
    { _topics.ForbiddenValues.Add(value); return this; }

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

    public MqttReactiveOrmBuilder<TContext> UseDevelopmentDefaults()
    {
        UseMqtt5();
        // Development frequently changes contracts and wire formats. A clean session
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
        var registry = _contracts.Build();
        var schemaResolver = new CachingJsonSchemaResolver(_schemas.Build(), _schemaCacheCapacity);

        _services.TryAddSingleton(_mqtt);
        _services.TryAddSingleton<ICloudEventFactory, CloudEventFactory>();
        _services.TryAddSingleton<ICloudEventCodec, JsonCloudEventCodec>();
        _services.AddSingleton<IEventContractRegistry>(registry);
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
