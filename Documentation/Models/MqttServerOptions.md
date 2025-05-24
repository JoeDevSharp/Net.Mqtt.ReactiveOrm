# MqttServerOptions Class Documentation

## Definition

`MqttReactiveObjectMapper.Models.MqttServerOptions`

Represents configuration options for connecting to an MQTT server.

---

## Remarks

This class encapsulates basic settings required to establish a connection with an MQTT broker. It includes server address, authentication, client identity, and optional TLS usage.

Use this class when configuring an MQTT client with specific connection requirements, either manually or through dependency injection.

---

## Constructors

### `MqttServerOptions()`

Creates a new instance of the `MqttServerOptions` class with default values.

- `ClientId` is initialized as a random GUID-based string.
- `Server` defaults to `"localhost"`.
- `Port` defaults to `1883`.
- `UseTls` is disabled by default.

---

## Properties

### `string ClientId`

The MQTT client identifier. If not specified, a unique ID is generated automatically.

**Default:** `"mqttorm-{GUID}"`

---

### `string Server`

The address of the MQTT server (hostname or IP address).

**Default:** `"localhost"`

---

### `int Port`

The port number used to connect to the MQTT broker.

**Default:** `1883`

---

### `string? Username`

The username for authenticating with the MQTT broker (optional).

---

### `string? Password`

The password for authenticating with the MQTT broker (optional).

---

### `bool UseTls`

Indicates whether the connection should use TLS/SSL encryption.

**Default:** `false`

---

## Methods

_None_

---

## Operators

_None_

---

## Explicit Interface Implementations

_None_

---

## Extension Methods

\_None defined explicitly. You may use standard object extensions like `ToString()`, `Equals()`, etc.

---
