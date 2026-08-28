using System.Reflection;
using System.Text.Json;

namespace Net.Mqtt.Infrastructure.Contracts;

/// <summary>Defines the accepted schema-version relationship for an event contract.</summary>
public enum ContractCompatibility
{
    /// <summary>Requires the exact registered schema version.</summary>
    Exact,
    /// <summary>Accepts schemas with the same major version.</summary>
    SameMajor,
    /// <summary>Accepts backward-compatible schemas up to the registered version.</summary>
    BackwardCompatible
}

/// <summary>Describes the governed relationship between an Event Entity, its CloudEvents type, schema, and version.</summary>
/// <param name="EventType">The CloudEvent type.</param>
/// <param name="DataSchema">The canonical schema URI.</param>
/// <param name="DataType">The CLR Event Entity type.</param>
/// <param name="Version">The contract version.</param>
/// <param name="Compatibility">The accepted compatibility policy.</param>
/// <param name="MaximumDataSize">The maximum serialized data size in bytes.</param>
/// <param name="ForbiddenFields">Fields that must not occur in data.</param>
/// <param name="JsonMapper">An optional governed JSON mapper.</param>
/// <param name="CompatibleSchemas">Additional schema URIs accepted for this contract.</param>
public sealed record EventEntityDescriptor(
    string EventType,
    Uri DataSchema,
    Type DataType,
    Version Version,
    ContractCompatibility Compatibility = ContractCompatibility.Exact,
    int MaximumDataSize = 1024 * 1024,
    IReadOnlySet<string>? ForbiddenFields = null,
    IContractJsonMapper? JsonMapper = null,
    IReadOnlySet<Uri>? CompatibleSchemas = null);

/// <summary>Resolves registered Event Entities by CloudEvents type or CLR type.</summary>
public interface IEventEntityRegistry
{
    /// <summary>Gets the get by event type operation.</summary>
    EventEntityDescriptor GetByEventType(string eventType);
    /// <summary>Gets the get by data type operation.</summary>
    EventEntityDescriptor GetByDataType(Type dataType);
}

/// <summary>Defines icontract json mapper.</summary>
public interface IContractJsonMapper
{
    /// <summary>Serializes the serialize operation.</summary>
    ReadOnlyMemory<byte> Serialize(object data, Type dataType);
    /// <summary>Deserializes the deserialize operation.</summary>
    object Deserialize(ReadOnlyMemory<byte> json, Type dataType);
}

/// <summary>Declares the CloudEvents type associated with an Event Entity.</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public sealed class EventTypeAttribute(string eventType) : Attribute
{
    /// <summary>Gets the CloudEvents type.</summary>
    public string EventType { get; } = eventType;
}

/// <summary>Declares the absolute CloudEvents dataschema URI associated with an Event Entity.</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public sealed class DataSchemaAttribute(string dataSchema) : Attribute
{
    /// <summary>Gets the absolute dataschema URI.</summary>
    public string DataSchema { get; } = dataSchema;
}

/// <summary>Declares the governed version associated with an Event Entity.</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public sealed class EventVersionAttribute(string version) : Attribute
{
    /// <summary>Gets the contract version.</summary>
    public string Version { get; } = version;
}

/// <summary>Declares the maximum serialized data size accepted for an Event Entity.</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public sealed class MaximumDataSizeAttribute(int bytes) : Attribute
{
    /// <summary>Gets the maximum serialized data size in bytes.</summary>
    public int Bytes { get; } = bytes;
}

/// <summary>Declares the schema compatibility policy for an Event Entity.</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public sealed class SchemaCompatibilityAttribute(ContractCompatibility compatibility) : Attribute
{
    /// <summary>Gets the schema compatibility policy.</summary>
    public ContractCompatibility Compatibility { get; } = compatibility;
}

/// <summary>Forbids a field anywhere in the serialized event data.</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = true, Inherited = false)]
public sealed class ForbiddenFieldAttribute(string fieldName) : Attribute
{
    /// <summary>Gets the forbidden field name.</summary>
    public string FieldName { get; } = fieldName;
}

/// <summary>Declares an additional dataschema URI accepted by an event contract.</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = true, Inherited = false)]
public sealed class CompatibleDataSchemaAttribute(string dataSchema) : Attribute
{
    /// <summary>Gets the compatible dataschema URI.</summary>
    public string DataSchema { get; } = dataSchema;
}

/// <summary>Declares the parameterless JSON mapper used by an Event Entity.</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public sealed class ContractJsonMapperAttribute(Type mapperType) : Attribute
{
    /// <summary>Gets the JSON mapper type.</summary>
    public Type MapperType { get; } = mapperType;
}

/// <summary>Represents the registry of governed Event Entities.</summary>
public sealed class EventEntityRegistry : IEventEntityRegistry
{
    private readonly IReadOnlyDictionary<string, EventEntityDescriptor> _byEventType;
    private readonly IReadOnlyDictionary<Type, EventEntityDescriptor> _byDataType;

    internal EventEntityRegistry(IEnumerable<EventEntityDescriptor> entities)
    {
        var list = entities.ToArray();
        _byEventType = list.ToDictionary(x => x.EventType, StringComparer.Ordinal);
        _byDataType = list.ToDictionary(x => x.DataType);
    }

    /// <summary>Gets the get by event type operation.</summary>
    public EventEntityDescriptor GetByEventType(string eventType) =>
        _byEventType.TryGetValue(eventType, out var contract) ? contract
        : throw new UnknownEventEntityException($"CloudEvent type '{eventType}' is not registered as an Event Entity.");

    /// <summary>Gets the get by data type operation.</summary>
    public EventEntityDescriptor GetByDataType(Type dataType) =>
        _byDataType.TryGetValue(dataType, out var contract) ? contract
        : throw new UnknownEventEntityException($"Type '{dataType.FullName}' is not registered as an Event Entity.");
}

/// <summary>Builds a registry from attributed Event Entity types.</summary>
public sealed class EventEntityRegistryBuilder
{
    private const int DefaultMaximumDataSize = 1024 * 1024;
    private readonly List<EventEntityDescriptor> _entities = [];

    /// <summary>Registers the Event Entity metadata declared on <typeparamref name="TData"/>.</summary>
    /// <exception cref="InvalidOperationException">Required CloudEvents contract metadata is missing or invalid.</exception>
    public EventEntityRegistryBuilder Add<TData>()
    {
        Add(typeof(TData));
        return this;
    }

    /// <summary>Discovers and registers attributed Event Entities from an assembly.</summary>
    public EventEntityRegistryBuilder AddEventEntities(Assembly assembly)
    {
        foreach (var type in assembly.ExportedTypes)
            if (HasContractMetadata(type))
                Add(type);
        return this;
    }

    /// <summary>Creates the build operation.</summary>
    public EventEntityRegistry Build() => new(_entities);

    private void Add(Type type)
    {
        var eventType = type.GetCustomAttribute<EventTypeAttribute>()?.EventType;
        var dataSchema = type.GetCustomAttribute<DataSchemaAttribute>()?.DataSchema;
        var versionText = type.GetCustomAttribute<EventVersionAttribute>()?.Version;
        var missing = new List<string>();

        if (string.IsNullOrWhiteSpace(eventType)) missing.Add(nameof(EventTypeAttribute));
        if (string.IsNullOrWhiteSpace(dataSchema)) missing.Add(nameof(DataSchemaAttribute));
        if (string.IsNullOrWhiteSpace(versionText)) missing.Add(nameof(EventVersionAttribute));

        if (missing.Count > 0)
            throw new InvalidOperationException(
                $"Event Entity '{type.FullName}' is missing required CloudEvents attributes: {string.Join(", ", missing)}.");

        if (!Uri.TryCreate(dataSchema, UriKind.Absolute, out var schema))
            throw new InvalidOperationException(
                $"Event Entity '{type.FullName}' has an invalid {nameof(DataSchemaAttribute)} value '{dataSchema}'. An absolute URI is required.");
        if (!Version.TryParse(versionText, out var version))
            throw new InvalidOperationException(
                $"Event Entity '{type.FullName}' has an invalid {nameof(EventVersionAttribute)} value '{versionText}'.");

        var maximumDataSize = type.GetCustomAttribute<MaximumDataSizeAttribute>()?.Bytes ?? DefaultMaximumDataSize;
        if (maximumDataSize <= 0)
            throw new InvalidOperationException(
                $"Event Entity '{type.FullName}' has an invalid {nameof(MaximumDataSizeAttribute)} value '{maximumDataSize}'. It must be greater than zero.");

        var compatibility = type.GetCustomAttribute<SchemaCompatibilityAttribute>()?.Compatibility
            ?? ContractCompatibility.Exact;
        var forbiddenFields = type.GetCustomAttributes<ForbiddenFieldAttribute>()
            .Select(attribute => attribute.FieldName)
            .ToHashSet(StringComparer.Ordinal);
        if (forbiddenFields.Any(string.IsNullOrWhiteSpace))
            throw new InvalidOperationException(
                $"Event Entity '{type.FullName}' has an empty {nameof(ForbiddenFieldAttribute)} value.");

        var compatibleSchemas = new HashSet<Uri>();
        foreach (var attribute in type.GetCustomAttributes<CompatibleDataSchemaAttribute>())
        {
            if (!Uri.TryCreate(attribute.DataSchema, UriKind.Absolute, out var compatibleSchema))
                throw new InvalidOperationException(
                    $"Event Entity '{type.FullName}' has an invalid {nameof(CompatibleDataSchemaAttribute)} value '{attribute.DataSchema}'. An absolute URI is required.");
            compatibleSchemas.Add(compatibleSchema);
        }

        IContractJsonMapper? jsonMapper = null;
        if (type.GetCustomAttribute<ContractJsonMapperAttribute>() is { } mapperAttribute)
        {
            if (!typeof(IContractJsonMapper).IsAssignableFrom(mapperAttribute.MapperType))
                throw new InvalidOperationException(
                    $"JSON mapper type '{mapperAttribute.MapperType.FullName}' declared by '{type.FullName}' must implement {nameof(IContractJsonMapper)}.");
            try
            {
                jsonMapper = (IContractJsonMapper?)Activator.CreateInstance(mapperAttribute.MapperType);
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException(
                    $"JSON mapper type '{mapperAttribute.MapperType.FullName}' declared by '{type.FullName}' must have an accessible parameterless constructor.", exception);
            }
        }

        _entities.Add(new(eventType!, schema, type, version, compatibility, maximumDataSize,
            forbiddenFields, jsonMapper, compatibleSchemas));
    }

    private static bool HasContractMetadata(Type type) =>
        type.IsDefined(typeof(EventTypeAttribute), false)
        || type.IsDefined(typeof(DataSchemaAttribute), false)
        || type.IsDefined(typeof(EventVersionAttribute), false)
        || type.IsDefined(typeof(MaximumDataSizeAttribute), false)
        || type.IsDefined(typeof(SchemaCompatibilityAttribute), false)
        || type.IsDefined(typeof(ForbiddenFieldAttribute), false)
        || type.IsDefined(typeof(CompatibleDataSchemaAttribute), false)
        || type.IsDefined(typeof(ContractJsonMapperAttribute), false);
}

/// <summary>Defines inon retryable error.</summary>
public interface INonRetryableError
{
    /// <summary>Gets whether retrying the failed operation can succeed without changing the message.</summary>
    bool IsRetryable { get; }
}
/// <summary>Represents contract validation exception.</summary>
public abstract class ContractValidationException : Exception, INonRetryableError
{
    /// <summary>Executes the contract validation exception operation.</summary>
    protected ContractValidationException(string message) : base(message) { }
    /// <summary>Executes the contract validation exception operation.</summary>
    protected ContractValidationException(string message, Exception inner) : base(message, inner) { }
    /// <summary>Gets is retryable.</summary>
    public bool IsRetryable => false;
}
/// <summary>Represents an unknown or unregistered Event Entity.</summary>
public sealed class UnknownEventEntityException(string message) : ContractValidationException(message);
/// <summary>Represents contract mismatch exception.</summary>
public sealed class ContractMismatchException(string message) : ContractValidationException(message);
