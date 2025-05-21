namespace Mqtt.net.ORM
{
    public class TopicStatus
    {
        public string DeviceId { get; set; } = new Guid().ToString();
        public string Status { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
