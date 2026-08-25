using System.Reflection;
using System.Text.Json;

namespace Net.Mqtt.ReactiveOrm.Contracts;

public enum ContractCompatibility { Exact, SameMajor, BackwardCompatible }

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

public interface IEventContractRegistry
{
    EventContractDescriptor GetByEventType(string eventType);
    EventContractDescriptor GetByDataType(Type dataType);
}

public interface IContractJsonMapper
{
    ReadOnlyMemory<byte> Serialize(object data, Type dataType);
    object Deserialize(ReadOnlyMemory<byte> json, Type dataType);
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false)]
public sealed class EventContractAttribute(string eventType, string dataSchema, string version) : Attribute
{
    public string EventType { get; } = eventType;
    public string DataSchema { get; } = dataSchema;
    public string Version { get; } = version;
}

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

    public EventContractDescriptor GetByEventType(string eventType) =>
        _byEventType.TryGetValue(eventType, out var contract) ? contract
        : throw new UnknownEventContractException($"CloudEvent type '{eventType}' is not registered.");

    public EventContractDescriptor GetByDataType(Type dataType) =>
        _byDataType.TryGetValue(dataType, out var contract) ? contract
        : throw new UnknownEventContractException($"Data type '{dataType.FullName}' is not registered.");
}

public sealed class EventContractRegistryBuilder
{
    private readonly List<EventContractDescriptor> _contracts = [];

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

    public EventContractRegistryBuilder AddGeneratedContracts(Assembly assembly)
    {
        foreach (var type in assembly.ExportedTypes)
            if (type.GetCustomAttribute<EventContractAttribute>() is { } attribute)
                Add(type, attribute);
        return this;
    }

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

public interface INonRetryableError { bool IsRetryable { get; } }
public abstract class ContractValidationException : Exception, INonRetryableError
{
    protected ContractValidationException(string message) : base(message) { }
    protected ContractValidationException(string message, Exception inner) : base(message, inner) { }
    public bool IsRetryable => false;
}
public sealed class UnknownEventContractException(string message) : ContractValidationException(message);
public sealed class ContractMismatchException(string message) : ContractValidationException(message);
