using Mqtt.net.ORM.Attributes;
using Mqtt.net.ORM.Bus.Interfaces;

namespace Mqtt.net.ORM.Interfaces
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
        /// Indica si se permiten comodines (+/#) en la suscripción.
        /// </summary>
        bool AllowWildcards { get; }

        /// <summary>
        /// Devuelve el observable completo asociado al tópico.
        /// </summary>
        IObservable<T> Observable();

        /// <summary>
        /// Publica un mensaje en el tópico MQTT.
        /// </summary>
        void Publish(T message);

        /// <summary>
        /// Cancela la suscripción del tipo de mensaje asociado.
        /// </summary>
        void Unsubscribe();
    }
}
