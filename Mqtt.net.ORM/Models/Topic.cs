namespace Mqtt.net.ORM.Models
{
    public class Topic
    {

        public Topic(string topicName, int qos)
        {
            Name = topicName;
            Qos = qos;
        }
        
        /// <summary>
        /// Nombre del topic.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Nivel de calidad de servicio (QoS) del topic.
        /// </summary>
        public int Qos { get; set; }
    }
}
