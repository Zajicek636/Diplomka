using System.Collections.Generic;
using System.Linq;

namespace TestGenerator.Generation.Profiles;

public sealed class GenericXunitProfile : ITestProfile
{
    public string Name => "generic";
    public string Description => "Čistý xUnit + Shouldly, bez firemních knihoven.";

    public IReadOnlyList<GeneratedFile> Generate(IReadOnlyList<EntityTestPlan> plans, GenerationOptions options)
    {
        var values = new ValueFactory(plans, random: false);
        var ns = options.RootNamespace + ".Generic";
        var usings = new[] { "System", "Shouldly", "Xunit" }.Concat(ValueFactory.Namespaces(plans)).ToList();
        var files = new List<GeneratedFile>();

        foreach (var plan in plans.Where(p => p.Constructor != null || p.MissingMembers.Count > 0))
        {
            var w = new CodeWriter();
            ProfileHelpers.Header(w, options, usings);
            w.Line($"namespace {ns};");
            w.Line();
            w.Open($"public class {plan.Name}Tests");

            if (plan.Constructor != null)
            {
                WriteData(w, plan, values);
                WriteCreate(w, plan);
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
            files.Add(new GeneratedFile($"Generic/{plan.Name}Tests.cs", w.ToString()));
        }
        return files;
    }

    private static void WriteData(CodeWriter w, EntityTestPlan plan, ValueFactory values)
    {
        w.Open("private sealed class Data");
        foreach (var p in plan.Parameters)
        {
            w.Line($"public {CSharp.TypeName(p.Type)} {p.FieldName} = {values.For(plan, p)};");
        }
        w.Line();
        var args = string.Join(", ", plan.Parameters.Select(p => p.FieldName));
        w.Line($"public {plan.Name} Create() => new {plan.Name}({args});");
        w.Close();
        w.Line();
    }

    private static void WriteCreate(CodeWriter w, EntityTestPlan plan)
    {
        w.Line("[Fact]");
        ProfileHelpers.CreateTraits(w, plan);
        w.Open($"public void Create_{plan.Name}_WithValidData_SetsProperties()");
        w.Line("var data = new Data();");
        w.Line("var entity = data.Create();");
        foreach (var p in plan.Parameters.Where(p => p.ComparesToProperty))
        {
            w.Line($"entity.{p.Property!.Name}.ShouldBe(data.{p.FieldName});");
        }
        w.Close();
        w.Line();
    }

    private static void WriteStringTest(CodeWriter w, EntityTestPlan plan, StringTestPlan test)
    {
        var method = $"{test.PropertyName}_Validation";
        if (test.SkipReason != null)
        {
            w.Line(ProfileHelpers.SkipAttribute("Fact", test.SkipReason));
            ProfileHelpers.Traits(w, test.Intent);
            w.Open($"public void {method}()");
            w.Close();
            w.Line();
            return;
        }

        w.Line("[Theory]");
        ProfileHelpers.Traits(w, test.Intent);
        ProfileHelpers.InlineCases(w, test);
        w.Open($"public void {method}(int length, bool shouldThrow)");
        w.Line($"string value = {ProfileHelpers.ValueExpression(test)};");
        if (test.Route == TestRoute.Constructor)
        {
            w.Line($"var data = new Data {{ {test.ConstructorParameter!.FieldName} = value }};");
            w.Line("Action act = () => data.Create();");
        }
        else
        {
            w.Line("var entity = new Data().Create();");
            w.Line($"Action act = () => entity.{test.Method!.Name}(value);");
        }
        w.Open("if (shouldThrow)");
        w.Line($"var exception = Should.Throw<{test.ExceptionType}>(act, {CSharp.Literal(test.Intent.Message)});");
        if (test.MessageContains.Length > 0)
        {
            w.Line($"exception.Message.ShouldContain({CSharp.Literal(test.MessageContains)});");
        }
        w.Close();
        w.Open("else");
        w.Line($"Should.NotThrow(act, {CSharp.Literal(test.Intent.Message)});");
        w.Close();
        w.Close();
        w.Line();
    }
}
