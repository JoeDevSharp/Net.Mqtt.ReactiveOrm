using Demo.Entities;
using Net.Mqtt.ReactiveOrm;
using Net.Mqtt.ReactiveOrm.Bus.Interfaces;
using Net.Mqtt.ReactiveOrm.Enums;
using Net.Mqtt.ReactiveOrm.Models;

namespace Demo.Mqtt;

public sealed class MqttContext(IMqttBus bus, ITopicModel model) : MqttOrmContext(bus, model)
{
    public TopicSet<DHT230222_Modules> DHT230222_Modules => Set<DHT230222_Modules>();
    public TopicSet<DHT230222_Modules> Z_XTR_Modules => Set<DHT230222_Modules>();
    public TopicSet<double> TemperatureRaw => Set<double>();
    public TopicSet<DHT230222_Modules> AllSensors => Set<DHT230222_Modules>();
    public TopicSet<DHT230222_Modules> AllSensorMessages => Set<DHT230222_Modules>();

    public static TopicModel CreateModel() => new TopicModelBuilder()
        .Add<DHT230222_Modules>(nameof(DHT230222_Modules), "factory_64/sensors/@/events", QoSLevel.ExactlyOnce)
        .Add<DHT230222_Modules>(nameof(Z_XTR_Modules), "factory_64/module/@/status", QoSLevel.AtLeastOnce)
        .Add<double>(nameof(TemperatureRaw), "factory_64/sensors/temperature/value")
        .Add<DHT230222_Modules>(nameof(AllSensors), "factory_64/+/+/status")
        .Add<DHT230222_Modules>(nameof(AllSensorMessages), "factory_64/sensors/#")
        .Build();
}
