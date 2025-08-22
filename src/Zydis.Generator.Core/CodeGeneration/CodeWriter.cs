using System.Diagnostics.CodeAnalysis;
using System.IO;

namespace Zydis.Generator.Core.CodeGeneration;

public sealed class CodeWriter
{
    private const int IndentSize = 4;

    private readonly TextWriter _writer;
    private string _indent;

    public CodeWriter(TextWriter writer)
    {
        _writer = writer;
        _indent = "";
    }

    public CodeWriter BeginBlock(bool indent = true)
    {
        _writer.Write(_indent);
        _writer.WriteLine('{');
        if (indent)
        {
            BeginIndent();
        }
        return this;
    }

    public CodeWriter EndBlock(bool semicolon = false, bool indent = true)
    {
        if (indent)
        {
            EndIndent();
        }
        _writer.Write(_indent);
        _writer.WriteLine(semicolon ? "};" : "}");
        return this;
    }

    public CodeWriter BeginIndent()
    {
        _indent = new string(' ', _indent.Length + IndentSize);
        return this;
    }

    public CodeWriter EndIndent()
    {
        _indent = new string(' ', _indent.Length - IndentSize);
        return this;
    }

    public CodeWriter Newline()
    {
        _writer.WriteLine();
        return this;
    }

    public CodeWriter WriteLine(string text)
    {
        _writer.Write(_indent);
        _writer.WriteLine(text);
        return this;
    }

    public CodeWriter WriteLine([StringSyntax(StringSyntaxAttribute.CompositeFormat)] string format, params object?[] arg)
    {
        _writer.Write(_indent);
        _writer.WriteLine(format, arg);
        return this;
    }
}
