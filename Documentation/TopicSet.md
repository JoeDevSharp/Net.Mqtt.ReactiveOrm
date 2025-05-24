## TopicSet<T>

`MqttReactiveObjectMapper.TopicSet<T>`

A strongly-typed MQTT topic wrapper that provides reactive access to published messages, as well as publishing and unsubscription capabilities.

This class implements `ITopicSet<T>` and `IObservable<T>` to allow seamless integration with Rx (Reactive Extensions) workflows.

---

### Constructor

#### `TopicSet(IMqttBus mqttBus, TopicAttribute attribute)`

Initializes a new instance of the `TopicSet<T>` class.

##### Parameters:

- `mqttBus`: An implementation of `IMqttBus` used to handle MQTT messaging.
- `attribute`: A `TopicAttribute` containing the topic template and wildcard configuration.

##### Exceptions:

- `ArgumentNullException`: Thrown if `mqttBus` or `attribute` is `null`.

---

### Properties

#### `IMqttBus MqttBus`

Gets the MQTT bus used to interact with the topic.

#### `TopicAttribute Attribute`

Gets the topic attribute that defines the MQTT topic template and options.

#### `string Template`

Returns the MQTT topic template (e.g., `"sensor/{deviceId}/temperature"`).

#### `bool AllowWildcards`

Indicates whether wildcards (`+`, `#`) are permitted in subscriptions.

---

### Methods

#### `IObservable<T> Where(Func<T, bool> predicate)`

Applies a filter to the observable stream.

##### Parameters:

- `predicate`: A function that determines whether a message should be included in the output stream.

##### Returns:

An `IObservable<T>` sequence filtered according to the predicate.

##### Exceptions:

- `ArgumentNullException`: Thrown if `predicate` is `null`.

---

#### `IDisposable Subscribe(IObserver<T> observer)`

Allows direct subscription to the observable topic stream.

This method enables `TopicSet<T>` to behave as an `IObservable<T>` directly.

---

#### `void Publish(T message)`

Publishes a message to the topic.

##### Parameters:

- `message`: The message object to be published.

##### Exceptions:

- `ArgumentNullException`: Thrown if `message` is `null`.

---

#### `void Unsubscribe()`

Unsubscribes from the MQTT topic, stopping further message flow from this topic.

---

#### `IObservable<T> Observable()`

Returns the raw observable stream for the topic, which can be used with LINQ or Rx operators.

---

### Example Usage

```csharp
[Topic("device/{id}/status", AllowWildcards = false)]
public TopicSet<DeviceStatus> Status { get; set; }

...

context.Status
    .Where(status => status.Online)
    .Subscribe(status => Console.WriteLine($"Device online: {status.Id}"));

context.Status.Publish(new DeviceStatus { Id = "dev123", Online = true });
```
