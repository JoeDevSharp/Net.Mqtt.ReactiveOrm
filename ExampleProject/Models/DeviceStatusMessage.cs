
using Mqtt.net.ORM;
using Mqtt.net.ORM.Attributes;
using MQTTnet.Protocol;

namespace ExampleProject.Models
{

    [MqttTopic("devices/{DeviceId}/status", MqttQualityOfServiceLevel.ExactlyOnce)]
    public class DeviceStatusMessage : TopicStatus
    {
    }
}
