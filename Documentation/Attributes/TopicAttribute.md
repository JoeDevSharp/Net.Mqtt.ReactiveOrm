# TopicAttribute Class Documentation

## Definition

`MqttReactiveObjectMapper.Attributes.TopicAttribute`

An attribute used to associate an MQTT topic with a class or property. It supports topic templating, QoS level configuration, and wildcard usage for subscriptions.

---

## Remarks

This attribute allows defining an MQTT topic at the class or property level. It provides control over the topic template, quality of service (QoS), and wildcard usage for subscribing purposes. Topic templates may contain placeholders (e.g., `@`) that can be dynamically resolved at runtime.

---

## Constructors

### `TopicAttribute(string template, MqttQualityOfServiceLevel QoS = MqttQualityOfServiceLevel.AtMostOnce, bool allowWildcards = false)`

Creates a new instance of the `TopicAttribute`.

**Parameters:**

- `template` (`string`): MQTT topic template. Must be a non-empty string. May include placeholders such as `@` for dynamic substitution.
- `QoS` (`MqttQualityOfServiceLevel`, optional): Specifies the MQTT QoS level. Default is `AtMostOnce`.
- `allowWildcards` (`bool`, optional): Indicates whether MQTT wildcards (`+`, `#`) are allowed. Default is `false`.

**Exceptions:**

- `ArgumentException`: Thrown if the template is null or empty.

---

## Properties

### `string Template`

Gets the MQTT topic template (e.g., `sensors/{deviceId}/temperature`). Can include placeholders or wildcards.

### `bool AllowWildcards`

Indicates whether MQTT wildcards (`+`, `#`) are permitted in the topic.

### `MqttQualityOfServiceLevel QoS`

Defines the Quality of Service level for the topic.

---

## Methods

### `string Resolve<T>(T topicClass)`

Resolves the topic template by replacing placeholders with values derived from the provided instance type.

**Type Parameters:**

- `T`: Type of the instance related to the topic.

**Parameters:**

- `topicClass` (`T`): An instance of the class to extract values for replacement.

**Returns:**

- `string`: The resolved MQTT topic string. If `topicClass` is `null`, returns the raw template.

---

## Operators

_None_

---

## Explicit Interface Implementations

_None_

---

## Extension Methods

_None_

---
