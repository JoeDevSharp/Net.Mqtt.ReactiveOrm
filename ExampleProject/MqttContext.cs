using ExampleProject.Topics;
using Mqtt.net.ORM;
using Mqtt.net.ORM.Attributes;
using MQTTnet.Protocol;

namespace ExampleProject
{
    public class MqttContext : MqttBaseContext
    {
        //public TopicSet<Sensor_Temp_001> Sensor_Temp_001 { get; set; }

        [Topic("Sensor/Temp/@/status")]
        public TopicSet<double> Sensor_hex_001 { get; set; }

        [Topic("devices/@/status", MqttQualityOfServiceLevel.ExactlyOnce)]
        public TopicSet<Sensor_Temp_001> Sensor_Temp_001 { get; set; }

        public MqttContext() : base("localhost", 1883)
        {
        }
    }
}
