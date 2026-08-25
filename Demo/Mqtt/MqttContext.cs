using Demo.Entities;
using Net.Mqtt.ReactiveOrm;
using Net.Mqtt.ReactiveOrm.Bus.Interfaces;
using Net.Mqtt.ReactiveOrm.Enums;
using Net.Mqtt.ReactiveOrm.Models;
using Net.Mqtt.ReactiveOrm.CloudEvents;

namespace Demo.Mqtt;

public sealed class MqttContext(
    IMqttBus bus,
    ITopicModel model,
    ICloudEventFactory cloudEventFactory,
    ICloudEventCodec cloudEventCodec) : MqttOrmContext(bus, model, cloudEventFactory, cloudEventCodec)
{
    public TopicSet<DHT230222_Modules> DHT230222_Modules => Set<DHT230222_Modules>();
    public TopicSet<DHT230222_Modules> Z_XTR_Modules => Set<DHT230222_Modules>();
    public TopicSet<double> TemperatureRaw => Set<double>();
    public TopicSet<DHT230222_Modules> AllSensors => Set<DHT230222_Modules>();
    public TopicSet<DHT230222_Modules> AllSensorMessages => Set<DHT230222_Modules>();

    public static TopicModel CreateModel() => new TopicModelBuilder()
        .Add<DHT230222_Modules>(nameof(DHT230222_Modules), "factory_64/sensors/@/events", QoSLevel.ExactlyOnce,
            cloudEvent: Descriptor("com.factory.sensor.reading.v1"))
        .Add<DHT230222_Modules>(nameof(Z_XTR_Modules), "factory_64/module/@/status", QoSLevel.AtLeastOnce,
            cloudEvent: Descriptor("com.factory.module.status.v1"))
        .Add<double>(nameof(TemperatureRaw), "factory_64/sensors/temperature/value",
            cloudEvent: Descriptor("com.factory.sensor.temperature.v1"))
        .Add<DHT230222_Modules>(nameof(AllSensors), "factory_64/+/+/status",
            cloudEvent: Descriptor("com.factory.sensor.status.v1"))
        .Add<DHT230222_Modules>(nameof(AllSensorMessages), "factory_64/sensors/#",
            cloudEvent: Descriptor("com.factory.sensor.message.v1"))
        .Build();

    private static CloudEventDescriptor Descriptor(string type) => new(
        Source: new Uri("urn:factory:equipment-worker"),
        Type: type,
        DataSchema: new Uri("urn:schema:factory:sensor-reading:v1"));
}
