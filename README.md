## 🧠 Framework – Net.Mqtt.ReactiveOrm

**Net.Mqtt.ReactiveOrm** is a lightweight **Reactive Object-Relational Mapper (Reactive ORM)** for MQTT-based .NET applications. It transforms raw MQTT topics into **strongly typed, observable entities**, enabling developers to manipulate real-time data streams with familiar patterns such as LINQ, context objects, and declarative subscriptions.

Inspired by the architecture of **Entity Framework** (`DbContext`, `DbSet<T>`), this library introduces a structured, composable, and reactive layer over MQTT – turning your messaging layer into a type-safe, reactive data bus.

---

### 🔍 Key Features

* **Entity Mapping via Attributes**: Bind MQTT topics directly to C# classes using `[Topic]` attributes.
* **Reactive Subscriptions**: React to messages using `IObservable<T>`, `.Where(...)`, and `.Subscribe(...)`.
* **Declarative Publishing**: Publish messages via `.Publish(entity)` instead of low-level topic handling.
* **Context-based API**: Create an `MqttOrmContext` that acts like an EF `DbContext` for MQTT.
* **LINQ-style Filtering**: Use `.Where()` and `.Select()` to declaratively transform and consume streams.
* **Pluggable MQTT Integration**: Works over any implementation of `IMqttBus` (Mosquitto, HiveMQ, EMQX, etc.).

---

### 🔧 Use Case Scenarios

* **IoT Gateways**: Represent sensors and device data as live C# objects.
* **Industrial Automation**: Monitor and control systems via strongly typed events.
* **Edge Processing**: Perform reactive computations without glue code.
* **Home Automation**: Handle device state changes cleanly and reactively.

---

### 🧱 Architecture Overview

| Component        | Description                                                                 |
| ---------------- | --------------------------------------------------------------------------- |
| `TopicSet<T>`    | Equivalent to `DbSet<T>` – manages publish/subscribe logic for a given type |
| `MqttOrmContext` | Registers all topic-mapped entities and exposes their `TopicSet<T>`s        |
| `IMqttBus`       | Abstraction layer over the MQTT client                                      |
| `TopicAttribute` | Declares the topic-to-entity mapping using attributes                       |

---

### 🤝 Philosophy

Rather than treating MQTT as a loose transport protocol with JSON blobs and topic strings, **Net.Mqtt.ReactiveOrm** embraces **type safety**, **reactivity**, and **declarative design**.

It enables you to **model your message flows as first-class domain entities**, subscribe with expressive LINQ queries, and **remove boilerplate MQTT plumbing** entirely.

If Entity Framework brought structure to databases, **Net.Mqtt.ReactiveOrm does the same for MQTT**.

---

### 📘 Developer Documentation – Net.Mqtt.ReactiveOrm

---

#### ✅ 1. Introduction

`Net.Mqtt.ReactiveOrm` offers a clean, modern alternative to traditional MQTT client libraries by treating messages as **live, observable entities**. It allows your application to **react, publish, and reason** about real-time events with minimal boilerplate.

---

#### 🚀 2. Installation & Setup

```bash
dotnet add package Net.Mqtt.ReactiveOrm
```

Or reference the source project directly for development or contribution.

---

#### 🧩 3. Defining MQTT Entities

```csharp
using Net.Mqtt.ReactiveOrm.Attributes;

public class DHT230222_Modules
{
    public double Temperature { get; set; }
    public double Humidity { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
```

---

#### 🏗️ 4. Creating the MQTT Context

```csharp
public class MqttContext : MqttOrmContext
{
    [Topic("iot/devices/dht/modules/DHT230222_Modules/status")]
    public TopicSet<DHT230222_Modules> DHT230222_Modules { get; }

    [Topic("iot/devices/dht/sensors/@/status")]
    public TopicSet<DHT230222_Modules> EX4008_Sensor { get; }

    public MqttContext()
    {
        DHT230222_Modules = Set<DHT230222_Modules>();
        EX4008_Sensor = Set<DHT230222_Modules>("EX4008"); // Named parameter replacement
    }
}
```

---

#### 👂 5. Subscribing to Messages

```csharp
_context.DHT230222_Modules
    .Where(x => x.Temperature > 25)
    .Subscribe(x =>
    {
        Console.WriteLine($"Warning: High temp = {x.Temperature}");
    });
```

---

#### 📤 6. Publishing Messages

```csharp
await _context.DHT230222_Modules.Publish(new DHT230222_Modules
{
    Temperature = 21.4,
    Humidity = 44.5
});
```

---

#### 🧪 7. Full Console Example

```csharp
static void Main(string[] args)
{
    var context = new MqttContext();

    context.DHT230222_Modules.Subscribe(m =>
    {
        Console.WriteLine($"Temp = {m.Temperature}, Humidity = {m.Humidity}");
    });

    context.DHT230222_Modules.Publish(new DHT230222_Modules
    {
        Temperature = 18.0,
        Humidity = 45.0
    });

    Console.ReadLine();
}
```

---

#### 🛠️ 8. Best Practices

* Always wait for connection establishment before publishing.
* Filter early using `.Where()` to reduce unnecessary processing.
* Avoid blocking inside `.Subscribe()`; use async patterns if needed.
* Use wildcard substitution (`@`) to support dynamic topic segments.

---

#### ❓ 9. FAQ

> **Q: Can I use MQTT wildcards like `+` and `#`?**
> Yes, enable `AllowWildcards = true` in the `[Topic]` attribute.

> **Q: What is `Set<T>()`?**
> Equivalent to `DbContext.Set<T>()`, it returns a `TopicSet<T>` mapped to a topic.

> **Q: Is it compatible with any MQTT broker?**
> Yes. As long as the broker supports MQTT 3.1.1 or 5.0.
