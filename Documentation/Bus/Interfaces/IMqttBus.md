# IMqttBus Interface Documentation

## Definition

`MqttReactiveObjectMapper.Bus.Interfaces.IMqttBus`

Defines a strongly-typed MQTT message bus. This interface enables publishing, subscribing, and observing messages based on data types decorated with `[Topic]` attributes.

---

## Remarks

The `IMqttBus` interface abstracts the interaction with an MQTT client by providing a type-safe contract for working with messages. It allows developers to publish messages and subscribe to observables using topic metadata defined through `TopicAttribute`.

Implementations of this interface are expected to handle topic resolution, QoS enforcement, and connection management internally.

---

## Constructors

_Not applicable (interface)._

---

## Properties

_None_

---

## Methods

### `Task ConnectAsync()`

Ensures the underlying MQTT client is connected. If already connected, this method performs no action.

**Returns:**

- `Task`: A task that completes when the connection is established.

---

### `IObservable<T> GetObservable<T>(TopicAttribute attribute)`

Retrieves an observable stream for a specific message type, allowing reactive subscriptions to incoming MQTT messages.

**Type Parameters:**

- `T`: The expected message type.

**Parameters:**

- `attribute` (`TopicAttribute`): Defines the topic to subscribe to.

**Returns:**

- `IObservable<T>`: A reactive stream of messages of type `T`.

---

### `Task PublishAsync<T>(object message, TopicAttribute attribute)`

Publishes a message instance to its corresponding MQTT topic.

**Type Parameters:**

- `T`: The type of the message, expected to be decorated with a `[Topic]` attribute.

**Parameters:**

- `message` (`object`): The message object to serialize and publish.
- `attribute` (`TopicAttribute`): Defines the topic template for the message.

**Returns:**

- `Task`: A task that completes when the message is published.

---

### `Task UnsubscribeAsync<T>(TopicAttribute attribute)`

Unsubscribes the handler associated with messages of the specified type.

**Type Parameters:**

- `T`: The message type decorated with `[Topic]`.

**Parameters:**

- `attribute` (`TopicAttribute`): Contains the topic template used for the subscription.

**Returns:**

- `Task`: A task that completes when the unsubscription is finalized.

---

## Operators

_None_

---

## Explicit Interface Implementations

_None_

---

## Extension Methods

_None_

---
