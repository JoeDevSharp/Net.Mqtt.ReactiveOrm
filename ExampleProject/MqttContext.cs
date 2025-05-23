using ExampleProject.Topics;
using Mqtt.net.ORM;

namespace ExampleProject
{
    public class MqttContext : MqttBaseContext
    {
        public TopicSet<Sensor_Temp_001> Sensor_Temp_001 { get; set; } 
        public TopicSet<Sensor_hex_0001> Sensor_hex_001 { get; set; }

        public MqttContext() : base() { }
    }
}
