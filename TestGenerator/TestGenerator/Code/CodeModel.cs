using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace TestGenerator.Code;

public enum DomainKind
{
    Entity,
    AggregateRoot,
    ValueObject
}

public sealed class CodeType
{
    public CodeType(INamedTypeSymbol symbol, DomainKind kind)
    {
        Symbol = symbol;
        Kind = kind;
    }

    public INamedTypeSymbol Symbol { get; }
    public DomainKind Kind { get; }
    public string Name => Symbol.Name;
    public List<IPropertySymbol> Properties { get; } = new();
    public List<StringRule> StringRules { get; } = new();
    public HashSet<string> CustomGuardedProperties { get; } = new();
    public Dictionary<string, string> Patterns { get; } = new();
    public Location Location => Symbol.Locations.Length > 0 ? Symbol.Locations[0] : Location.None;
}

public sealed class StringRule
{
    public StringRule(string propertyName, bool required, int? maxLength, string source, Location location, bool inConstructor,
        IMethodSymbol? method = null, string? parameterName = null)
    {
        InConstructor = inConstructor;
        Method = method;
        ParameterName = parameterName;
        PropertyName = propertyName;
        Required = required;
        MaxLength = maxLength;
        Source = source;
        Location = location;
    }

    public string PropertyName { get; }
    public bool Required { get; }
    public int? MaxLength { get; }
    public string Source { get; }
    public Location Location { get; }
    public bool InConstructor { get; }
    public IMethodSymbol? Method { get; }
    public string? ParameterName { get; }
    public bool IsAbpCheck => Source.StartsWith("Check.");
    public bool IsCustomCheck => Source.StartsWith("CustomCheck.");
}
