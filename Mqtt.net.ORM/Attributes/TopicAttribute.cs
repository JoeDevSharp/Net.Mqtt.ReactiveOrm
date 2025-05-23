using MQTTnet.Protocol;

namespace Mqtt.net.ORM.Attributes
{
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
    public class TopicAttribute : Attribute
    {
        /// <summary>
        /// The MQTT topic template (e.g., sensors/{deviceId}/temperature)
        /// </summary>
        public string Template { get; }

        /// <summary>
        /// Whether this topic supports MQTT wildcards (+/#) for subscriptions.
        /// </summary>
        public bool AllowWildcards { get; }

        public MqttQualityOfServiceLevel QoS { get; }

        public TopicAttribute(string template, MqttQualityOfServiceLevel QoS = MqttQualityOfServiceLevel.AtMostOnce, bool allowWildcards = false)
        {
            if (string.IsNullOrWhiteSpace(template))
                throw new ArgumentException("Topic template must be a non-empty string.", nameof(template));

            Template = template;
            AllowWildcards = allowWildcards;
        }

        /// <summary>
        /// Replaces placeholders in the topic template with actual values.
        /// </summary>
        /// <param name="parameters">Dictionary of placeholder values</param>
        /// <returns>Resolved topic string</returns>
        public string Resolve<T>(T topicClass)
        {
            if (topicClass == null)
                return Template;

            var name = topicClass.GetType().Name;

            return Template.Replace("@", name);
        }
    }
}
