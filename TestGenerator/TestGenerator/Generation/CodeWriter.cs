using System.Text;

namespace TestGenerator.Generation;

public sealed class CodeWriter
{
    private readonly StringBuilder _sb = new();
    private int _indent;

    public CodeWriter Line(string text = "")
    {
        if (text.Length > 0)
        {
            _sb.Append(' ', _indent * 4);
        }
        _sb.Append(text).Append("\r\n");
        return this;
    }

    public CodeWriter Open(string text)
    {
        Line(text);
        Line("{");
        _indent++;
        return this;
    }

    public CodeWriter Close(string suffix = "")
    {
        _indent--;
        Line("}" + suffix);
        return this;
    }

    public override string ToString() => _sb.ToString();
}
