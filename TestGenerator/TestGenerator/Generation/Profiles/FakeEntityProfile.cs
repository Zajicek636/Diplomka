using System.Collections.Generic;
using System.Linq;

namespace TestGenerator.Generation.Profiles;

public sealed class FakeEntityProfile : ITestProfile
{
    public string Name => "fakeentity";
    public string Description => "TestHelper: IFakeEntity, RandomGenerator, PropertyTester, ExtendedShould.";

    public IReadOnlyList<GeneratedFile> Generate(IReadOnlyList<EntityTestPlan> plans, GenerationOptions options)
    {
        var values = new ValueFactory(plans, random: true);
        var ns = options.RootNamespace + ".FakeEntity";
        var domainUsings = ValueFactory.Namespaces(plans).ToList();
        var files = new List<GeneratedFile>();
        var constructible = plans.Where(p => p.Constructor != null).ToList();

        foreach (var plan in constructible)
        {
            files.Add(new GeneratedFile($"FakeEntity/FakeEntities/Fake{plan.Name}.cs", FakeClass(plan, ns, options, domainUsings)));
        }

        files.Add(new GeneratedFile("FakeEntity/Generators/RandomFakeEntityGenerator.cs",
            RandomGenerator(constructible, values, ns, options, domainUsings)));

        foreach (var plan in plans.Where(p => p.Constructor != null || p.MissingMembers.Count > 0))
        {
            files.Add(new GeneratedFile($"FakeEntity/{plan.Name}_Tests.cs", Tests(plan, ns, options, domainUsings)));
        }
        return files;
    }

    private static string FakeClass(EntityTestPlan plan, string ns, GenerationOptions options, IEnumerable<string> domainUsings)
    {
        var w = new CodeWriter();
        ProfileHelpers.Header(w, options, new[] { "System", "System.Text.Json", "TestHelper.FakeEntities", "TestHelper.Utils" }.Concat(domainUsings));
        w.Line($"namespace {ns}.FakeEntities;");
        w.Line();
        w.Open($"public class Fake{plan.Name} : IFakeEntity<{plan.Name}>");

        foreach (var test in plan.StringTests.Where(t => t.MaxLength.HasValue))
        {
            w.Line($"public const int {test.PropertyName}MaxLength = {test.MaxLength};");
        }
        foreach (var p in plan.Parameters)
        {
            w.Line($"public {CSharp.TypeName(p.Type)} {p.FieldName} = default!;");
        }
        w.Line();
        w.Line("public string SerializeToJson() => JsonSerializer.Serialize(this, JsonConfig.DefaultJsonSerializerOptions);");
        w.Line();
        var args = string.Join(", ", plan.Parameters.Select(p => p.FieldName));
        w.Line($"public {plan.Name} CreateOriginalEntity(bool checkIfNotThrow = true) =>");
        w.Line($"    this.DoActionWithNotThrowCheck(() => new {plan.Name}({args}), checkIfNotThrow);");
        w.Line();
        w.Open($"public void CheckIfSame({plan.Name} entity)");
        w.Line($"var exShould = new ExtendedShould<{plan.Name}>(this);");
        foreach (var p in plan.Parameters.Where(p => p.ComparesToProperty))
        {
            w.Line($"exShould.ShouldBe(entity.{p.Property!.Name}, {p.FieldName});");
        }
        w.Close();
        w.Close();
        return w.ToString();
    }

    private static string RandomGenerator(IReadOnlyList<EntityTestPlan> plans, ValueFactory values, string ns,
        GenerationOptions options, IEnumerable<string> domainUsings)
    {
        var w = new CodeWriter();
        ProfileHelpers.Header(w, options, new[] { "System", "TestHelper.Generators", $"{ns}.FakeEntities" }.Concat(domainUsings));
        w.Line($"namespace {ns}.Generators;");
        w.Line();
        w.Open("public static partial class RandomFakeEntityGenerator");
        foreach (var plan in plans)
        {
            w.Open($"public static Fake{plan.Name} RandomFake{plan.Name}() => new()");
            foreach (var p in plan.Parameters)
            {
                w.Line($"{p.FieldName} = {values.For(plan, p)},");
            }
            w.Close(";");
            w.Line();
        }
        w.Close();
        return w.ToString();
    }

    private static string Tests(EntityTestPlan plan, string ns, GenerationOptions options, IEnumerable<string> domainUsings)
    {
        var w = new CodeWriter();
        ProfileHelpers.Header(w, options, new[]
        {
            "System", "Shouldly", "Xunit", "TestHelper.Utils", $"{ns}.FakeEntities", $"{ns}.Generators"
        }.Concat(domainUsings));
        w.Line($"namespace {ns};");
        w.Line();
        w.Open($"public class {plan.Name}_Tests");

        if (plan.Constructor != null)
        {
            w.Line($"private readonly Fake{plan.Name} _data = RandomFakeEntityGenerator.RandomFake{plan.Name}();");
            w.Line();
            w.Line("[Fact]");
            ProfileHelpers.CreateTraits(w, plan);
            w.Open($"public void Create_{plan.Name}()");
            w.Line("var entity = _data.CreateOriginalEntity();");
            w.Line("_data.CheckIfSame(entity);");
            w.Close();
            w.Line();

            foreach (var test in plan.StringTests)
            {
                WriteStringTest(w, plan, test);
            }
        }

        foreach (var member in plan.MissingMembers)
        {
            w.Line("[Fact]");
            ProfileHelpers.MissingMember(w, plan, member);
        }

        w.Close();
        return w.ToString();
    }

    private static void WriteStringTest(CodeWriter w, EntityTestPlan plan, StringTestPlan test)
    {
        var method = $"Test_{plan.Name}_{test.PropertyName}";
        if (test.SkipReason != null)
        {
            w.Line(ProfileHelpers.SkipAttribute("Fact", test.SkipReason));
            ProfileHelpers.Traits(w, test.Intent);
            w.Open($"public void {method}()");
            w.Close();
            w.Line();
            return;
        }

        var usePropertyTester = test.Shape == StringShape.Plain && test.MaxLength.HasValue;
        w.Line("[Theory]");
        ProfileHelpers.Traits(w, test.Intent);
        if (usePropertyTester)
        {
            var nullable = test.Required ? "false" : "true";
            w.Line($"[MemberData(nameof(PropertyTester.GetStringPropertyTestData), [{nullable}, Fake{plan.Name}.{test.PropertyName}MaxLength], MemberType = typeof(PropertyTester))]");
            w.Open($"public void {method}(string value, bool shouldThrow)");
        }
        else
        {
            ProfileHelpers.InlineCases(w, test);
            w.Open($"public void {method}(int length, bool shouldThrow)");
            w.Line($"string value = {ProfileHelpers.ValueExpression(test)};");
        }

        var message = CSharp.Literal(test.MessageContains);
        if (test.Route == TestRoute.Constructor)
        {
            w.Line($"_data.{test.ConstructorParameter!.FieldName} = value;");
            w.Line($"_data.TestSetProperty<{plan.Name}, {test.ExceptionType}>(shouldThrow, {message});");
        }
        else
        {
            w.Line($"_data.TestSetProperty<{plan.Name}, {test.ExceptionType}>((data, _) =>");
            w.Line("{");
            w.Line("    var entity = data.CreateOriginalEntity();");
            w.Line($"    entity.{test.Method!.Name}(value);");
            w.Line("    return entity;");
            w.Line($"}}, shouldThrow, {message});");
        }
        w.Close();
        w.Line();
    }
}
