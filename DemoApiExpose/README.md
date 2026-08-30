# DemoApiExpose

ASP.NET Core demo exposing MQTT request/reply through both an MVC controller and Minimal API.

```powershell
dotnet run --project DemoApiExpose/DemoApiExpose.csproj --urls http://localhost:5080
```

The default configuration uses `InMemoryMqttBus`, so no broker is required.

Controller endpoint:

```powershell
Invoke-RestMethod http://localhost:5080/api/controller/messages `
  -Method Post -ContentType application/json `
  -Body '{"message":"hello"}'
```

Minimal API endpoint:

```powershell
Invoke-RestMethod http://localhost:5080/api/minimal/messages `
  -Method Post -ContentType application/json `
  -Body '{"message":"hello"}'
```

Set `Mqtt:UseInMemory` to `false` to use the configured MQTT broker.
