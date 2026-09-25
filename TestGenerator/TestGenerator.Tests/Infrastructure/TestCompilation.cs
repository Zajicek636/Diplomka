using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace TestGenerator.Tests.Infrastructure;

public static class TestCompilation
{
    public const string AbpStubs = """
        namespace Volo.Abp
        {
            public static class Check
            {
                public static string NotNullOrWhiteSpace(string? value, string parameterName, int maxLength = int.MaxValue, int minLength = 0) => value!;
                public static string? Length(string? value, string parameterName, int maxLength, int minLength = 0) => value;
                public static T NotNull<T>(T value, string parameterName) => value;
            }

            public class BusinessException : System.Exception
            {
                public BusinessException(string code) : base(code) { }
            }
        }

        namespace Volo.Abp.Domain.Entities
        {
            public abstract class Entity<TKey>
            {
                public TKey Id { get; protected set; } = default!;
                protected Entity() { }
                protected Entity(TKey id) { Id = id; }
            }

            public abstract class AggregateRoot<TKey> : Entity<TKey>
            {
                protected AggregateRoot() { }
                protected AggregateRoot(TKey id) : base(id) { }
            }
        }

        namespace Volo.Abp.Domain.Values
        {
            public abstract class ValueObject { }
        }
        """;

    public static Compilation Create(params string[] sources)
    {
        var trees = new[] { AbpStubs }.Concat(sources).Select(s => CSharpSyntaxTree.ParseText(s)).ToList();
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Select(p => MetadataReference.CreateFromFile(p));

        var compilation = CSharpCompilation.Create("TestAssembly", trees, references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

        var errors = compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, errors));
        }
        return compilation;
    }
}
