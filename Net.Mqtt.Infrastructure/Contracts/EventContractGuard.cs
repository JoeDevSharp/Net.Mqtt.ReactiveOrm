using System.Text.Json;
using Net.Mqtt.Infrastructure.CloudEvents;

namespace Net.Mqtt.Infrastructure.Contracts;

internal static class EventEntityGuard
{
    public static void EnsureCompatible(EventEntityDescriptor contract, string eventType, Uri? dataSchema, Type dataType)
    {
        if (!string.Equals(contract.EventType, eventType, StringComparison.Ordinal))
            throw new ContractMismatchException($"Event type '{eventType}' is not valid for CLR type '{dataType.FullName}'.");
        if (contract.DataType != dataType)
            throw new ContractMismatchException($"Contract '{eventType}' maps to '{contract.DataType.FullName}', not '{dataType.FullName}'.");
        if (dataSchema is null) throw new ContractMismatchException($"CloudEvent '{eventType}' requires dataschema.");
        if (!IsSchemaCompatible(contract, dataSchema))
            throw new ContractMismatchException($"Dataschema '{dataSchema}' is not compatible with contract '{contract.DataSchema}'.");
    }

    public static ValidationResult ValidateLimits(EventEntityDescriptor contract, ReadOnlyMemory<byte> data)
    {
        List<ValidationError> errors = [];
        if (data.Length > contract.MaximumDataSize)
            errors.Add(new("$", $"Data size {data.Length} exceeds limit {contract.MaximumDataSize}."));
        if (contract.ForbiddenFields is { Count: > 0 })
        {
            using var document = JsonDocument.Parse(data);
            FindForbidden(document.RootElement, "$", contract.ForbiddenFields, errors);
        }
        return errors.Count == 0 ? ValidationResult.Success : new(false, errors);
    }

    private static bool IsSchemaCompatible(EventEntityDescriptor contract, Uri candidate)
    {
        if (candidate == contract.DataSchema || contract.CompatibleSchemas?.Contains(candidate) == true) return true;
        if (contract.Compatibility == ContractCompatibility.Exact || !TryVersion(candidate, out var candidateVersion)) return false;
        return contract.Compatibility switch
        {
            ContractCompatibility.SameMajor => candidateVersion.Major == contract.Version.Major,
            ContractCompatibility.BackwardCompatible => candidateVersion.Major == contract.Version.Major && candidateVersion <= contract.Version,
            _ => false
        };
    }

    private static bool TryVersion(Uri uri, out Version version)
    {
        var segments = uri.OriginalString.Split(['/', ':', '@'], StringSplitOptions.RemoveEmptyEntries);
        foreach (var segment in segments.Reverse())
            if (Version.TryParse(segment.TrimStart('v', 'V'), out version!)) return true;
        version = null!;
        return false;
    }

    private static void FindForbidden(JsonElement value, string path, IReadOnlySet<string> forbidden, List<ValidationError> errors)
    {
        if (value.ValueKind == JsonValueKind.Object)
            foreach (var property in value.EnumerateObject())
            {
                if (forbidden.Contains(property.Name)) errors.Add(new($"{path}.{property.Name}", "Field is forbidden by the Event Entity policy."));
                FindForbidden(property.Value, $"{path}.{property.Name}", forbidden, errors);
            }
        else if (value.ValueKind == JsonValueKind.Array)
        {
            var index = 0;
            foreach (var item in value.EnumerateArray()) FindForbidden(item, $"{path}[{index++}]", forbidden, errors);
        }
    }
}
