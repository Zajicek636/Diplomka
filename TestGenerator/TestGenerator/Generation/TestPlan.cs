using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using TestGenerator.Code;
using TestGenerator.Linking;

namespace TestGenerator.Generation;

public sealed class EntityTestPlan
{
    public EntityTestPlan(CodeType type)
    {
        Type = type;
    }

    public CodeType Type { get; }
    public string Name => Type.Name;
    public string Namespace => Type.Symbol.ContainingNamespace.ToDisplayString();
    public IMethodSymbol? Constructor { get; set; }
    public List<ParameterPlan> Parameters { get; } = new();
    public List<StringTestPlan> StringTests { get; } = new();
    public List<MissingMemberPlan> MissingMembers { get; } = new();
}

public sealed class ParameterPlan
{
    public ParameterPlan(IParameterSymbol parameter, string fieldName, IPropertySymbol? property)
    {
        Parameter = parameter;
        FieldName = fieldName;
        Property = property;
    }

    public IParameterSymbol Parameter { get; }
    public string FieldName { get; }
    public IPropertySymbol? Property { get; }
    public ITypeSymbol Type => Parameter.Type;
    public bool IsNullableReference => Parameter.Type.IsReferenceType && Parameter.NullableAnnotation == NullableAnnotation.Annotated;
    public bool ComparesToProperty => Property != null &&
        SymbolEqualityComparer.Default.Equals(Property.Type.WithNullableAnnotation(NullableAnnotation.None),
            Parameter.Type.WithNullableAnnotation(NullableAnnotation.None));
}

public enum TestRoute
{
    None,
    Constructor,
    Method
}

public sealed class StringTestPlan
{
    public StringTestPlan(TestIntent intent, string propertyName)
    {
        Intent = intent;
        PropertyName = propertyName;
    }

    public TestIntent Intent { get; }
    public string PropertyName { get; }
    public StringShape Shape { get; set; } = StringShape.Plain;
    public bool Required { get; set; }
    public int? MaxLength { get; set; }
    public string ExceptionType { get; set; } = "System.Exception";
    public string MessageContains { get; set; } = "";
    public TestRoute Route { get; set; }
    public ParameterPlan? ConstructorParameter { get; set; }
    public IMethodSymbol? Method { get; set; }
    public string? SkipReason { get; set; }

    public IEnumerable<(int Length, bool ShouldThrow)> Cases()
    {
        yield return (-1, Required);
        if (MaxLength.HasValue && Shape.SupportsLength)
        {
            yield return (MaxLength.Value, false);
            yield return (MaxLength.Value + 1, true);
        }
    }
}

public sealed class MissingMemberPlan
{
    public MissingMemberPlan(TestIntent intent, string expectedName)
    {
        Intent = intent;
        ExpectedName = expectedName;
    }

    public TestIntent Intent { get; }
    public string ExpectedName { get; }
}
