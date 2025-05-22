namespace Mqtt.net.ORM
{
    public class TopicStatus
    {
        public Guid DeviceId { get; set; } = Guid.NewGuid();
        public string Status { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
