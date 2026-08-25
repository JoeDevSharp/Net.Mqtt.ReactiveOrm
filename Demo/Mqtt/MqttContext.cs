using Demo.Entities;
using Net.Mqtt.ReactiveOrm;
using Net.Mqtt.ReactiveOrm.Enums;
using Net.Mqtt.ReactiveOrm.Attributes;

namespace Demo.Mqtt;

public sealed class MqttContext(MqttContextDependencies dependencies) : MqttOrmContext(dependencies)
{
    [MqttTopic(
        PublishTopic = "factory_64/sensors/DHT230222_Modules/events",
        SubscribeFilter = "factory_64/sensors/DHT230222_Modules/events",
        QoS = MqttQoS.ExactlyOnce,
        Retain = false)]
    public TopicSet<DHT230222_Modules> DHT230222_Modules => Set<DHT230222_Modules>();
}
