﻿using Net.Mqtt.ReactiveOrm.Enums;
using MQTTnet.Protocol;

namespace Net.Mqtt.ReactiveOrm.Attributes
{
    /// <summary>
    /// Atributo que define un tópico MQTT asociado a una clase o propiedad. 
    /// Permite especificar la plantilla del tópico, el nivel de calidad del servicio (QoS),
    /// la retención y si se permiten comodines para suscripciones.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
    public class TopicAttribute : Attribute
    {
        /// <summary>
        /// Plantilla del tópico MQTT (por ejemplo: sensors/{deviceId}/temperature).
        /// Puede incluir comodines o marcadores que serán reemplazados dinámicamente.
        /// </summary>
        public string Template { get; }

        /// <summary>
        /// Nivel de calidad del servicio (QoS) por defecto para este tópico.
        /// </summary>
        public MqttQualityOfServiceLevel QoS { get; }

        /// <summary>
        /// Indica si los mensajes deben marcarse como retenidos por defecto.
        /// </summary>
        public bool Retain { get; }

        /// <summary>
        /// Crea una nueva instancia del atributo <see cref="TopicAttribute"/>.
        /// </summary>
        /// <param name="template">Plantilla del tópico MQTT. Debe ser una cadena no vacía.</param>
        /// <param name="QoS">Nivel de calidad del servicio (por defecto: AtMostOnce).</param>
        /// <param name="retain">Indica si los mensajes deben ser retenidos por defecto (por defecto: false).</param>
        /// <param name="allowWildcards">Indica si se permiten comodines MQTT (por defecto: false).</param>
        /// <exception cref="ArgumentException">Se lanza si la plantilla del tópico es nula o vacía.</exception>
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
        /// Resuelve la plantilla del tópico reemplazando los marcadores con los valores
        /// derivados del tipo de la instancia proporcionada.
        /// </summary>
        /// <param name="topicClass">Instancia de clase para extraer valores de reemplazo.</param>
        /// <typeparam name="T">Tipo de la clase relacionada con el tópico.</typeparam>
        /// <returns>Cadena de tópico resuelta.</returns>
        public string Resolve<T>(T topicClass)
        {
            if (topicClass == null)
                return Template;

            var name = topicClass.GetType().Name;

            return Template.Replace("@", name);
        }
    }
}
