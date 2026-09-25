using System.Text.RegularExpressions;

namespace TestGenerator.Generation;

public sealed class StringShape
{
    private const string EmailSuffix = "@example.com";

    private StringShape(string kind, char fill, int? fixedLength)
    {
        Kind = kind;
        Fill = fill;
        FixedLength = fixedLength;
    }

    public string Kind { get; }
    public char Fill { get; }
    public int? FixedLength { get; }
    public bool IsKnown => Kind != "Unknown";
    public bool SupportsLength => FixedLength == null && IsKnown;

    public static readonly StringShape Plain = new("Plain", 'A', null);
    public static readonly StringShape Email = new("Email", 'a', null);
    public static readonly StringShape Unknown = new("Unknown", 'A', null);

    public static StringShape FromPattern(string? pattern)
    {
        if (pattern == null)
        {
            return Plain;
        }
        if (pattern.Contains("@"))
        {
            return Email;
        }

        var digits = Regex.Match(pattern, @"^\^(?:\\d|\[0-9\])\{(\d+)\}\$$");
        if (digits.Success)
        {
            return new StringShape("Digits", '1', int.Parse(digits.Groups[1].Value));
        }

        var charClass = Regex.Match(pattern, @"^\^\[([^\]]+)\][+*]\$$");
        if (charClass.Success)
        {
            var content = charClass.Groups[1].Value;
            var fill = content.Contains("A-Z") ? 'A'
                : content.Contains("a-z") ? 'a'
                : content.Contains("0-9") ? '1'
                : content[0];
            return new StringShape("CharClass", fill, null);
        }

        return Unknown;
    }

    public string ValidExpression(int? maxLength)
    {
        if (FixedLength.HasValue)
        {
            return CSharp.Literal(new string(Fill, FixedLength.Value));
        }
        if (this == Email)
        {
            return CSharp.Literal("test" + EmailSuffix);
        }
        var length = maxLength.HasValue ? System.Math.Min(maxLength.Value, 5) : 5;
        return CSharp.Literal(new string(Fill, length));
    }

    public string OfLengthExpression(string lengthVariable)
    {
        if (this == Email)
        {
            return $"new string('{Fill}', {lengthVariable} - {EmailSuffix.Length}) + {CSharp.Literal(EmailSuffix)}";
        }
        return $"new string('{Fill}', {lengthVariable})";
    }
}
