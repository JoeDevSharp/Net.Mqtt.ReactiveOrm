using Demo.Entities;
using Net.Mqtt.Infrastructure;
using Net.Mqtt.Infrastructure.Enums;
using Net.Mqtt.Infrastructure.Attributes;
using Net.Mqtt.Infrastructure.RequestReply;

namespace Demo.Mqtt;

/// <summary>Declares the typed MQTT topic sets used by the demo application.</summary>
/// <param name="dependencies">The services required by the MQTT ORM context.</param>
public sealed class MqttContext(MqttContextDependencies dependencies) : MqttOrmContext(dependencies)
{
    /// <summary>Gets environmental inputs consumed by business Worker BS1.</summary>
    [MqttTopic(
        PublishTopic = "business/bs1/inputs/environment/v1",
        SubscribeFilter = "business/bs1/inputs/environment/v1",
        QoS = MqttQoS.ExactlyOnce,
        Retain = false)]
    public TopicSet<Sensor1Telemetry> Sensor1Telemetry => Set<Sensor1Telemetry>();

    /// <summary>Gets binary video chunks consumed by business Worker BS2.</summary>
    [MqttTopic(
        PublishTopic = "business/bs2/inputs/video/v1",
        SubscribeFilter = "business/bs2/inputs/video/v1",
        QoS = MqttQoS.AtLeastOnce,
        Retain = false)]
    public TopicSet<Sensor2VideoChunk> Sensor2VideoChunks => Set<Sensor2VideoChunk>();

    /// <summary>Gets operational assessments produced by business Worker BS1.</summary>
    [MqttTopic(PublishTopic = "business/bs1/results/v1", SubscribeFilter = "business/bs1/results/v1", QoS = MqttQoS.AtLeastOnce)]
    public TopicSet<Bs1OperationalAssessment> Bs1Assessments => Set<Bs1OperationalAssessment>();

    /// <summary>Gets completed video results produced by business Worker BS2.</summary>
    [MqttTopic(PublishTopic = "business/bs2/results/v1", SubscribeFilter = "business/bs2/results/v1", QoS = MqttQoS.AtLeastOnce)]
    public TopicSet<Bs2VideoResult> Bs2VideoResults => Set<Bs2VideoResult>();

    /// <summary>Gets request messages for the request/reply demonstration.</summary>
    [MqttTopic(PublishTopic = "request-reply/simple/request", SubscribeFilter = "request-reply/simple/request", QoS = MqttQoS.AtLeastOnce)]
    public TopicSet<SimpleMessage> SimpleMessages => Set<SimpleMessage>();

    /// <summary>Gets correlated responses for the request/reply demonstration.</summary>
    [MqttTopic(PublishTopic = "request-reply/simple/response", SubscribeFilter = "request-reply/simple/response", QoS = MqttQoS.AtLeastOnce)]
    public TopicSet<SimpleMessageResponse> SimpleMessageResponses => Set<SimpleMessageResponse>();

    /// <summary>Gets the shared correlated request/reply dispatcher.</summary>
    public MqttRequestSet<SimpleMessage, SimpleMessageResponse> SimpleMessageRequests =>
        Request<SimpleMessage, SimpleMessageResponse>(nameof(SimpleMessages), nameof(SimpleMessageResponses));
}
