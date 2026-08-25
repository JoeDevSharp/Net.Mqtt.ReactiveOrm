using System.Text.Json;

namespace Net.Mqtt.ReactiveOrm.CloudEvents;

public sealed class JsonCloudEventCodec(JsonSerializerOptions? options = null) : ICloudEventCodec
{
    public const string StructuredContentType = "application/cloudevents+json; charset=utf-8";
    private readonly JsonSerializerOptions _options = options ?? new(JsonSerializerDefaults.Web);

    public ReadOnlyMemory<byte> Serialize<TData>(CloudEventMessage<TData> message)
    {
        Validate(message);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteString("specversion", message.SpecVersion);
            writer.WriteString("id", message.Id);
            writer.WriteString("source", message.Source.OriginalString);
            writer.WriteString("type", message.Type);
            WriteOptional(writer, "subject", message.Subject);
            if (message.Time is { } time) writer.WriteString("time", time);
            WriteOptional(writer, "datacontenttype", message.DataContentType);
            if (message.DataSchema is { } schema) writer.WriteString("dataschema", schema.OriginalString);
            WriteExtensions(writer, message.Extensions);
            writer.WritePropertyName("data");
            JsonSerializer.Serialize(writer, message.Data, _options);
            writer.WriteEndObject();
        }
        return stream.ToArray();
    }

    public CloudEventMessage<TData> Deserialize<TData>(ReadOnlyMemory<byte> payload, string? contentType)
    {
        ValidateContentType(contentType);
        JsonDocument document;
        try { document = JsonDocument.Parse(payload); }
        catch (JsonException error) { throw new InvalidDataException("The MQTT payload is not valid structured CloudEvents JSON.", error); }
        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) throw new InvalidDataException("A structured CloudEvent must be a JSON object.");
            EnsureUniqueAttributes(root);
            var dataElement = Required(root, "data");
            var data = dataElement.Deserialize<TData>(_options)
                ?? throw new InvalidDataException($"CloudEvent data cannot be deserialized as {typeof(TData).Name}.");
            var message = new CloudEventMessage<TData>
            {
                SpecVersion = RequiredString(root, "specversion"),
                Id = RequiredString(root, "id"),
                Source = RequiredUri(root, "source"),
                Type = RequiredString(root, "type"),
                Subject = OptionalString(root, "subject"),
                Time = OptionalDateTimeOffset(root, "time"),
                DataContentType = RequiredString(root, "datacontenttype"),
                DataSchema = OptionalUri(root, "dataschema"),
                Data = data,
                Extensions = ReadExtensions(root)
            };
            Validate(message);
            return message;
        }
    }

    private static void Validate<TData>(CloudEventMessage<TData> message)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (message.SpecVersion != "1.0") throw new InvalidDataException("Only CloudEvents specversion 1.0 is supported.");
        ArgumentException.ThrowIfNullOrWhiteSpace(message.Id);
        if (!message.Source.IsAbsoluteUri) throw new InvalidDataException("CloudEvent source must be an absolute URI.");
        ArgumentException.ThrowIfNullOrWhiteSpace(message.Type);
        ArgumentException.ThrowIfNullOrWhiteSpace(message.DataContentType);
        ArgumentNullException.ThrowIfNull(message.Data);
        CloudEventValidation.ValidateExtensions(message.Extensions);
    }

    private static void ValidateContentType(string? contentType)
    {
        if (contentType is null) return; // MQTT 3.1.1 has no Content Type property.
        var mediaType = contentType.Split(';', 2)[0].Trim();
        if (!mediaType.Equals("application/cloudevents+json", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"MQTT payload content type '{contentType}' is not structured CloudEvents JSON.");
    }

    private static void WriteExtensions(Utf8JsonWriter writer, CloudEventExtensions extensions)
    {
        WriteOptional(writer, "correlationid", extensions.CorrelationId);
        WriteOptional(writer, "causationid", extensions.CausationId);
        WriteOptional(writer, "traceparent", extensions.TraceParent);
        WriteOptional(writer, "tracestate", extensions.TraceState);
        WriteOptional(writer, "negotiationid", extensions.NegotiationId);
        if (extensions.ExpiresAt is { } expiresAt) writer.WriteString("expiresat", expiresAt);
        foreach (var extension in extensions.Additional) writer.WriteString(extension.Key, extension.Value);
    }

    private static void EnsureUniqueAttributes(JsonElement root)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in root.EnumerateObject())
            if (!names.Add(property.Name))
                throw new InvalidDataException($"CloudEvent attribute '{property.Name}' is duplicated.");
    }

    private static CloudEventExtensions ReadExtensions(JsonElement root)
    {
        var additional = new Dictionary<string, string>(StringComparer.Ordinal);
        var known = new HashSet<string>(StringComparer.Ordinal)
        {
            "specversion", "id", "source", "type", "subject", "time", "datacontenttype", "dataschema", "data",
            "correlationid", "causationid", "traceparent", "tracestate", "negotiationid", "expiresat"
        };
        foreach (var property in root.EnumerateObject())
            if (!known.Contains(property.Name))
                additional[property.Name] = property.Value.ValueKind == JsonValueKind.String
                    ? property.Value.GetString()! : property.Value.GetRawText();
        var result = new CloudEventExtensions
        {
            CorrelationId = OptionalString(root, "correlationid"),
            CausationId = OptionalString(root, "causationid"),
            TraceParent = OptionalString(root, "traceparent"),
            TraceState = OptionalString(root, "tracestate"),
            NegotiationId = OptionalString(root, "negotiationid"),
            ExpiresAt = OptionalDateTimeOffset(root, "expiresat"),
            Additional = additional
        };
        CloudEventValidation.ValidateExtensions(result);
        return result;
    }

    private static void WriteOptional(Utf8JsonWriter writer, string name, string? value)
    { if (value is not null) writer.WriteString(name, value); }
    private static JsonElement Required(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) ? value : throw new InvalidDataException($"Required CloudEvent attribute '{name}' is missing.");
    private static string RequiredString(JsonElement root, string name) =>
        Required(root, name).ValueKind == JsonValueKind.String && Required(root, name).GetString() is { Length: > 0 } value
            ? value : throw new InvalidDataException($"Required CloudEvent attribute '{name}' must be a non-empty string.");
    private static string? OptionalString(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind != JsonValueKind.Null ? value.GetString() : null;
    private static Uri RequiredUri(JsonElement root, string name) =>
        Uri.TryCreate(RequiredString(root, name), UriKind.Absolute, out var uri) ? uri : throw new InvalidDataException($"CloudEvent attribute '{name}' is not an absolute URI.");
    private static Uri? OptionalUri(JsonElement root, string name)
    {
        var value = OptionalString(root, name);
        if (value is null) return null;
        return Uri.TryCreate(value, UriKind.Absolute, out var uri) ? uri
            : throw new InvalidDataException($"CloudEvent attribute '{name}' is not an absolute URI.");
    }

    private static DateTimeOffset? OptionalDateTimeOffset(JsonElement root, string name)
    {
        var value = OptionalString(root, name);
        if (value is null) return null;
        return DateTimeOffset.TryParse(value, null, System.Globalization.DateTimeStyles.RoundtripKind, out var result) ? result
            : throw new InvalidDataException($"CloudEvent attribute '{name}' is not an RFC 3339 timestamp.");
    }
}
