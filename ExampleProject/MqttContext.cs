using ExampleProject.Topics;
using Mqtt.net.ORM;

namespace ExampleProject
{
    public class MqttContext : Mqtt.net.ORM.MqttContext
    {
        public TopicSet<Sensor_Temp_001> Sensor_Temp_001 { get; set; } 

        public MqttContext() : base("localhost") { }
    }
}
