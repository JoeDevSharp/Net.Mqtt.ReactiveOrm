using ExampleProject.Models;
using Mqtt.net.ORM;

namespace ExampleProject
{
    public class MqttContext : Mqtt.net.ORM.MqttContext
    {
        public TopicSet<DeviceStatusMessage> DeviceStatusMessage { get; set; }

        public MqttContext() : base("localhost") { }
    }
}
