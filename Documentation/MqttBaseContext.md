# MqttBaseContext Class Documentation

## Definition

`MqttReactiveObjectMapper.MqttBaseContext`

Provides a base class for managing MQTT connections and initializing typed topic sets. It encapsulates MQTT client setup, configuration, and automatic topic discovery using reflection.

---

## Remarks

`MqttBaseContext` is designed to be extended by user-defined contexts that declare public properties of type `TopicSet<T>` decorated with the `TopicAttribute`. Upon instantiation, it automatically wires those properties to the MQTT bus and assigns the appropriate topic metadata.

---

## Constructors

### `MqttBaseContext(string host = "localhost", int port = 1883, string? username = null, string? password = null)`

Initializes the MQTT connection with the specified parameters, builds the internal MQTT client and bus, and invokes automatic initialization of `TopicSet<T>` properties.

#### Parameters:

- `host`: MQTT server address. Default is `"localhost"`.
- `port`: MQTT server port. Default is `1883`.
- `username`: Optional username for authentication.
- `password`: Optional password for authentication.

---

## Properties

### `MqttBus MqttBus`

The internal MQTT bus used to send and receive strongly typed messages. Exposes methods like `ConnectAsync`, `PublishAsync`, and `GetObservable`.

---

## Methods

### `MqttBaseContext WithServer(string host, int port = 1883)`

Sets the MQTT server address and port.

#### Returns:

The current instance of `MqttBaseContext` for chaining.

---

### `MqttBaseContext WithClientId(string clientId)`

Sets the MQTT client identifier.

#### Returns:

The current instance of `MqttBaseContext` for chaining.

---

### `MqttBaseContext WithCredentials(string username, string password)`

Sets the MQTT credentials for broker authentication.

#### Returns:

The current instance of `MqttBaseContext` for chaining.

---

### `MqttBaseContext UseTls(bool enabled = true)`

Enables or disables the use of TLS/SSL for the MQTT connection.

#### Returns:

The current instance of `MqttBaseContext` for chaining.

---

## Protected/Internal Mechanics

### `InitializeTopicSets()`

Uses reflection to find public properties of type `TopicSet<T>` that are decorated with a `TopicAttribute`. It instantiates each topic set and injects the MQTT bus and topic metadata.

- Only initializes properties declared in derived types.
- Throws `InvalidOperationException` if a topic set constructor is not compatible with `(IMqttBus, TopicAttribute)`.

---

## Usage Example

```csharp
public class MyMqttContext : MqttBaseContext
{
    [Topic("devices/{deviceId}/status", QoS = MqttQualityOfServiceLevel.AtLeastOnce)]
    public TopicSet<DeviceStatus> DeviceStatusTopic { get; set; }
}

// Usage
var context = new MyMqttContext("broker.hivemq.com")
    .WithClientId("my-client-id")
    .WithCredentials("user", "pass");

await context.MqttBus.ConnectAsync();

context.DeviceStatusTopic.Publish(new DeviceStatus { ... });
```
