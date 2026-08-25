using Net.Mqtt.ReactiveOrm.Bus.Interfaces;
using Net.Mqtt.ReactiveOrm.Interfaces;
using Net.Mqtt.ReactiveOrm.Models;
using Net.Mqtt.ReactiveOrm.CloudEvents;
using Net.Mqtt.ReactiveOrm.Contracts;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace Net.Mqtt.ReactiveOrm;

public abstract class MqttOrmContext
{
    private readonly IMqttBus _bus;
    private readonly ITopicModel _model;
    private readonly ICloudEventFactory _cloudEventFactory;
    private readonly ICloudEventCodec _cloudEventCodec;
    private readonly IEventContractRegistry? _contractRegistry;
    private readonly IEventDataValidator? _dataValidator;
    private readonly ConcurrentDictionary<(Type, string), object> _sets = new();

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

    protected TopicSet<TData> Set<TData>([CallerMemberName] string setName = "") =>
        (TopicSet<TData>)_sets.GetOrAdd((typeof(TData), setName), _ =>
            new TopicSet<TData>(_bus, _cloudEventFactory, _cloudEventCodec,
                _contractRegistry ?? throw new InvalidOperationException("IEventContractRegistry is required."),
                _dataValidator ?? throw new InvalidOperationException("IEventDataValidator is required."),
                _model.GetTopic(typeof(TData), setName)));
}
