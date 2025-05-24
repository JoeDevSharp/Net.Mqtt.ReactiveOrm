## MqttSerializer

`MqttReactiveObjectMapper.MqttSerializer`

Provides a utility for serializing and deserializing MQTT message payloads using JSON. Internally leverages `System.Text.Json` and supports optional configuration via `JsonSerializerOptions`.

---

### Constructor

#### `MqttSerializer(JsonSerializerOptions? options = null)`

Initializes a new instance of the `MqttSerializer` class.

- If `options` is provided, it is used as the serializer configuration.
- If `null`, it defaults to camelCase property naming and compact output.

##### Parameters:

- `options`: _(optional)_ Custom `JsonSerializerOptions`.

---

### Methods

#### `string Serialize<T>(T message)`

Serializes a message of type `T` into a JSON string encoded in UTF-8.

##### Parameters:

- `message`: The object to serialize. Must not be `null`.

##### Returns:

A UTF-8 encoded JSON string.

##### Exceptions:

- `ArgumentNullException`: Thrown if `message` is `null`.

---

#### `T Deserialize<T>(string payload)`

Deserializes a JSON string into an object of type `T`.

##### Parameters:

- `payload`: The JSON string to deserialize. Must not be `null` or whitespace.

##### Returns:

An instance of type `T`.

##### Exceptions:

- `ArgumentException`: Thrown if `payload` is empty or whitespace.
- `InvalidOperationException`: Thrown if deserialization fails or returns `null`.

---

### Example Usage

```csharp
var serializer = new MqttSerializer();

var message = new SensorReading { Temperature = 22.5, Humidity = 60 };
string json = serializer.Serialize(message);

var deserialized = serializer.Deserialize<SensorReading>(json);
```
