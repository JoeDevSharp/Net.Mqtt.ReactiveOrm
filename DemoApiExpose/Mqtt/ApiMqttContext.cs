using DemoApiExpose.EventEntities;
using Net.Mqtt.Infrastructure;
using Net.Mqtt.Infrastructure.Attributes;
using Net.Mqtt.Infrastructure.Enums;
using Net.Mqtt.Infrastructure.RequestReply;

namespace DemoApiExpose.Mqtt;

public sealed class ApiMqttContext(MqttContextDependencies dependencies) : MqttOrmContext(dependencies)
{
    [MqttTopic(PublishTopic = "../../capabilities/controller/messages/request", SubscribeFilter = "../../capabilities/controller/messages/request", QoS = MqttQoS.AtLeastOnce)]
    public TopicSet<ControllerMessageRequest> ControllerRequests => Set<ControllerMessageRequest>();

    [MqttTopic(PublishTopic = "../../capabilities/controller/messages/response", SubscribeFilter = "../../capabilities/controller/messages/response", QoS = MqttQoS.AtLeastOnce)]
    public TopicSet<ControllerMessageResponse> ControllerResponses => Set<ControllerMessageResponse>();

    [MqttTopic(PublishTopic = "minimal/messages/request", SubscribeFilter = "minimal/messages/request", QoS = MqttQoS.AtLeastOnce)]
    public TopicSet<MinimalMessageRequest> MinimalRequests => Set<MinimalMessageRequest>();

    [MqttTopic(PublishTopic = "minimal/messages/response", SubscribeFilter = "minimal/messages/response", QoS = MqttQoS.AtLeastOnce)]
    public TopicSet<MinimalMessageResponse> MinimalResponses => Set<MinimalMessageResponse>();

    public MqttRequestSet<ControllerMessageRequest, ControllerMessageResponse> ControllerRequestReply =>
        Request<ControllerMessageRequest, ControllerMessageResponse>(nameof(ControllerRequests), nameof(ControllerResponses));

    public MqttRequestSet<MinimalMessageRequest, MinimalMessageResponse> MinimalRequestReply =>
        Request<MinimalMessageRequest, MinimalMessageResponse>(nameof(MinimalRequests), nameof(MinimalResponses));
}
