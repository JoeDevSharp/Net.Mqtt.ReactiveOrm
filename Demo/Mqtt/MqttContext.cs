using Demo.Entities;
using Net.Mqtt.Infrastructure;
using Net.Mqtt.Infrastructure.Enums;
using Net.Mqtt.Infrastructure.Attributes;

namespace Demo.Mqtt;

/// <summary>Declares the typed MQTT topic sets used by the demo application.</summary>
/// <param name="dependencies">The services required by the MQTT ORM context.</param>
public sealed class MqttContext(MqttContextDependencies dependencies) : MqttOrmContext(dependencies)
{
    /// <summary>Gets the topic set used to publish and consume demo sensor readings.</summary>
    [MqttTopic(
        PublishTopic = "factory_64/sensors/DHT230222_Modules/events",
        SubscribeFilter = "factory_64/sensors/DHT230222_Modules/events",
        QoS = MqttQoS.ExactlyOnce,
        Retain = false)]
    public TopicSet<DHT230222_Modules> DHT230222_Modules => Set<DHT230222_Modules>();
}
