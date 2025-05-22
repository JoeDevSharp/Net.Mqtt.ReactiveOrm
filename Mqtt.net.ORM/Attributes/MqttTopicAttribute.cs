using MQTTnet.Protocol;
using System.Text.RegularExpressions;

namespace Mqtt.net.ORM.Attributes
{
    /// <summary>
    /// Maps a class to an MQTT topic. Supports wildcards (+/#) and template parameters (e.g. sensors/{deviceId}/temperature).
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class MqttTopicAttribute : Attribute
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

        /// <summary>
        /// Creates a new MqttTopicAttribute with a topic template.
        /// </summary>
        /// <param name="template">Topic template (e.g., sensors/{deviceId}/temperature)</param>
        /// <param name="allowWildcards">If true, allows MQTT wildcards in subscriptions</param>
        public MqttTopicAttribute(string template, MqttQualityOfServiceLevel QoS = MqttQualityOfServiceLevel.AtMostOnce, bool allowWildcards = false)
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
            
            return Template.Replace("[Device]", name);
        }

    }
}
