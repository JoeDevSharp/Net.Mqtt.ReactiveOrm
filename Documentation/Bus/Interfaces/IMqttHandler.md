# IMqttHandler&lt;T&gt; Interface Documentation

## Definition

`MqttReactiveObjectMapper.Bus.Interfaces.IMqttHandler<T>`

Defines a handler for incoming MQTT messages of type `T`.

---

## Remarks

The `IMqttHandler<T>` interface provides a contract for processing MQTT messages that have been deserialized into strongly-typed objects. It is typically used to encapsulate business logic that should be executed upon receiving a message from a specific MQTT topic.

This interface is useful for separating message processing responsibilities in reactive or event-driven applications.

---

## Constructors

_Not applicable (interface)._

---

## Properties

_None_

---

## Methods

### `Task HandleAsync(T message)`

Handles an incoming MQTT message.

**Parameters:**

- `message` (`T`): The deserialized message object received from the MQTT payload.

**Returns:**

- `Task`: A task representing the asynchronous handling operation.

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
