using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Numerics;

namespace Zydis.Generator.Core.CodeGeneration;

public sealed class InitializerListWriter
{
    private readonly TextWriter _writer;
    private readonly int? _indent;
    private readonly string? _indentString;

    private bool _isFirst = true;
    private bool _lastTokenIsExpression;
    private bool _isConditional;

    internal InitializerListWriter(TextWriter writer, int? indent)
    {
        ArgumentNullException.ThrowIfNull(writer);

        _writer = writer;
        _indent = indent;

        if (_indent.HasValue)
        {
            _indentString = new string(' ', _indent.Value);
        }
    }

    public static InitializerListWriter Create(TextWriter writer, bool indent = true)
    {
        ArgumentNullException.ThrowIfNull(writer);

        return new InitializerListWriter(writer, indent ? 4 : null);
    }

    public InitializerListWriter BeginList()
    {
        if (_indent.HasValue)
        {
            _writer.WriteLine('{');
        }
        else
        {
            _writer.Write("{ ");
        }

        return this;
    }

    public InitializerListWriter WriteExpression(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        EnsureDelimiter();

        _writer.Write(value);
        _lastTokenIsExpression = true;

        EnsureMacroParenthesis();

        return this;
    }

    public InitializerListWriter WriteExpression([StringSyntax(StringSyntaxAttribute.CompositeFormat)] string format, params object?[] arg)
    {
        ArgumentNullException.ThrowIfNull(format);

        EnsureDelimiter();

        _writer.Write(format, arg);
        _lastTokenIsExpression = true;

        EnsureMacroParenthesis();

        return this;
    }

    public InitializerListWriter WriteBool(bool value)
    {
        EnsureDelimiter();

        WriterExtensions.WriteBool(_writer, value);
        _lastTokenIsExpression = true;

        EnsureMacroParenthesis();

        return this;
    }

    public InitializerListWriter WriteChar(char value)
    {
        EnsureDelimiter();

        WriterExtensions.WriteChar(_writer, value);
        _lastTokenIsExpression = true;

        EnsureMacroParenthesis();

        return this;
    }

    public InitializerListWriter WriteString(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        EnsureDelimiter();

        WriterExtensions.WriteString(_writer, value);
        _lastTokenIsExpression = true;

        EnsureMacroParenthesis();

        return this;
    }

    public InitializerListWriter WriteInteger<TValue>(IBinaryInteger<TValue> value, int length = 0, bool hex = false)
        where TValue : struct, IBinaryInteger<TValue>
    {
        ArgumentNullException.ThrowIfNull(value);

        EnsureDelimiter();

        WriterExtensions.WriteInteger(_writer, value, length, hex);
        _lastTokenIsExpression = true;

        EnsureMacroParenthesis();

        return this;
    }

    public InitializerListWriter WriteNull()
    {
        EnsureDelimiter();

        WriterExtensions.WriteNull(_writer);
        _lastTokenIsExpression = true;

        EnsureMacroParenthesis();

        return this;
    }

    public InitializerListWriter WriteInitializerList(bool indent = true)
    {
        EnsureDelimiter();

        _lastTokenIsExpression = true;

        // TODO: EnsureMacroParenthesis();

        return new InitializerListWriter(_writer, !indent ? null : _indent + 4);
    }

    public InitializerListWriter WriteFieldDesignation(string identifier)
    {
        ArgumentNullException.ThrowIfNull(identifier);

        EnsureDelimiter();

        _writer.Write(".{0} = ", identifier);

        return this;
    }

    public InitializerListWriter WriteArrayDesignation(string expression)
    {
        ArgumentNullException.ThrowIfNull(expression);

        EnsureDelimiter();

        _writer.Write("[{0}] = ", expression);

        return this;
    }

    public InitializerListWriter WriteArrayDesignation(int index, int length = 0, bool hex = false)
    {
        EnsureDelimiter();

        _writer.Write('[');
        WriterExtensions.WriteInteger(_writer, index, length, hex);
        _writer.Write("] = ");

        return this;
    }

    public InitializerListWriter WriteInlineComment(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        EnsureDelimiter();

        _writer.Write("/* {0} */", value);

        return this;
    }

    public InitializerListWriter WriteInlineComment([StringSyntax(StringSyntaxAttribute.CompositeFormat)] string format, params object?[] arg)
    {
        ArgumentNullException.ThrowIfNull(format);

        EnsureDelimiter();

        _writer.Write("/* ");
        _writer.Write(format, arg);
        _writer.Write(" */");

        return this;
    }

    public InitializerListWriter Conditional()
    {
        var temp = _lastTokenIsExpression;

        if (_lastTokenIsExpression)
        {
            if (_indent.HasValue)
            {
                _writer.WriteLine();
                _writer.Write(_indentString);
            }
            else
            {
                _writer.Write(' ');
            }

            _lastTokenIsExpression = false;
        }

        _writer.Write("ZYDIS_NOTMIN(");

        if (temp)
        {
            _writer.Write("COMMA");
        }

        _isConditional = true;

        return this;
    }

    public void EndList()
    {
        EnsureMacroParenthesis();

        if (_indent.HasValue)
        {
            if (_lastTokenIsExpression)
            {
                _writer.WriteLine();
                _writer.Write(_indentString![..^4]);
            }
        }
        else
        {
            _writer.Write(' ');
        }

        _writer.Write('}');
    }

    private void EnsureDelimiter()
    {
        var requiresIndent = _lastTokenIsExpression || _isFirst;

        if (_lastTokenIsExpression)
        {
            _lastTokenIsExpression = false;

            if (_indent.HasValue)
            {
                _writer.WriteLine(',');
            }
            else
            {
                _writer.Write(", ");
            }
        }
        else
        {
            if (!requiresIndent)
            {
                _writer.Write(' ');
            }
        }

        if (!requiresIndent)
        {
            return;
        }

        _isFirst = false;

        if (_indent.HasValue)
        {
            _writer.Write(_indentString);
        }
    }

    private void EnsureMacroParenthesis()
    {
        if (!_isConditional)
        {
            return;
        }

        _writer.Write(')');
        _isConditional = false;
    }
}
