using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using TestGenerator.Code;
using TestGenerator.Linking;
using TestGenerator.Naming;

namespace TestGenerator.Generation;

public static class TestPlanBuilder
{
    public static IReadOnlyList<EntityTestPlan> Build(IReadOnlyList<CodeType> code, IReadOnlyList<TestIntent> intents)
    {
        var plans = new Dictionary<CodeType, EntityTestPlan>();
        foreach (var type in code.OrderBy(t => t.Name))
        {
            plans[type] = CreatePlan(type);
        }

        foreach (var intent in intents)
        {
            if (intent.Target == null || !plans.TryGetValue(intent.Target, out var plan))
            {
                continue;
            }

            if (intent.Kind == IntentKind.MemberExists && intent.Status == MatchStatus.AnalysisOnly && intent.AnalysisAttribute != null)
            {
                plan.MissingMembers.Add(new MissingMemberPlan(intent, NameMatcher.ToPascalCase(intent.AnalysisAttribute)));
            }
            else if (intent.Kind == IntentKind.StringConstraint && intent.CodeProperty != null)
            {
                plan.StringTests.Add(CreateStringTest(plan, intent));
            }
        }

        return plans.Values.ToList();
    }

    private static EntityTestPlan CreatePlan(CodeType type)
    {
        var plan = new EntityTestPlan(type);
        plan.Constructor = type.Symbol.InstanceConstructors
            .Where(c => c.Parameters.Length > 0 && IsAccessible(c))
            .OrderByDescending(c => c.Parameters.Length)
            .FirstOrDefault();

        if (plan.Constructor != null)
        {
            foreach (var parameter in plan.Constructor.Parameters)
            {
                var property = type.Properties.FirstOrDefault(p => string.Equals(p.Name, parameter.Name, StringComparison.OrdinalIgnoreCase));
                plan.Parameters.Add(new ParameterPlan(parameter, property?.Name ?? CSharp.Pascal(parameter.Name), property));
            }
        }
        return plan;
    }

    private static StringTestPlan CreateStringTest(EntityTestPlan plan, TestIntent intent)
    {
        var propertyName = intent.CodeProperty!;
        var test = new StringTestPlan(intent, propertyName);
        var type = plan.Type;
        var rule = type.StringRules
            .Where(r => r.PropertyName == propertyName)
            .OrderByDescending(r => r.MaxLength.HasValue)
            .ThenByDescending(r => r.Required)
            .FirstOrDefault();

        type.Patterns.TryGetValue(propertyName, out var pattern);
        test.Shape = StringShape.FromPattern(pattern);

        var fromAnalysis = intent.AnalysisAttribute != null;
        test.MaxLength = (fromAnalysis ? intent.MaxLength : null) ?? rule?.MaxLength;
        test.Required = intent.Required ?? rule?.Required ?? false;
        if (fromAnalysis && !test.Required && rule is { Required: true, InConstructor: false })
        {
            test.Required = true;
        }

        test.ExceptionType = rule == null ? "System.Exception"
            : rule.IsAbpCheck ? "System.ArgumentException"
            : rule.IsCustomCheck ? "Volo.Abp.UserFriendlyException"
            : "System.Exception";
        test.MessageContains = rule?.ParameterName ?? "";

        test.ConstructorParameter = plan.Parameters.FirstOrDefault(p => p.Property?.Name == propertyName);
        if (test.ConstructorParameter != null)
        {
            test.Route = TestRoute.Constructor;
        }
        else if (rule?.Method is { IsStatic: false, MethodKind: MethodKind.Ordinary } method &&
                 method.Parameters.Length == 1 &&
                 method.Parameters[0].Type.SpecialType == SpecialType.System_String &&
                 IsAccessible(method) &&
                 plan.Constructor != null)
        {
            test.Route = TestRoute.Method;
            test.Method = method;
        }

        if (intent.Status == MatchStatus.Ambiguous)
        {
            test.SkipReason = "Ambiguous: " + intent.Message;
        }
        else if (!test.Shape.IsKnown)
        {
            test.SkipReason = $"Neznámý formát hodnoty (regex {pattern}) – nelze sestavit platnou hodnotu.";
        }
        else if (test.Route == TestRoute.None)
        {
            test.SkipReason = "Vlastnost nelze nastavit konstruktorem ani jednoparametrovou metodou.";
        }
        return test;
    }

    private static bool IsAccessible(ISymbol symbol) =>
        symbol.DeclaredAccessibility is Accessibility.Public or Accessibility.Internal or Accessibility.ProtectedOrInternal;
}
