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

/// <summary>Describes the governed relationship between an event type, schema, version, and CLR type.</summary>
/// <param name="EventType">The CloudEvent type.</param>
/// <param name="DataSchema">The canonical schema URI.</param>
/// <param name="DataType">The generated CLR data type.</param>
/// <param name="Version">The contract version.</param>
/// <param name="Compatibility">The accepted compatibility policy.</param>
/// <param name="MaximumDataSize">The maximum serialized data size in bytes.</param>
/// <param name="ForbiddenFields">Fields that must not occur in data.</param>
/// <param name="JsonMapper">An optional governed JSON mapper.</param>
/// <param name="CompatibleSchemas">Additional schema URIs accepted for this contract.</param>
public sealed record EventContractDescriptor(
    string EventType,
    Uri DataSchema,
    Type DataType,
    Version Version,
    ContractCompatibility Compatibility = ContractCompatibility.Exact,
    int MaximumDataSize = 1024 * 1024,
    IReadOnlySet<string>? ForbiddenFields = null,
    IContractJsonMapper? JsonMapper = null,
    IReadOnlySet<Uri>? CompatibleSchemas = null);

/// <summary>Defines ievent contract registry.</summary>
public interface IEventContractRegistry
{
    /// <summary>Gets the get by event type operation.</summary>
    EventContractDescriptor GetByEventType(string eventType);
    /// <summary>Gets the get by data type operation.</summary>
    EventContractDescriptor GetByDataType(Type dataType);
}

/// <summary>Defines icontract json mapper.</summary>
public interface IContractJsonMapper
{
    /// <summary>Serializes the serialize operation.</summary>
    ReadOnlyMemory<byte> Serialize(object data, Type dataType);
    /// <summary>Deserializes the deserialize operation.</summary>
    object Deserialize(ReadOnlyMemory<byte> json, Type dataType);
}

/// <summary>Declares contract metadata on a generated CLR event-data type.</summary>
/// <param name="eventType">The CloudEvent type.</param>
/// <param name="dataSchema">The absolute schema URI.</param>
/// <param name="version">The contract version.</param>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false)]
public sealed class EventContractAttribute(string eventType, string dataSchema, string version) : Attribute
{
    /// <summary>Gets event type.</summary>
    public string EventType { get; } = eventType;
    /// <summary>Gets data schema.</summary>
    public string DataSchema { get; } = dataSchema;
    /// <summary>Gets version.</summary>
    public string Version { get; } = version;
}

/// <summary>Represents event contract registry.</summary>
public sealed class EventContractRegistry : IEventContractRegistry
{
    private readonly IReadOnlyDictionary<string, EventContractDescriptor> _byEventType;
    private readonly IReadOnlyDictionary<Type, EventContractDescriptor> _byDataType;

    internal EventContractRegistry(IEnumerable<EventContractDescriptor> contracts)
    {
        var list = contracts.ToArray();
        _byEventType = list.ToDictionary(x => x.EventType, StringComparer.Ordinal);
        _byDataType = list.ToDictionary(x => x.DataType);
    }

    /// <summary>Gets the get by event type operation.</summary>
    public EventContractDescriptor GetByEventType(string eventType) =>
        _byEventType.TryGetValue(eventType, out var contract) ? contract
        : throw new UnknownEventContractException($"CloudEvent type '{eventType}' is not registered.");

    /// <summary>Gets the get by data type operation.</summary>
    public EventContractDescriptor GetByDataType(Type dataType) =>
        _byDataType.TryGetValue(dataType, out var contract) ? contract
        : throw new UnknownEventContractException($"Data type '{dataType.FullName}' is not registered.");
}

/// <summary>Represents event contract registry builder.</summary>
public sealed class EventContractRegistryBuilder
{
    private readonly List<EventContractDescriptor> _contracts = [];

    /// <summary>Adds the add&lt;tdata&gt; operation.</summary>
    public EventContractRegistryBuilder Add<TData>(string eventType, Uri dataSchema, Version version,
        ContractCompatibility compatibility = ContractCompatibility.Exact, int maximumDataSize = 1024 * 1024,
        IEnumerable<string>? forbiddenFields = null, IContractJsonMapper? jsonMapper = null,
        IEnumerable<Uri>? compatibleSchemas = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType);
        if (!dataSchema.IsAbsoluteUri) throw new ArgumentException("Contract dataschema must be absolute.", nameof(dataSchema));
        if (maximumDataSize <= 0) throw new ArgumentOutOfRangeException(nameof(maximumDataSize));
        _contracts.Add(new(eventType, dataSchema, typeof(TData), version, compatibility, maximumDataSize,
            forbiddenFields?.ToHashSet(StringComparer.Ordinal), jsonMapper,
            compatibleSchemas?.ToHashSet()));
        return this;
    }

    /// <summary>Adds the add generated contracts operation.</summary>
    public EventContractRegistryBuilder AddGeneratedContracts(Assembly assembly)
    {
        foreach (var type in assembly.ExportedTypes)
            if (type.GetCustomAttribute<EventContractAttribute>() is { } attribute)
                Add(type, attribute);
        return this;
    }

    /// <summary>Creates the build operation.</summary>
    public EventContractRegistry Build() => new(_contracts);

    private void Add(Type type, EventContractAttribute attribute)
    {
        if (!Uri.TryCreate(attribute.DataSchema, UriKind.Absolute, out var schema))
            throw new InvalidOperationException($"Generated contract '{type.FullName}' has an invalid dataschema URI.");
        if (!Version.TryParse(attribute.Version, out var version))
            throw new InvalidOperationException($"Generated contract '{type.FullName}' has an invalid version.");
        _contracts.Add(new(attribute.EventType, schema, type, version));
    }
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
/// <summary>Represents unknown event contract exception.</summary>
public sealed class UnknownEventContractException(string message) : ContractValidationException(message);
/// <summary>Represents contract mismatch exception.</summary>
public sealed class ContractMismatchException(string message) : ContractValidationException(message);
