using Demo.Entities;
using Net.Mqtt.ReactiveOrm;
using Net.Mqtt.ReactiveOrm.Bus.Interfaces;
using Net.Mqtt.ReactiveOrm.Enums;
using Net.Mqtt.ReactiveOrm.Models;
using Net.Mqtt.ReactiveOrm.CloudEvents;
using Net.Mqtt.ReactiveOrm.Contracts;

namespace Demo.Mqtt;

public sealed class MqttContext(
    IMqttBus bus,
    ITopicModel model,
    ICloudEventFactory cloudEventFactory,
    ICloudEventCodec cloudEventCodec,
    IEventContractRegistry contractRegistry,
    IEventDataValidator dataValidator)
    : MqttOrmContext(bus, model, cloudEventFactory, cloudEventCodec, contractRegistry, dataValidator)
{
    public TopicSet<DHT230222_Modules> DHT230222_Modules => Set<DHT230222_Modules>();

    public static TopicModel CreateModel() => new TopicModelBuilder()
        .Add<DHT230222_Modules>(nameof(DHT230222_Modules), "factory_64/sensors/@/events", QoSLevel.ExactlyOnce,
            cloudEvent: Descriptor("com.factory.sensor.reading.v1"))
        .Build();

    private static CloudEventDescriptor Descriptor(string type) => new(
        Source: new Uri("urn:factory:equipment-worker"),
        Type: type,
        DataSchema: new Uri("urn:schema:factory:sensor-reading:v1"));
}
