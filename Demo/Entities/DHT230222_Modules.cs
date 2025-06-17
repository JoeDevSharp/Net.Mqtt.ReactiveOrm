using JoeDevSharp.MqttNet.ReactiveBinding.Attributes;

namespace Demo.Entities
{
    public class DHT230222_Modules
    {
        public double Temperature { get; set; }
        public double Humidity { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
