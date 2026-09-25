using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace TestGenerator.Generation;

public sealed class ValueFactory
{
    private readonly Dictionary<INamedTypeSymbol, EntityTestPlan> _plans;
    private readonly bool _random;

    public ValueFactory(IEnumerable<EntityTestPlan> plans, bool random)
    {
        _plans = new Dictionary<INamedTypeSymbol, EntityTestPlan>(SymbolEqualityComparer.Default);
        foreach (var plan in plans)
        {
            _plans[plan.Type.Symbol] = plan;
        }
        _random = random;
    }

    public EntityTestPlan? PlanFor(ITypeSymbol type) =>
        type is INamedTypeSymbol named && _plans.TryGetValue(named, out var plan) ? plan : null;

    public string For(EntityTestPlan owner, ParameterPlan parameter, int depth = 0)
    {
        var type = parameter.Type;
        if (parameter.IsNullableReference)
        {
            return "null";
        }

        if (type.SpecialType == SpecialType.System_String)
        {
            var property = parameter.Property?.Name ?? parameter.FieldName;
            owner.Type.Patterns.TryGetValue(property, out var pattern);
            var shape = StringShape.FromPattern(pattern);
            var maxLength = owner.StringTests.FirstOrDefault(t => t.PropertyName == property)?.MaxLength
                            ?? owner.Type.StringRules.Where(r => r.PropertyName == property).Select(r => r.MaxLength).FirstOrDefault();
            if (_random && shape == StringShape.Plain)
            {
                return maxLength.HasValue
                    ? $"RandomGenerator.RandomAlpNum(1, {System.Math.Max(2, maxLength.Value)})"
                    : "RandomGenerator.RandomAlpNum(10)";
            }
            return shape.ValidExpression(maxLength);
        }

        return ForType(type, depth);
    }

    public string ForType(ITypeSymbol type, int depth)
    {
        if (type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable)
        {
            return ForType(nullable.TypeArguments[0], depth);
        }

        switch (type.SpecialType)
        {
            case SpecialType.System_Boolean: return "true";
            case SpecialType.System_Byte:
            case SpecialType.System_Int16:
            case SpecialType.System_Int32:
            case SpecialType.System_Int64:
                return _random ? "RandomGenerator.RandomNumber(1, 100)" : "1";
            case SpecialType.System_Decimal: return _random ? "RandomGenerator.RandomNumber(1, 100)" : "1m";
            case SpecialType.System_Double: return "1d";
            case SpecialType.System_Single: return "1f";
            case SpecialType.System_DateTime: return _random ? "System.DateTime.Today" : "new System.DateTime(2025, 1, 1)";
            case SpecialType.System_String: return CSharp.Literal("A");
        }

        if (type.Name == "Guid" && type.ContainingNamespace?.ToDisplayString() == "System")
        {
            return _random ? "System.Guid.NewGuid()" : "System.Guid.Parse(\"00000000-0000-0000-0000-000000000001\")";
        }

        if (type.TypeKind == TypeKind.Enum)
        {
            if (_random)
            {
                return $"RandomGenerator.RandomFromEnum<{CSharp.TypeName(type)}>()";
            }
            var first = type.GetMembers().OfType<IFieldSymbol>().FirstOrDefault(f => f.HasConstantValue);
            return first != null ? $"{CSharp.TypeName(type)}.{first.Name}" : $"default({CSharp.TypeName(type)})";
        }

        var plan = PlanFor(type);
        if (plan?.Constructor != null && depth < 3)
        {
            if (_random)
            {
                return $"RandomFakeEntityGenerator.RandomFake{plan.Name}().CreateOriginalEntity()";
            }
            var args = plan.Parameters.Select(p => For(plan, p, depth + 1));
            return $"new {plan.Name}({string.Join(", ", args)})";
        }

        return type.IsReferenceType ? "null!" : $"default({CSharp.TypeName(type)})";
    }

    public static IEnumerable<string> Namespaces(IEnumerable<EntityTestPlan> plans)
    {
        var result = new SortedSet<string>();
        foreach (var plan in plans)
        {
            result.Add(plan.Namespace);
            foreach (var parameter in plan.Parameters)
            {
                AddNamespace(result, parameter.Type);
            }
        }
        return result;
    }

    private static void AddNamespace(ISet<string> result, ITypeSymbol type)
    {
        if (type is INamedTypeSymbol named)
        {
            foreach (var argument in named.TypeArguments)
            {
                AddNamespace(result, argument);
            }
        }
        var ns = type.ContainingNamespace;
        if (ns != null && !ns.IsGlobalNamespace && type.SpecialType == SpecialType.None)
        {
            result.Add(ns.ToDisplayString());
        }
    }
}
