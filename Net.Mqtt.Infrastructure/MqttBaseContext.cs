using Net.Mqtt.Infrastructure.Bus.Interfaces;
using Net.Mqtt.Infrastructure.Interfaces;
using Net.Mqtt.Infrastructure.Models;
using Net.Mqtt.Infrastructure.CloudEvents;
using Net.Mqtt.Infrastructure.Contracts;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace Net.Mqtt.Infrastructure;

/// <summary>Groups the services required by an MQTT ORM context.</summary>
/// <param name="Bus">The shared MQTT transport.</param>
/// <param name="Topics">The explicit topic model.</param>
/// <param name="CloudEventFactory">The CloudEvent factory.</param>
/// <param name="CloudEventCodec">The structured CloudEvent codec.</param>
/// <param name="Contracts">The governed event-contract registry.</param>
/// <param name="Validator">The event-data validator.</param>
public sealed record MqttContextDependencies(
    IMqttBus Bus,
    ITopicModel Topics,
    ICloudEventFactory CloudEventFactory,
    ICloudEventCodec CloudEventCodec,
    IEventContractRegistry Contracts,
    IEventDataValidator Validator);

/// <summary>Represents mqtt orm context.</summary>
public abstract class MqttOrmContext
{
    private readonly IMqttBus _bus;
    private readonly ITopicModel _model;
    private readonly ICloudEventFactory _cloudEventFactory;
    private readonly ICloudEventCodec _cloudEventCodec;
    private readonly IEventContractRegistry? _contractRegistry;
    private readonly IEventDataValidator? _dataValidator;
    private readonly ConcurrentDictionary<(Type, string), object> _sets = new();

    /// <summary>Executes the mqtt orm context operation.</summary>
    protected MqttOrmContext(MqttContextDependencies dependencies)
        : this(dependencies.Bus, dependencies.Topics, dependencies.CloudEventFactory,
            dependencies.CloudEventCodec, dependencies.Contracts, dependencies.Validator)
    {
    }

    /// <summary>Executes the mqtt orm context operation.</summary>
    protected MqttOrmContext(IMqttBus bus, ITopicModel model,
        ICloudEventFactory? cloudEventFactory = null, ICloudEventCodec? cloudEventCodec = null,
        IEventContractRegistry? contractRegistry = null, IEventDataValidator? dataValidator = null)
    {
        _bus = bus ?? throw new ArgumentNullException(nameof(bus));
        _model = model ?? throw new ArgumentNullException(nameof(model));
        _cloudEventFactory = cloudEventFactory ?? new CloudEventFactory();
        _cloudEventCodec = cloudEventCodec ?? new JsonCloudEventCodec();
        _contractRegistry = contractRegistry;
        _dataValidator = dataValidator;
    }

    /// <summary>Sets the set&lt;tdata&gt; operation.</summary>
    protected TopicSet<TData> Set<TData>([CallerMemberName] string setName = "") =>
        (TopicSet<TData>)_sets.GetOrAdd((typeof(TData), setName), _ =>
            new TopicSet<TData>(_bus, _cloudEventFactory, _cloudEventCodec,
                _contractRegistry ?? throw new InvalidOperationException("IEventContractRegistry is required."),
                _dataValidator ?? throw new InvalidOperationException("IEventDataValidator is required."),
                _model.GetTopic(typeof(TData), setName)));
}
