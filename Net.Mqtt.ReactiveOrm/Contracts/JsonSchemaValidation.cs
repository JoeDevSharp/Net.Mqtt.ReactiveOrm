using System.Net.Http.Headers;
using System.Text.Json;

namespace Net.Mqtt.ReactiveOrm.Contracts;

public sealed record ValidationError(string Path, string Message);
public sealed record ValidationResult(bool IsValid, IReadOnlyList<ValidationError> Errors)
{
    public static ValidationResult Success { get; } = new(true, []);
}

public interface IEventDataValidator
{
    ValueTask<ValidationResult> ValidateAsync(Uri dataSchema, ReadOnlyMemory<byte> data, CancellationToken cancellationToken);
}

public sealed record ResolvedJsonSchema(Uri Uri, ReadOnlyMemory<byte> Content, string Version);

public interface IJsonSchemaResolver
{
    ValueTask<ResolvedJsonSchema> ResolveAsync(Uri dataSchema, CancellationToken cancellationToken);
}

public sealed class InMemoryJsonSchemaResolver : IJsonSchemaResolver
{
    private readonly Dictionary<Uri, ResolvedJsonSchema> _schemas = [];
    public InMemoryJsonSchemaResolver Add(Uri uri, string jsonSchema, string version)
    {
        _schemas[uri] = new(uri, System.Text.Encoding.UTF8.GetBytes(jsonSchema), version);
        return this;
    }
    public ValueTask<ResolvedJsonSchema> ResolveAsync(Uri dataSchema, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(_schemas.TryGetValue(dataSchema, out var schema) ? schema
            : throw new JsonSchemaResolutionException($"Schema '{dataSchema}' is not registered locally."));
    }
}

public sealed class FileJsonSchemaResolver(IReadOnlyDictionary<Uri, string> files) : IJsonSchemaResolver
{
    public async ValueTask<ResolvedJsonSchema> ResolveAsync(Uri dataSchema, CancellationToken cancellationToken)
    {
        if (!files.TryGetValue(dataSchema, out var path))
            throw new JsonSchemaResolutionException($"Schema '{dataSchema}' has no local file mapping.");
        var content = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
        return new(dataSchema, content, File.GetLastWriteTimeUtc(path).Ticks.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }
}

public sealed class HttpJsonSchemaResolver(HttpClient httpClient, int maximumSchemaSize = 2 * 1024 * 1024) : IJsonSchemaResolver
{
    public async ValueTask<ResolvedJsonSchema> ResolveAsync(Uri dataSchema, CancellationToken cancellationToken)
    {
        if (dataSchema.Scheme is not ("http" or "https"))
            throw new JsonSchemaResolutionException($"Schema URI '{dataSchema}' is not HTTP(S).");
        using var response = await httpClient.GetAsync(dataSchema, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength > maximumSchemaSize)
            throw new JsonSchemaResolutionException($"Schema '{dataSchema}' exceeds the configured size limit.");
        var content = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        if (content.Length > maximumSchemaSize) throw new JsonSchemaResolutionException($"Schema '{dataSchema}' exceeds the configured size limit.");
        var version = response.Headers.ETag?.Tag ?? response.Content.Headers.LastModified?.ToString("O") ?? "unversioned";
        return new(dataSchema, content, version);
    }
}

public sealed class CompositeJsonSchemaResolver(params IJsonSchemaResolver[] resolvers) : IJsonSchemaResolver
{
    public async ValueTask<ResolvedJsonSchema> ResolveAsync(Uri dataSchema, CancellationToken cancellationToken)
    {
        List<Exception> errors = [];
        foreach (var resolver in resolvers)
        {
            try { return await resolver.ResolveAsync(dataSchema, cancellationToken).ConfigureAwait(false); }
            catch (JsonSchemaResolutionException error) { errors.Add(error); }
        }
        throw new JsonSchemaResolutionException($"Schema '{dataSchema}' could not be resolved.", new AggregateException(errors));
    }
}

public sealed class CachingJsonSchemaResolver(IJsonSchemaResolver inner, int capacity = 64, TimeSpan? freshness = null) : IJsonSchemaResolver
{
    private readonly Dictionary<Uri, (ResolvedJsonSchema Schema, LinkedListNode<Uri> Node, DateTimeOffset CachedAt)> _cache = [];
    private readonly LinkedList<Uri> _lru = [];
    private readonly object _gate = new();

    public async ValueTask<ResolvedJsonSchema> ResolveAsync(Uri dataSchema, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            if (_cache.TryGetValue(dataSchema, out var cached) && DateTimeOffset.UtcNow - cached.CachedAt < (freshness ?? TimeSpan.FromMinutes(15)))
            {
                _lru.Remove(cached.Node);
                _lru.AddFirst(cached.Node);
                return cached.Schema;
            }
            if (_cache.Remove(dataSchema, out var expired)) _lru.Remove(expired.Node);
        }
        var resolved = await inner.ResolveAsync(dataSchema, cancellationToken).ConfigureAwait(false);
        lock (_gate)
        {
            if (_cache.TryGetValue(dataSchema, out var existing)) return existing.Schema;
            var node = _lru.AddFirst(dataSchema);
            _cache[dataSchema] = (resolved, node, DateTimeOffset.UtcNow);
            while (_cache.Count > Math.Max(1, capacity))
            {
                var last = _lru.Last!;
                _lru.RemoveLast();
                _cache.Remove(last.Value);
            }
        }
        return resolved;
    }
}

public sealed class JsonSchemaEventDataValidator(IJsonSchemaResolver resolver) : IEventDataValidator
{
    public async ValueTask<ValidationResult> ValidateAsync(Uri dataSchema, ReadOnlyMemory<byte> data, CancellationToken cancellationToken)
    {
        var schema = await resolver.ResolveAsync(dataSchema, cancellationToken).ConfigureAwait(false);
        try
        {
            using var schemaDocument = JsonDocument.Parse(schema.Content);
            using var dataDocument = JsonDocument.Parse(data);
            List<ValidationError> errors = [];
            ValidateNode(schemaDocument.RootElement, schemaDocument.RootElement, dataDocument.RootElement, "$", errors);
            return errors.Count == 0 ? ValidationResult.Success : new(false, errors);
        }
        catch (JsonException error)
        {
            return new(false, [new("$", $"Invalid JSON or JSON Schema: {error.Message}")]);
        }
    }

    private static void ValidateNode(JsonElement rootSchema, JsonElement schema, JsonElement value, string path, List<ValidationError> errors)
    {
        if (schema.TryGetProperty("$ref", out var reference))
        {
            var target = ResolveLocalReference(rootSchema, reference.GetString()!);
            ValidateNode(rootSchema, target, value, path, errors);
            return;
        }
        if (schema.TryGetProperty("type", out var type) && !MatchesType(value, type.GetString()))
        {
            errors.Add(new(path, $"Expected JSON type '{type.GetString()}', found '{value.ValueKind}'."));
            return;
        }
        if (schema.TryGetProperty("enum", out var enumValues) && !enumValues.EnumerateArray().Any(item => JsonElement.DeepEquals(item, value)))
            errors.Add(new(path, "Value is not part of the schema enum."));
        if (value.ValueKind == JsonValueKind.Object)
        {
            if (schema.TryGetProperty("required", out var required))
                foreach (var property in required.EnumerateArray().Select(x => x.GetString()!))
                    if (!value.TryGetProperty(property, out _)) errors.Add(new($"{path}.{property}", "Required property is missing."));
            var properties = schema.TryGetProperty("properties", out var defined) ? defined : default;
            foreach (var property in value.EnumerateObject())
            {
                if (properties.ValueKind == JsonValueKind.Object && properties.TryGetProperty(property.Name, out var propertySchema))
                    ValidateNode(rootSchema, propertySchema, property.Value, $"{path}.{property.Name}", errors);
                else if (schema.TryGetProperty("additionalProperties", out var additional) && additional.ValueKind == JsonValueKind.False)
                    errors.Add(new($"{path}.{property.Name}", "Additional property is forbidden."));
            }
        }
        if (value.ValueKind == JsonValueKind.Array && schema.TryGetProperty("items", out var items))
        {
            var index = 0;
            foreach (var item in value.EnumerateArray()) ValidateNode(rootSchema, items, item, $"{path}[{index++}]", errors);
        }
        if (value.ValueKind == JsonValueKind.String)
        {
            var length = value.GetString()!.Length;
            if (schema.TryGetProperty("minLength", out var min) && length < min.GetInt32()) errors.Add(new(path, "String is shorter than minLength."));
            if (schema.TryGetProperty("maxLength", out var max) && length > max.GetInt32()) errors.Add(new(path, "String is longer than maxLength."));
        }
        if (value.ValueKind == JsonValueKind.Number)
        {
            var number = value.GetDouble();
            if (schema.TryGetProperty("minimum", out var min) && number < min.GetDouble()) errors.Add(new(path, "Number is below minimum."));
            if (schema.TryGetProperty("maximum", out var max) && number > max.GetDouble()) errors.Add(new(path, "Number is above maximum."));
        }
    }

    private static JsonElement ResolveLocalReference(JsonElement root, string reference)
    {
        if (!reference.StartsWith("#/", StringComparison.Ordinal))
            throw new JsonSchemaResolutionException($"External schema reference '{reference}' must be registered as the contract dataschema.");
        var current = root;
        foreach (var rawSegment in reference[2..].Split('/'))
        {
            var segment = rawSegment.Replace("~1", "/", StringComparison.Ordinal).Replace("~0", "~", StringComparison.Ordinal);
            if (!current.TryGetProperty(segment, out current))
                throw new JsonSchemaResolutionException($"JSON Schema reference '{reference}' cannot be resolved.");
        }
        return current;
    }

    private static bool MatchesType(JsonElement value, string? type) => type switch
    {
        "object" => value.ValueKind == JsonValueKind.Object,
        "array" => value.ValueKind == JsonValueKind.Array,
        "string" => value.ValueKind == JsonValueKind.String,
        "integer" => value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out _),
        "number" => value.ValueKind == JsonValueKind.Number,
        "boolean" => value.ValueKind is JsonValueKind.True or JsonValueKind.False,
        "null" => value.ValueKind == JsonValueKind.Null,
        _ => true
    };
}

public sealed class JsonSchemaResolutionException : IOException
{
    public JsonSchemaResolutionException(string message) : base(message) { }
    public JsonSchemaResolutionException(string message, Exception inner) : base(message, inner) { }
}

public sealed class EventDataValidationException(ValidationResult result)
    : ContractValidationException(string.Join("; ", result.Errors.Select(x => $"{x.Path}: {x.Message}")))
{
    public ValidationResult Result { get; } = result;
}
