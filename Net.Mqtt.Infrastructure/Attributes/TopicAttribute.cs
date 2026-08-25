using Net.Mqtt.Infrastructure.Enums;
using MQTTnet.Protocol;

namespace Net.Mqtt.Infrastructure.Attributes
{
    /// <summary>
    /// Defines the legacy MQTT topic mapping associated with a class or property.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
    [Obsolete("Use MqttTopicAttribute with separate PublishTopic and SubscribeFilter.")]
    public class TopicAttribute : Attribute
    {
        /// <summary>
        /// Gets the legacy MQTT topic template.
        /// </summary>
        public string Template { get; }

        /// <summary>
        /// Gets the default quality-of-service level.
        /// </summary>
        public MqttQualityOfServiceLevel QoS { get; }

        /// <summary>
        /// Gets whether publications are retained by default.
        /// </summary>
        public bool Retain { get; }

        /// <summary>
        /// Initializes a legacy topic mapping.
        /// </summary>
        /// <param name="template">The non-empty topic template.</param>
        /// <param name="qos">The default quality-of-service level.</param>
        /// <param name="retain">Whether publications are retained by default.</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="template"/> is empty.</exception>
        public TopicAttribute(
            string template,
            QoSLevel qos = QoSLevel.AtMostOnce,
            bool retain = false)
        {
            if (string.IsNullOrWhiteSpace(template))
                throw new ArgumentException("La plantilla del tópico debe ser una cadena no vacía.", nameof(template));

            Template = template;
            QoS = (MqttQualityOfServiceLevel)qos;
            Retain = retain;
        }

        /// <summary>
        /// Resolves the topic template by replacing markers with values derived from the supplied instance type.
        /// </summary>
        /// <param name="topicClass">The instance from which replacement values are obtained.</param>
        /// <typeparam name="T">The type associated with the topic.</typeparam>
        /// <returns>The resolved topic string.</returns>
        public string Resolve<T>(T topicClass)
        {
            if (topicClass == null)
                return Template;

            var name = topicClass.GetType().Name;

            return Template.Replace("@", name);
        }
    }
}
