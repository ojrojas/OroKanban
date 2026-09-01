using BuildingBlocks.Kernel.Domain.ValueObjects;

namespace AiProcessing.Domain.ValueObjects;

public sealed class ModelDescriptor : ValueObject
{
    public string Provider { get; }
    public string ModelName { get; }
    public string Version { get; }

    public ModelDescriptor(string provider, string modelName, string version)
    {
        if (string.IsNullOrWhiteSpace(provider) || provider.Length > 50) throw new ArgumentException("Provider 1..50");
        if (string.IsNullOrWhiteSpace(modelName) || modelName.Length > 200) throw new ArgumentException("ModelName 1..200");
        if (string.IsNullOrWhiteSpace(version) || version.Length > 100) throw new ArgumentException("Version 1..100");
        Provider = provider;
        ModelName = modelName;
        Version = version;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Provider;
        yield return ModelName;
        yield return Version;
    }
}
