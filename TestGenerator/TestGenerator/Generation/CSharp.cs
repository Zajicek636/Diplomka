using System.Text;
using Microsoft.CodeAnalysis;

namespace TestGenerator.Generation;

public static class CSharp
{
    public static readonly SymbolDisplayFormat TypeFormat = SymbolDisplayFormat.MinimallyQualifiedFormat
        .WithMiscellaneousOptions(SymbolDisplayMiscellaneousOptions.UseSpecialTypes |
                                  SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

    public static string Literal(string value)
    {
        var sb = new StringBuilder("\"");
        foreach (var c in value)
        {
            switch (c)
            {
                case '\\': sb.Append("\\\\"); break;
                case '"': sb.Append("\\\""); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                default: sb.Append(c); break;
            }
        }
        return sb.Append('"').ToString();
    }

    public static string TypeName(ITypeSymbol type) => type.ToDisplayString(TypeFormat);

    public static string Pascal(string name) =>
        string.IsNullOrEmpty(name) ? name : char.ToUpperInvariant(name[0]) + name.Substring(1);

    public static string Camel(string name) =>
        string.IsNullOrEmpty(name) ? name : char.ToLowerInvariant(name[0]) + name.Substring(1);
}
