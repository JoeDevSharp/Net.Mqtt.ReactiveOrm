# ITopicSet&lt;T&gt; Interface Documentation

## Definition

`MqttReactiveObjectMapper.Interfaces.ITopicSet<T>`

Represents a strongly-typed set of MQTT topics with capabilities for observation, publishing, and unsubscription.

---

## Remarks

The `ITopicSet<T>` interface provides a high-level abstraction over MQTT topic handling. It integrates publishing, subscribing (as an `IObservable<T>`), and unsubscribing logic into a single structure.

This abstraction allows consumers to:

- Reactively subscribe to topic messages of type `T`.
- Publish structured messages without manually formatting MQTT topics.
- Unsubscribe from topics when no longer needed.

This pattern supports separation of concerns and simplifies working with MQTT in a reactive .NET environment.

---

## Constructors

_Not applicable (interface)._

---

## Properties

### `IMqttBus MqttBus`

The MQTT bus instance used for all operations.

**Type:** `IMqttBus`

---

### `TopicAttribute Attribute`

The topic attribute containing the topic template and QoS configuration.

**Type:** `TopicAttribute`

---

### `string Template`

The MQTT topic template (e.g., `"sensor/{deviceId}/temperature"`).

**Type:** `string`

---

### `bool AllowWildcards`

Indicates whether MQTT wildcards (`+`, `#`) are allowed in the subscription.

**Type:** `bool`

---

## Methods

### `IObservable<T> Observable()`

Gets the full observable stream associated with this topic.

**Returns:** `IObservable<T>`

---

### `void Publish(T message)`

Publishes a message to the configured MQTT topic.

**Parameters:**

- `message` (`T`): The message to be serialized and published.

---

### `void Unsubscribe()`

Cancels the subscription associated with the topic and message type.

---

## Operators

_None_

---

## Explicit Interface Implementations

This interface extends:

- `IObservable<T>` — allowing direct reactive subscriptions using `.Subscribe(...)`.

---

## Extension Methods

\_None defined explicitly. Standard LINQ and Rx.NET operators (e.g., `.Select`, `.Where`, etc.) are applicable through `IObservable<T>`.

---
