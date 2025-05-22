
using Mqtt.net.ORM;
using Mqtt.net.ORM.Attributes;
using MQTTnet.Protocol;

namespace ExampleProject.Topics
{

    [MqttTopic("devices/[Device]/status", MqttQualityOfServiceLevel.ExactlyOnce)]
    public class Sensor_Temp_001 : TopicStatus
    {
        public int Temperature { get; set; } = 0;
        public int Humidity { get; set; } = 0;
    }
}
