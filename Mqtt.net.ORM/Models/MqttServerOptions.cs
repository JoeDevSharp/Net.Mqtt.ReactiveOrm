namespace Mqtt.net.ORM.Models
{
    public class MqttServerOptions
    {
        public string ClientId { get; set; } = $"mqttorm-{Guid.NewGuid()}";
        public string Server { get; set; } = "localhost";
        public int Port { get; set; } = 1883;
        public string? Username { get; set; }
        public string? Password { get; set; }
        public bool UseTls { get; set; } = false;
    }
}
