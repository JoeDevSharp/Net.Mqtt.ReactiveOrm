## 🧠 Framework – JoeDevSharp.MqttNet.ReactiveBinding

**JoeDevSharp.MqttNet.ReactiveBinding** is a lightweight Reactive Object Mapper (ROM) for MQTT-based applications in .NET. It abstracts MQTT topics as strongly typed, observable entities, enabling developers to handle real-time data streams using LINQ-style syntax and reactive programming patterns.

Inspired by Entity Framework’s `DbContext` and `DbSet<T>` model, `JoeDevSharp.MqttNet.ReactiveBinding` brings structure and clarity to messaging-driven systems by treating MQTT topics as first-class, observable data sources. This makes it easier to reason about, subscribe to, filter, and publish MQTT messages without dealing directly with low-level client code.

---

### 🔍 Key Features

- **Entity Mapping via Attributes**: Define MQTT topics using `[Topic]` attributes on classes, making the topic-to-entity relationship explicit.
- **Reactive Subscriptions**: Use `IObservable<T>` and `Where(...).Subscribe(...)` for reactive, filtered event handling.
- **Declarative Publishing**: Publish entities directly using the `.Publish(entity)` method, hiding low-level MQTT details.
- **Plug-and-Play Context**: Create a custom `MqttContext` to manage all MQTT entities just like a database context.
- **LINQ-style filtering**: Chain `.Where()` and `.Select()` on your MQTT streams.
- **Transparent MQTT integration**: Built on top of a pluggable `IMqttBus`, it can be adapted to different MQTT libraries and broker implementations.

---

### 🔧 Use Case Scenarios

- **IoT Gateways**: Map sensor data (e.g., temperature, humidity) to typed C# classes and react to threshold violations in real time.
- **Industry 4.0 Applications**: Monitor and control factory machinery using MQTT messages structured as typed objects.
- **Edge Analytics**: Apply inline analytics (e.g., filtering, transformation) on the edge without manual MQTT client plumbing.
- **Home Automation Systems**: React to MQTT events (e.g., motion detection, switch toggles) declaratively in .NET apps.

---

### 🧱 Architecture Overview

- `TopicSet<T>`: A typed gateway to subscribe, publish, and filter MQTT messages mapped to the topic defined on type `T`.
- `MqttOrmContext`: The central configuration point that registers all MQTT entities and creates topic sets.
- `IMqttBus`: The abstraction over the MQTT client, which handles publishing, subscribing, and stream conversion.
- `TopicAttribute`: Metadata annotation that binds a C# class to a specific MQTT topic.

---

### 🤝 Philosophy

JoeDevSharp.MqttNet.ReactiveBinding promotes a **clean, reactive, and domain-driven** approach to working with MQTT. Instead of treating MQTT as a generic transport layer with string topics and JSON blobs, it treats it as a structured, type-safe message bus that seamlessly integrates with C#'s type system and LINQ capabilities.

The goal is to minimize boilerplate, enforce consistency, and make reactive MQTT applications more expressive and maintainable.

---

Let me know if you'd like this reformatted as a `README.md`, or integrated with badges, install instructions, and GitHub action workflows. I can also generate an architectural diagram or visual overview if needed.

## 📘 Developer Documentation – JoeDevSharp.MqttNet.ReactiveBinding

### Table of Contents

1. ✅ Introduction
2. 🚀 Installation & Setup
3. 🧩 Defining MQTT Entities
4. 🏗️ Creating the MQTT Context
5. 👂 Subscribing to Messages (Reactive)
6. 📤 Publishing Messages
7. 🧪 Full Console Example
8. 🛠️ Best Practices
9. ❓FAQ

---

### ✅ 1. Introduction

`JoeDevSharp.MqttNet.ReactiveBinding` is a lightweight framework that simplifies working with **MQTT topics as strongly typed reactive entities** in .NET applications. Inspired by Entity Framework, it enables a structured, observable, and declarative approach to working with real-time MQTT data.

---

### 🚀 2. Installation & Setup

Install the NuGet package:

```bash
dotnet add package JoeDevSharp.MqttNet.ReactiveBinding
```

Or reference the project directly if you’re working from source.

---

### 🧩 3. Defining MQTT Entities

Each entity class must be annotated with the `[Topic]` attribute to define the corresponding MQTT topic.

```csharp
using JoeDevSharp.MqttNet.ReactiveBinding.Attributes;

public class DHT230222_Modules
{
    public double Temperature { get; set; }
    public double Humidity { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
```

---

### 🏗️ 4. Creating the MQTT Context

The context exposes topic sets as typed properties, similar to EF’s `DbSet<T>`. Each topics must be annotated with the `[Topic]` attribute to define the corresponding MQTT topic.

```csharp
public class MqttContext : MqttOrmContext
{
    // Topic set for the DHT230222 module status updates
    [Topic("iot/devices/dht/modules/DHT230222_Modules/status")]
    public TopicSet<DHT230222_Modules> DHT230222_Modules { get; }

    // Example of another topic set using the '@' wildcard to be replaced by a parameter name
    [Topic("iot/devices/dht/sensors/@/status")]
    public TopicSet<DHT230222_Modules> EX4008_Sensor { get; }

    public MqttContext()
    {
        DHT230222_Modules = Set<DHT230222_Modules>();
    }
}
```

---

### 👂 5. Subscribing to Messages

Subscribe to incoming messages using LINQ-like filters:

```csharp
_context.DHT230222_Modules
    .Where(m => m.Temperature > 20)
    .Subscribe(m =>
    {
        Console.WriteLine($"High temperature: {m.Temperature}");
    });
```

Subscriptions are reactive and automatically connected to the MQTT bus.

---

### 📤 6. Publishing Messages

Send messages to the corresponding topic using the `Publish` method:

```csharp
_context.DHT230222_Modules.Publish(new DHT230222_Modules
{
    Temperature = 22.5,
    Humidity = 50.0
});
```

Publishing is handled asynchronously behind the scenes, but `Publish()` blocks until the operation is completed.

---

### 🧪 7. Full Console Example

```csharp
static void Main(string[] args)
{
    var context = new MqttContext();

    context.DHT230222_Modules.Subscribe(m =>
    {
        Console.WriteLine($"Received: Temp = {m.Temperature} °C, Humidity = {m.Humidity} %");
    });

    context.DHT230222_Modules.Publish(new DHT230222_Modules
    {
        Temperature = 18.0,
        Humidity = 45.0
    });

    Console.ReadLine(); // Keep the application alivez
}
```

---

### 🛠️ 8. Best Practices

- Wait for connection and subscription to complete before publishing.
- Use `.Where()` to filter messages and reduce unnecessary processing.
- Avoid long-running operations inside `.Subscribe()`; use async delegates or background workers.

---

### ❓ 9. FAQ

**Q: Can I use MQTT wildcards (+, #)?**
Yes, set `AllowWildcards = true` in the `[Topic]` attribute.

**Q: What does `Set<T>()` do in the context?**
It returns a `TopicSet<T>` that manages publishing and subscribing to the associated MQTT topic.

**Q: Is this compatible with any MQTT broker?**
Yes. The library works with any broker that supports standard MQTT 3.1.1/5.0 (e.g., Mosquitto, HiveMQ, EMQX).
