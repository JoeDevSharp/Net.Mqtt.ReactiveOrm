# 📘 `MqttTopicAttribute`

### Namespace: `MqttORM.Attributes`

### Assembly: `MqttORM.dll`

---

## 🧩 Overview

`MqttTopicAttribute` is used to annotate message model classes with an MQTT topic pattern or template. It supports:

- Static topics
- Topics with wildcards (`+`, `#`)
- Parameterized topics with placeholders (e.g. `{deviceId}`)

This enables automatic topic resolution and message routing in the `MqttORM` framework.

---

## 📐 Declaration

```csharp
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class MqttTopicAttribute : Attribute
```

---

## 🔧 Constructor

```csharp
public MqttTopicAttribute(string template, bool allowWildcards = false)
```

### Parameters:

| Name             | Type     | Description                                                                                            |
| ---------------- | -------- | ------------------------------------------------------------------------------------------------------ |
| `template`       | `string` | Topic template string (e.g., `"devices/{deviceId}/status"` or `"sensors/+/temperature"`). Required.    |
| `allowWildcards` | `bool`   | Indicates whether MQTT wildcards (`+`, `#`) are permitted in the topic. Optional. Defaults to `false`. |

---

## 📦 Properties

| Name             | Type     | Description                                             |
| ---------------- | -------- | ------------------------------------------------------- |
| `Template`       | `string` | The topic pattern or template defined by the developer. |
| `AllowWildcards` | `bool`   | Whether wildcards are allowed in this topic.            |

---

## 🔄 Methods

### `Resolve(IDictionary<string, string> parameters)`

Replaces placeholders in the topic template with concrete values.

```csharp
public string Resolve(IDictionary<string, string> parameters)
```

**Example:**

```csharp
var topic = attribute.Resolve(new Dictionary<string, string>
{
    ["deviceId"] = "alpha-01"
});
// Result: "devices/alpha-01/status"
```

---

### `GetTemplateParameters()`

Extracts all placeholder names (e.g., `{deviceId}`) from the topic template.

```csharp
public IEnumerable<string> GetTemplateParameters()
```

**Example:**

```csharp
// For topic: "devices/{deviceId}/status"
var parameters = attribute.GetTemplateParameters();
// Returns: ["deviceId"]
```

---

## ✅ Usage Examples

### 1. **Static Topic**

```csharp
[MqttTopic("sensors/temperature")]
public class TemperatureMessage
{
    public string DeviceId { get; set; }
    public double Value { get; set; }
}
```

### 2. **Parameterized Topic**

```csharp
[MqttTopic("devices/{deviceId}/status")]
public class StatusMessage
{
    public string Status { get; set; }
}
```

### 3. **Topic with MQTT Wildcards**

```csharp
[MqttTopic("sensors/+/humidity", allowWildcards: true)]
public class HumidityMessage
{
    public double Value { get; set; }
}
```

---

## ⚠️ Developer Notes

- Placeholders must be enclosed in curly braces (`{}`), e.g. `{room}`, `{sensorId}`.
- `Resolve()` does **not** validate whether all placeholders are provided.
- You can combine wildcards and placeholders if logic allows it, but it's up to the subscriber to ensure consistent usage.

---

## 📎 Common Use Cases

| Use Case                        | Solution                                   |
| ------------------------------- | ------------------------------------------ |
| Device-specific message routing | Use parameterized topics with `{deviceId}` |
| Broad subscriptions             | Use wildcards in templates (`+`, `#`)      |
| Dynamic publishing              | Call `Resolve()` with runtime parameters   |
