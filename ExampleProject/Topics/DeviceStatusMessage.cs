
using Mqtt.net.ORM;
using Mqtt.net.ORM.Attributes;
using MQTTnet.Protocol;

namespace ExampleProject.Topics
{

    [MqttTopic("devices/{DeviceId}/status", MqttQualityOfServiceLevel.ExactlyOnce)]
    public class DeviceStatusMessage : TopicStatus
    {
        public string Name { get; set; } = string.Empty;
        public int Temperature { get; set; } = 0;
        public int Humidity { get; set; } = 0;
    }
}
