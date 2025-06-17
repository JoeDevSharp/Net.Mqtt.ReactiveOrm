using JoeDevSharp.MqttNet.ReactiveBinding.Attributes;
using MQTTnet.Protocol;

namespace JoeDevSharp.MqttNet.ReactiveBinding.Bus.Interfaces
{
    /// <summary>
    /// Define un bus de mensajes MQTT fuertemente tipado.
    /// Permite la publicación, suscripción y observación de mensajes 
    /// basados en tipos de datos decorados con atributos [Topic].
    /// </summary>
    public interface IMqttBus
    {
        /// <summary>
        /// Asegura que el cliente MQTT subyacente esté conectado.
        /// Si ya está conectado, no realiza ninguna acción.
        /// </summary>
        Task ConnectAsync();

        /// <summary>
        /// Obtiene un observable para un tipo de mensaje específico.
        /// Este flujo permite la suscripción reactiva a mensajes entrantes.
        /// </summary>
        /// <typeparam name="T">Tipo del mensaje esperado.</typeparam>
        /// <param name="attribute">Atributo que define el topic al que se debe suscribir.</param>
        /// <returns>Una secuencia observable de mensajes del tipo <typeparamref name="T"/>.</returns>
        IObservable<T> GetObservable<T>(TopicAttribute attribute);

        /// <summary>
        /// Publica una instancia de mensaje en su topic MQTT correspondiente.
        /// </summary>
        /// <typeparam name="T">Tipo del mensaje decorado con [Topic].</typeparam>
        /// <param name="message">Objeto del mensaje a serializar y publicar.</param>
        /// <param name="attribute">
        /// Atributo que define el template del topic.
        /// </param>
        Task PublishAsync<T>(object message, TopicAttribute attribute);

        /// <summary>
        /// Publica una instancia de mensaje en su topic MQTT correspondiente con overider QoS and Retain.
        /// </summary>
        /// <typeparam name="T">Tipo del mensaje decorado con [Topic].</typeparam>
        /// <param name="message">Objeto del mensaje a serializar y publicar.</param>
        /// <param name="attribute">
        /// Atributo que define el template del topic.
        /// </param>
        Task PublishAsync<T>(object message, TopicAttribute attribute, MqttQualityOfServiceLevel qos, bool retain);

        /// <summary>
        /// Cancela la suscripción del manejador para el tipo de mensaje <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">Tipo del mensaje decorado con [Topic].</typeparam>
        /// <param name="attribute">Atributo que contiene el template del topic.</param>
        Task UnsubscribeAsync<T>(TopicAttribute attribute);
    }
}
