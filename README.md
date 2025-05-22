## 📘 Introduction

**Mqtt.Net.Orm** is a lightweight and expressive framework for .NET that brings an **object-oriented approach to MQTT messaging**. Inspired by the design principles of Object-Relational Mappers (ORMs), it allows developers to **define strongly-typed classes** that map directly to MQTT topics using simple attributes.

By abstracting away manual topic formatting and message serialization, `Mqtt.Net.Orm` simplifies the development of IoT, cloud, and industrial applications where devices and services communicate via MQTT.

Whether you're building an edge device, a backend message processor, or a full-scale IoT platform, `Mqtt.Net.Orm` helps you:

- ✅ **Publish and subscribe to MQTT topics using strongly-typed objects**
- ✅ **Automatically resolve topics from class attributes and runtime parameters**
- ✅ **Work with structured messages (JSON) without manual parsing**
- ✅ **Create readable, maintainable, and scalable MQTT-enabled applications**

> Define. Publish. Subscribe. All with clean and reliable .NET code.

---

## 📦 Framework Details

### 🧩 Name: **Mqtt.Net.Orm**

### 🛠 Primary Programming Language

✅ Entirely written in **C#** (.NET Standard), compatible with modern .NET development environments, including console applications, backend services, and embedded systems that support MQTT.

### 🎯 Main Purpose

**Mqtt.Net.Orm** aims to map strongly-typed .NET objects to MQTT topics, enabling simple and structured publishing and subscribing of complex messages. It’s designed to **simplify state synchronization, event handling, or structured data exchange** between IoT devices and backend systems—without needing to manually manipulate topic strings or handle serialization logic.

### 💡 What Makes It Different or Innovative?

✅ **Object-Oriented Design** – Use classes like `DeviceStatusMessage` decorated with attributes to represent MQTT messages.
✅ **Automatic Topic Resolution** – Topics such as `"devices/{DeviceId}/status"` are resolved dynamically using parameters at runtime via attributes like `[MqttTopic("devices/{DeviceId}/status")]`.
✅ **Typed Subscription and Publishing** – No need to manually serialize or parse JSON.
✅ **Seamless Integration** – Easily integrates into existing projects with minimal setup, including console apps or microservices.
✅ **ORM-like Philosophy** – Inspired by classic ORM patterns, but applied to an MQTT messaging bus instead of a database.

### 🧑‍💻 How Is It Meant to Be Used?

**Mqtt.Net.Orm** is designed for:

- Industrial IoT applications managing thousands of devices in the field.
- Desktop or backend applications communicating with gateways or sensors over MQTT.
- Cloud services that need to structure MQTT event logic using business objects.
- Embedded systems (e.g., Raspberry Pi, Linux ARM) integrating with MQTT brokers like Mosquitto.

### 🚀 What Inspired Its Creation?

The framework was born from real industrial needs to:

- **Reduce human error** in manually constructing MQTT topics.
- **Avoid repetitive boilerplate** for message serialization/deserialization.
- **Promote a safer, maintainable, and structured communication model**, similar to how Entity Framework handles persistence—but applied to message-driven systems.

---

**In summary**, _Mqtt.Net.Orm_ is a modern, strongly-typed solution for working with MQTT in .NET, with a professional and maintainable approach to messaging.
