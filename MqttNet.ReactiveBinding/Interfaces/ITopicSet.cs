using JoeDevSharp.MqttNet.ReactiveBinding.Attributes;
using JoeDevSharp.MqttNet.ReactiveBinding.Bus.Interfaces;
using JoeDevSharp.MqttNet.ReactiveBinding.Enums;

namespace JoeDevSharp.MqttNet.ReactiveBinding.Interfaces
{
    /// <summary>
    /// Representa un conjunto de tópicos MQTT fuertemente tipados, con capacidades de observación, publicación y cancelación de suscripción.
    /// </summary>
    public interface ITopicSet<T> : IObservable<T>
    {
        /// <summary>
        /// Instancia del bus MQTT utilizada para las operaciones.
        /// </summary>
        IMqttBus MqttBus { get; }

        /// <summary>
        /// Atributo del tópico que define plantilla y configuración.
        /// </summary>
        TopicAttribute Attribute { get; }

        /// <summary>
        /// Plantilla del tópico MQTT (por ejemplo, "sensor/{deviceId}/temperature").
        /// </summary>
        string Template { get; }

        /// <summary>
        /// Publica un mensaje en el tópico MQTT.
        /// </summary>
        void Publish(T message);

        /// <summary>
        /// Publica un mensaje en el tópico MQTT, reescribe Qos y Retail.
        /// </summary>
        void Publish(T message, QoSLevel qos, bool retain);

        /// <summary>
        /// Cancela la suscripción del tipo de mensaje asociado.
        /// </summary>
        void Unsubscribe();
    }
}
