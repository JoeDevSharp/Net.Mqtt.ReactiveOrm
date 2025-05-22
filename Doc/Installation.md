## 📦 Installation

### 🔧 Prerequisites

- [.NET 6.0](https://dotnet.microsoft.com/en-us/download) or higher (compatible with .NET Standard)
- An MQTT broker (e.g., [Mosquitto](https://mosquitto.org/), [EMQX](https://www.emqx.io/), or [HiveMQ](https://www.hivemq.com/))

### 📥 NuGet Package

If the package is published to NuGet, users can install it using the command:

```bash
dotnet add package Mqtt.Net.Orm
```

> 💡 **Note:** If the package is not yet published to NuGet, you can build the project locally and add a reference manually:

```bash
dotnet add reference ../Mqtt.Net.Orm/Mqtt.Net.Orm.csproj
```

### 🔌 Dependencies

Mqtt.Net.Orm relies on the following core libraries:

- `MQTTnet` – for MQTT communication
- `System.Text.Json` – for serialization (optional if you want to plug your own)
