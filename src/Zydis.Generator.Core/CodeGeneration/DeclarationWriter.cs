using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Numerics;

namespace Zydis.Generator.Core.CodeGeneration;

public sealed class DeclarationWriter
{
    private readonly TextWriter _writer;
    private readonly bool _indented;

    internal DeclarationWriter(TextWriter writer, bool indented)
    {
        ArgumentNullException.ThrowIfNull(writer);

        _writer = writer;
        _indented = indented;
    }

    public static DeclarationWriter Create(TextWriter writer, bool indented = true)
    {
        ArgumentNullException.ThrowIfNull(writer);

        return new DeclarationWriter(writer, indented);
    }

    public DeclarationWriter BeginDeclaration(string type, string identifier)
    {
        _writer.Write("{0} {1}", type, identifier);

        return this;
    }

    public DeclarationWriter BeginDeclaration(string modifier, string type, string identifier)
    {
        _writer.Write("{0} {1} {2}", modifier, type, identifier);

        return this;
    }

    public DeclarationWriter EndDeclaration()
    {
        _writer.Write(';');

        return this;
    }

    public DeclarationWriter WriteInitializerExpression(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        _writer.Write(" = ");
        _writer.Write(value);

        return this;
    }

    public DeclarationWriter WriteInitializerExpression([StringSyntax(StringSyntaxAttribute.CompositeFormat)] string format, params object?[] arg)
    {
        ArgumentNullException.ThrowIfNull(format);

        _writer.Write(" = ");
        _writer.Write(format, arg);

        return this;
    }

    public DeclarationWriter WriteInitializerValue(bool value)
    {
        _writer.Write(" = ");
        WriterExtensions.WriteBool(_writer, value);

        return this;
    }

    public DeclarationWriter WriteInitializerValue(char value)
    {
        _writer.Write(" = ");
        WriterExtensions.WriteChar(_writer, value);

        return this;
    }

    public DeclarationWriter WriteInitializerValue(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        _writer.Write(" = ");
        WriterExtensions.WriteString(_writer, value);

        return this;
    }

    public DeclarationWriter WriteInitializerValue<TValue>(IBinaryInteger<TValue> value, int length = 0, bool hex = false)
        where TValue : struct, IBinaryInteger<TValue>
    {
        ArgumentNullException.ThrowIfNull(value);

        _writer.Write(" = ");
        WriterExtensions.WriteInteger(_writer, value, length, hex);

        return this;
    }

    public InitializerListWriter WriteInitializerList(bool doNotIndent = false)
    {
        _writer.Write(" = ");

        if (_indented)
        {
            _writer.WriteLine();
        }

        return new InitializerListWriter(_writer, _indented && !doNotIndent ? 4 : null);
    }
}
