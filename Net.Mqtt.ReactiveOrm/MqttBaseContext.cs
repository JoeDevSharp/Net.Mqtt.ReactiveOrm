using Net.Mqtt.ReactiveOrm.Bus.Interfaces;
using Net.Mqtt.ReactiveOrm.Interfaces;
using Net.Mqtt.ReactiveOrm.Models;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace Net.Mqtt.ReactiveOrm;

public abstract class MqttOrmContext
{
    private readonly IMqttBus _bus;
    private readonly ITopicModel _model;
    private readonly IMqttCodec _codec;
    private readonly ConcurrentDictionary<(Type, string), object> _sets = new();

    protected MqttOrmContext(IMqttBus bus, ITopicModel model, IMqttCodec? codec = null)
    {
        _bus = bus ?? throw new ArgumentNullException(nameof(bus));
        _model = model ?? throw new ArgumentNullException(nameof(model));
        _codec = codec ?? new MqttSerializer();
    }

    protected TopicSet<TData> Set<TData>([CallerMemberName] string setName = "") =>
        (TopicSet<TData>)_sets.GetOrAdd((typeof(TData), setName), _ =>
            new TopicSet<TData>(_bus, _codec, _model.GetTopic(typeof(TData), setName)));
}
