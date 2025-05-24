# MqttBus Class Documentation

## Definition

`MqttReactiveObjectMapper.Bus.MqttBus`

Default implementation of `IMqttBus` using [MQTTnet](https://github.com/dotnet/MQTTnet). Manages reactive subscriptions and strongly-typed message publishing over MQTT.

---

## Remarks

`MqttBus` enables seamless integration of MQTT messaging into reactive applications. It supports:

- Type-safe subscriptions using `[Topic]` attributes.
- Dynamic topic resolution and serialization via `MqttSerializer`.
- Reactive message streams through `IObservable<T>`.
- MQTT wildcard matching support.

It ensures that each message type is subscribed only once and caches subscription and subject information for performance.

---

## Constructors

### `MqttBus(IMqttClient client, MqttClientOptions options, MqttSerializer serializer)`

Initializes a new instance of `MqttBus`.

**Parameters:**

- `client` (`IMqttClient`): The MQTT client instance.
- `options` (`MqttClientOptions`): Connection options for the MQTT client.
- `serializer` (`MqttSerializer`): Serializer used to transform messages to/from MQTT payloads.

---

## Properties

_None_

---

## Methods

### `Task ConnectAsync()`

Ensures the MQTT client is connected. Does nothing if already connected.

**Returns:**

- `Task`: Completes when the client is connected.

---

### `IObservable<T> GetObservable<T>(TopicAttribute attribute)`

Returns a reactive stream of messages of type `T`.

**Type Parameters:**

- `T`: The message type.

**Parameters:**

- `attribute` (`TopicAttribute`): Defines the MQTT topic to subscribe to.

**Returns:**

- `IObservable<T>`: An observable stream of messages of type `T`.

---

### `Task PublishAsync<T>(object message, TopicAttribute attribute)`

Publishes a message to the topic defined by the given `TopicAttribute`.

**Type Parameters:**

- `T`: The type of the message.

**Parameters:**

- `message` (`object`): The message to serialize and publish.
- `attribute` (`TopicAttribute`): Defines the topic template and QoS level.

**Returns:**

- `Task`: Completes when the message is published.

---

### `Task UnsubscribeAsync<T>(TopicAttribute attribute)`

Unsubscribes the topic associated with the provided attribute.

**Type Parameters:**

- `T`: The message type.

**Parameters:**

- `attribute` (`TopicAttribute`): Defines the topic to unsubscribe.

**Returns:**

- `Task`: Completes when the unsubscription is finalized.

---

## Operators

_None_

---

## Explicit Interface Implementations

_All interface methods are implemented directly._

---

## Extension Methods

_None_

---

## Private Methods

### `Task EnsureSubscribedAsync<T>()`

Ensures that message type `T` is only subscribed once and registers its handler.

---

### `Task OnApplicationMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs e)`

Handles incoming MQTT messages and dispatches them to registered handlers.

---

### `Task DispatchAsync<T>(string raw, Func<T, Task> handler)`

Deserializes a message and invokes the provided handler.

---

### `bool MatchTopic(string pattern, string topic)`

Performs MQTT wildcard topic matching.

- `+` matches a single level
- `#` matches multiple levels

**Returns:** `true` if `topic` matches `pattern`.

---

Let me know if you'd like to generate a unified documentation file for the entire library or add more classes/interfaces to this set.
