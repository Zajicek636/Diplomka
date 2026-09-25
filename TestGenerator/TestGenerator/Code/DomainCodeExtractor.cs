using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace TestGenerator.Code;

public static class DomainCodeExtractor
{
    private static readonly Dictionary<string, (string Method, bool Required)[]> StringCheckMethods = new()
    {
        ["Volo.Abp.Check"] = new[]
        {
            ("NotNullOrWhiteSpace", true),
            ("NotNullOrEmpty", true),
            ("NotNull", true),
            ("Length", false)
        },
        ["Dynatech.Abp.ValidationLibrary.ValidationFunctions.CustomCheck"] = new[]
        {
            ("NotNull", true),
            ("Length", false)
        }
    };

    public static IReadOnlyList<CodeType> Extract(Compilation compilation, CancellationToken cancellationToken = default)
    {
        var types = new Dictionary<INamedTypeSymbol, CodeType>(SymbolEqualityComparer.Default);

        foreach (var tree in compilation.SyntaxTrees)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var model = compilation.GetSemanticModel(tree);
            var root = tree.GetRoot(cancellationToken);

            foreach (var declaration in root.DescendantNodes().OfType<ClassDeclarationSyntax>())
            {
                if (model.GetDeclaredSymbol(declaration, cancellationToken) is not INamedTypeSymbol symbol)
                {
                    continue;
                }

                var kind = Classify(symbol);
                if (kind == null)
                {
                    continue;
                }

                if (!types.TryGetValue(symbol, out var codeType))
                {
                    codeType = new CodeType(symbol, kind.Value);
                    codeType.Properties.AddRange(CollectProperties(symbol));
                    types.Add(symbol, codeType);
                }

                CollectStringRules(declaration, model, codeType, cancellationToken);
                CollectCustomGuards(declaration, model, codeType, cancellationToken);
            }
        }

        return types.Values.ToList();
    }

    public static DomainKind? Classify(INamedTypeSymbol symbol)
    {
        if (symbol.IsAbstract || (symbol.ContainingNamespace?.ToDisplayString() ?? "").StartsWith("Volo.Abp"))
        {
            return null;
        }

        for (var current = symbol.BaseType; current != null; current = current.BaseType)
        {
            var ns = current.ContainingNamespace?.ToDisplayString();
            var name = current.OriginalDefinition.Name;
            if (ns == "Volo.Abp.Domain.Values" && name == "ValueObject")
            {
                return DomainKind.ValueObject;
            }
            if (ns == "Volo.Abp.Domain.Entities")
            {
                if (name == "AggregateRoot" || name == "BasicAggregateRoot")
                {
                    return DomainKind.AggregateRoot;
                }
                if (name == "Entity")
                {
                    return DomainKind.Entity;
                }
            }
        }
        return null;
    }

    private static IEnumerable<IPropertySymbol> CollectProperties(INamedTypeSymbol symbol)
    {
        var seen = new HashSet<string>();
        for (var current = symbol; current != null && current.SpecialType != SpecialType.System_Object; current = current.BaseType)
        {
            foreach (var property in current.GetMembers().OfType<IPropertySymbol>())
            {
                if (!property.IsStatic && !property.IsIndexer && seen.Add(property.Name))
                {
                    yield return property;
                }
            }
        }
    }

    private static void CollectStringRules(ClassDeclarationSyntax declaration, SemanticModel model, CodeType codeType,
        CancellationToken cancellationToken)
    {
        foreach (var invocation in declaration.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (model.GetSymbolInfo(invocation, cancellationToken).Symbol is not IMethodSymbol method)
            {
                continue;
            }

            var containingType = method.ContainingType?.ToDisplayString();
            if (containingType == null || !StringCheckMethods.TryGetValue(containingType, out var known))
            {
                continue;
            }

            var match = known.FirstOrDefault(k => k.Method == method.Name);
            if (match.Method == null)
            {
                continue;
            }

            var valueArgument = GetArgument(invocation, method, 0, "value");
            if (valueArgument == null ||
                model.GetTypeInfo(valueArgument.Expression, cancellationToken).Type?.SpecialType != SpecialType.System_String)
            {
                continue;
            }

            var propertyName = ResolvePropertyName(invocation, method, model, codeType, cancellationToken);
            if (propertyName == null)
            {
                continue;
            }

            int? maxLength = null;
            var maxLengthArgument = GetArgument(invocation, method, 2, "maxLength");
            if (maxLengthArgument != null)
            {
                var constant = model.GetConstantValue(maxLengthArgument.Expression, cancellationToken);
                if (constant.HasValue && constant.Value is int value && value != int.MaxValue)
                {
                    maxLength = value;
                }
            }

            var source = method.ContainingType!.Name + "." + method.Name;
            var inConstructor = invocation.FirstAncestorOrSelf<ConstructorDeclarationSyntax>() != null;
            var containingMethod = model.GetEnclosingSymbol(invocation.SpanStart, cancellationToken) as IMethodSymbol;
            string? parameterName = null;
            var parameterNameArgument = GetArgument(invocation, method, 1, "parameterName");
            if (parameterNameArgument != null &&
                model.GetConstantValue(parameterNameArgument.Expression, cancellationToken) is { HasValue: true, Value: string constantName })
            {
                parameterName = constantName;
            }
            var rule = new StringRule(propertyName, match.Required, maxLength, source, invocation.GetLocation(), inConstructor,
                containingMethod, parameterName);
            if (!codeType.StringRules.Any(r => r.PropertyName == rule.PropertyName && r.Required == rule.Required && r.MaxLength == rule.MaxLength))
            {
                codeType.StringRules.Add(rule);
            }
        }
    }

    private static ArgumentSyntax? GetArgument(InvocationExpressionSyntax invocation, IMethodSymbol method, int fallbackIndex, string parameterName)
    {
        var arguments = invocation.ArgumentList.Arguments;
        var named = arguments.FirstOrDefault(a => a.NameColon?.Name.Identifier.ValueText == parameterName);
        if (named != null)
        {
            return named;
        }

        var index = -1;
        for (var i = 0; i < method.Parameters.Length; i++)
        {
            if (method.Parameters[i].Name == parameterName)
            {
                index = i;
                break;
            }
        }
        if (index < 0)
        {
            index = fallbackIndex;
        }
        if (index < 0 || index >= arguments.Count || arguments[index].NameColon != null)
        {
            return null;
        }
        return arguments[index];
    }

    private static string? ResolvePropertyName(InvocationExpressionSyntax invocation, IMethodSymbol method, SemanticModel model,
        CodeType codeType, CancellationToken cancellationToken)
    {
        SyntaxNode node = invocation;
        while (node.Parent is ParenthesizedExpressionSyntax or CastExpressionSyntax)
        {
            node = node.Parent;
        }
        if (node.Parent is AssignmentExpressionSyntax assignment && assignment.Right == node &&
            model.GetSymbolInfo(assignment.Left, cancellationToken).Symbol is IPropertySymbol assigned)
        {
            return assigned.Name;
        }

        var nameArgument = GetArgument(invocation, method, 1, "parameterName");
        if (nameArgument != null)
        {
            var constant = model.GetConstantValue(nameArgument.Expression, cancellationToken);
            if (constant.HasValue && constant.Value is string name)
            {
                var property = codeType.Properties.FirstOrDefault(p => string.Equals(p.Name, name, System.StringComparison.OrdinalIgnoreCase));
                if (property != null)
                {
                    return property.Name;
                }
            }
        }

        var valueArgument = GetArgument(invocation, method, 0, "value");
        if (valueArgument != null && model.GetSymbolInfo(valueArgument.Expression, cancellationToken).Symbol is IParameterSymbol parameter)
        {
            return FindPropertyAssignedFrom(invocation, parameter, model, cancellationToken);
        }
        return null;
    }

    private static string? FindPropertyAssignedFrom(SyntaxNode origin, IParameterSymbol parameter, SemanticModel model, CancellationToken cancellationToken)
    {
        var body = origin.FirstAncestorOrSelf<BaseMethodDeclarationSyntax>();
        if (body == null)
        {
            return null;
        }
        foreach (var assignment in body.DescendantNodes().OfType<AssignmentExpressionSyntax>())
        {
            if (model.GetSymbolInfo(assignment.Right, cancellationToken).Symbol is IParameterSymbol source &&
                SymbolEqualityComparer.Default.Equals(source, parameter) &&
                model.GetSymbolInfo(assignment.Left, cancellationToken).Symbol is IPropertySymbol property)
            {
                return property.Name;
            }
        }
        return null;
    }

    private static void CollectPatterns(ExpressionSyntax condition, BaseMethodDeclarationSyntax member, SemanticModel model,
        CodeType codeType, CancellationToken cancellationToken)
    {
        foreach (var invocation in condition.DescendantNodesAndSelf().OfType<InvocationExpressionSyntax>())
        {
            if (model.GetSymbolInfo(invocation, cancellationToken).Symbol is not IMethodSymbol method ||
                method.Name != "IsMatch" ||
                method.ContainingType?.ToDisplayString() != "System.Text.RegularExpressions.Regex" ||
                invocation.ArgumentList.Arguments.Count < 2)
            {
                continue;
            }

            var pattern = model.GetConstantValue(invocation.ArgumentList.Arguments[1].Expression, cancellationToken);
            if (!pattern.HasValue || pattern.Value is not string patternText)
            {
                continue;
            }

            var input = model.GetSymbolInfo(invocation.ArgumentList.Arguments[0].Expression, cancellationToken).Symbol;
            var propertyName = input switch
            {
                IPropertySymbol property => property.Name,
                IParameterSymbol parameter => FindPropertyAssignedFrom(member, parameter, model, cancellationToken),
                _ => null
            };
            if (propertyName != null && !codeType.Patterns.ContainsKey(propertyName))
            {
                codeType.Patterns.Add(propertyName, patternText);
            }
        }
    }

    private static void CollectCustomGuards(ClassDeclarationSyntax declaration, SemanticModel model, CodeType codeType,
        CancellationToken cancellationToken)
    {
        foreach (var member in declaration.Members.OfType<BaseMethodDeclarationSyntax>())
        {
            var guardedSymbols = new List<ISymbol>();
            foreach (var ifStatement in member.DescendantNodes().OfType<IfStatementSyntax>())
            {
                var throws = ifStatement.Statement.DescendantNodesAndSelf()
                    .Any(n => n.IsKind(SyntaxKind.ThrowStatement) || n.IsKind(SyntaxKind.ThrowExpression));
                if (!throws)
                {
                    continue;
                }
                CollectPatterns(ifStatement.Condition, member, model, codeType, cancellationToken);
                foreach (var identifier in ifStatement.Condition.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>())
                {
                    var symbol = model.GetSymbolInfo(identifier, cancellationToken).Symbol;
                    if (symbol is IParameterSymbol or IPropertySymbol)
                    {
                        guardedSymbols.Add(symbol);
                    }
                }
            }

            foreach (var symbol in guardedSymbols)
            {
                if (symbol is IPropertySymbol property)
                {
                    codeType.CustomGuardedProperties.Add(property.Name);
                }
                else if (symbol is IParameterSymbol parameter)
                {
                    var assigned = FindPropertyAssignedFrom(member, parameter, model, cancellationToken);
                    if (assigned != null)
                    {
                        codeType.CustomGuardedProperties.Add(assigned);
                    }
                }
            }
        }
    }
}
