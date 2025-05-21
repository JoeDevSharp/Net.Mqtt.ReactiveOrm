namespace MqttORM.Models
{
    public class TopicValues : Dictionary<string, object>
    {
        public TopicValues Convert(Dictionary<string, object> dictionary)
        {
            foreach (var kvp in dictionary)
            {
                this[kvp.Key] = kvp.Value;
            }
            return this;
        }

        public TopicValues Convert(Dictionary<string, string> dictionary)
        {
            foreach (var kvp in dictionary)
            {
                this[kvp.Key] = kvp.Value;
            }
            return this;
        }
    }
}
