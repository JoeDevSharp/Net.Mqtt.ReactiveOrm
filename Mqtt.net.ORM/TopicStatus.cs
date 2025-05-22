namespace Mqtt.net.ORM
{
    public class TopicStatus
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Status { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
