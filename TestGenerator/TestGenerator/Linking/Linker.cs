using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using TestGenerator.Code;
using TestGenerator.Model;
using TestGenerator.Naming;

namespace TestGenerator.Linking;

public static class Linker
{
    public static IReadOnlyList<TestIntent> Link(AnalysisModel analysis, IReadOnlyList<CodeType> code)
    {
        var intents = new List<TestIntent>();
        var linkedProperties = new HashSet<(CodeType, string)>();

        foreach (var analysisClass in analysis.Classes.Where(c => !c.External))
        {
            var classKey = Key(analysisClass.Name);
            var candidates = NameMatcher.BestMatches(analysisClass.Name, code, t => t.Name);

            if (candidates.Count == 0)
            {
                intents.Add(new TestIntent($"{classKey}.Type", IntentKind.TypeExists, MatchStatus.AnalysisOnly,
                    $"Třída '{analysisClass.Name}' z analýzy nemá v kódu doménový typ.", Location.None)
                {
                    AnalysisClass = analysisClass.Name
                });
                continue;
            }

            if (candidates.Count > 1)
            {
                intents.Add(new TestIntent($"{classKey}.Type", IntentKind.TypeExists, MatchStatus.Ambiguous,
                    $"Třída '{analysisClass.Name}' odpovídá více typům: {string.Join(", ", candidates.Select(c => c.Name))}.",
                    candidates[0].Location)
                {
                    AnalysisClass = analysisClass.Name
                });
                continue;
            }

            var codeType = candidates[0];
            foreach (var attribute in analysisClass.Attributes)
            {
                var intent = LinkAttribute(analysisClass, attribute, codeType, linkedProperties);
                if (intent != null)
                {
                    intents.Add(intent);
                }
            }
        }

        foreach (var codeType in code)
        {
            foreach (var rule in codeType.StringRules)
            {
                if (linkedProperties.Contains((codeType, rule.PropertyName)))
                {
                    continue;
                }
                intents.Add(new TestIntent($"{codeType.Name}.{rule.PropertyName}.String", IntentKind.StringConstraint, MatchStatus.CodeOnly,
                    $"{codeType.Name}.{rule.PropertyName}: {Describe(rule.Required, rule.MaxLength)} ({rule.Source}) – analýza pravidlo neuvádí.",
                    rule.Location)
                {
                    CodeType = codeType.Name,
                    CodeProperty = rule.PropertyName,
                    Target = codeType,
                    Required = rule.Required,
                    MaxLength = rule.MaxLength
                });
            }
        }

        return intents;
    }

    private static TestIntent? LinkAttribute(AnalysisClass analysisClass, AnalysisAttribute attribute, CodeType codeType,
        HashSet<(CodeType, string)> linkedProperties)
    {
        var id = $"{Key(analysisClass.Name)}.{Key(attribute.Name)}";
        var properties = NameMatcher.BestMatches(attribute.Name, codeType.Properties, p => p.Name);

        if (properties.Count == 0)
        {
            return new TestIntent($"{id}.Member", IntentKind.MemberExists, MatchStatus.AnalysisOnly,
                $"{codeType.Name}: atribut '{attribute.Name}' ({attribute.Type}) z analýzy v kódu chybí.", codeType.Location)
            {
                AnalysisClass = analysisClass.Name,
                AnalysisAttribute = attribute.Name,
                CodeType = codeType.Name,
                Target = codeType
            };
        }

        if (properties.Count > 1)
        {
            return new TestIntent($"{id}.Member", IntentKind.MemberExists, MatchStatus.Ambiguous,
                $"{codeType.Name}: atribut '{attribute.Name}' odpovídá více vlastnostem: {string.Join(", ", properties.Select(p => p.Name))}.",
                codeType.Location)
            {
                AnalysisClass = analysisClass.Name,
                AnalysisAttribute = attribute.Name,
                CodeType = codeType.Name
            };
        }

        var property = properties[0];
        linkedProperties.Add((codeType, property.Name));

        if (!attribute.IsString || attribute.Derived)
        {
            return null;
        }

        var rules = codeType.StringRules.Where(r => r.PropertyName == property.Name).ToList();
        var expected = Describe(attribute.IsRequired, attribute.Length);
        var location = property.Locations.Length > 0 ? property.Locations[0] : codeType.Location;

        if (rules.Count == 0)
        {
            if (!attribute.IsRequired && attribute.Length == null)
            {
                return null;
            }

            var status = codeType.CustomGuardedProperties.Contains(property.Name) ? MatchStatus.Ambiguous : MatchStatus.AnalysisOnly;
            var reason = status == MatchStatus.Ambiguous
                ? "kód má vlastní validaci, kterou nelze deterministicky porovnat"
                : "kód hodnotu nevaliduje";
            return Create($"{id}.String", status, $"{codeType.Name}.{property.Name}: analýza {expected}, {reason}.", location,
                analysisClass, attribute, codeType, property.Name);
        }

        var rule = rules.OrderByDescending(r => r.MaxLength.HasValue).ThenByDescending(r => r.Required).First();
        var differences = new List<string>();
        var requiredOnlyForOperation = !attribute.IsRequired && rule.Required && !rule.InConstructor;
        if (rule.Required != attribute.IsRequired && !requiredOnlyForOperation)
        {
            differences.Add(attribute.IsRequired ? "analýza povinné, kód nepovinné" : "analýza nepovinné, kód povinné");
        }

        var codeOnlyLength = false;
        if (attribute.Length != rule.MaxLength)
        {
            if (attribute.Length == null)
            {
                codeOnlyLength = true;
            }
            else
            {
                differences.Add($"max. délka analýza {attribute.Length}, kód {(rule.MaxLength?.ToString() ?? "neomezeno")}");
            }
        }

        if (differences.Count > 0)
        {
            return Create($"{id}.String", MatchStatus.Conflict,
                $"{codeType.Name}.{property.Name}: {string.Join("; ", differences)}.", rule.Location,
                analysisClass, attribute, codeType, property.Name);
        }

        if (codeOnlyLength)
        {
            return Create($"{id}.String", MatchStatus.CodeOnly,
                $"{codeType.Name}.{property.Name}: kód omezuje délku na {rule.MaxLength}, analýza délku neuvádí.", rule.Location,
                analysisClass, attribute, codeType, property.Name);
        }

        return Create($"{id}.String", MatchStatus.Confirmed,
            $"{codeType.Name}.{property.Name}: {expected} ({rule.Source}).", rule.Location,
            analysisClass, attribute, codeType, property.Name);
    }

    private static TestIntent Create(string id, MatchStatus status, string message, Location location,
        AnalysisClass analysisClass, AnalysisAttribute attribute, CodeType codeType, string propertyName)
    {
        return new TestIntent(id, IntentKind.StringConstraint, status, message, location)
        {
            AnalysisClass = analysisClass.Name,
            AnalysisAttribute = attribute.Name,
            CodeType = codeType.Name,
            CodeProperty = propertyName,
            Target = codeType,
            Required = attribute.IsRequired,
            MaxLength = attribute.Length
        };
    }

    private static string Describe(bool required, int? maxLength)
    {
        var text = required ? "povinné" : "nepovinné";
        return maxLength.HasValue ? $"{text}, max {maxLength}" : text;
    }

    private static string Key(string analysisName) => NameMatcher.ToPascalCase(analysisName);
}
