using Demo.Entities;
using Codevia.MqttReactiveObjectMapper;
using Codevia.MqttReactiveObjectMapper.Attributes;

namespace Demo.Mqtt
{
    public class MqttContext : MqttBaseContext
    {

        [Topic("factory_64/sensors/@/status")]
        public TopicSet<DHT230222_Modules> DHT230222_Modules { get; set; }

        public MqttContext() : base("localhost", 1883) { }
    }
}
