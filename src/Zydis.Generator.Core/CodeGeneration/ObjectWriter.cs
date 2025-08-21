using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;

using static Zydis.Generator.Core.CodeGeneration.ObjectDeclaration;

namespace Zydis.Generator.Core.CodeGeneration;

public sealed class ObjectWriter
{
    private struct Field
    {
        public string Value { get; set; }
        public bool IsConditional { get; set; }
    }

    private const int IndentSize = 4;

    private readonly ObjectDeclaration _declaration;
    private readonly int? _indent;
    private readonly Field[] _fields;

    private bool _isNextFieldConditional;

    private bool AllFieldsSet() => _fields.All(x => x.Value is not null);

    internal ObjectWriter(ObjectDeclaration declaration, int? indent)
    {
        ArgumentNullException.ThrowIfNull(declaration);

        _declaration = declaration;
        _fields = new Field[declaration.Count];
        _indent = indent;
    }
    public ObjectWriter CreateObjectWriter(ObjectDeclaration declaration)
    {
        ArgumentNullException.ThrowIfNull(declaration);
        return new ObjectWriter(declaration, _indent + IndentSize ?? null);
    }

    public ObjectWriter WriteExpression(string fieldName, string value)
    {
        SetField(fieldName, value);
        return this;
    }

    public ObjectWriter WriteExpression(int fieldIndex, string value)
    {
        return WriteExpression(fieldIndex.ToString(), value);
    }

    public ObjectWriter WriteExpression(string fieldName, [StringSyntax(StringSyntaxAttribute.CompositeFormat)] string format, params object?[] arg)
    {
        SetField(fieldName, string.Format(format, arg));
        return this;
    }

    public ObjectWriter WriteExpression(int fieldIndex, [StringSyntax(StringSyntaxAttribute.CompositeFormat)] string format, params object?[] arg)
    {
        return WriteExpression(fieldIndex.ToString(), format, arg);
    }

    public ObjectWriter WriteBool(string fieldName, bool value)
    {
        using var writer = new StringWriter();
        WriterExtensions.WriteBool(writer, value);
        SetField(fieldName, writer.ToString());
        return this;
    }

    public ObjectWriter WriteBool(int fieldIndex, bool value)
    {
        return WriteBool(fieldIndex.ToString(), value);
    }

    public ObjectWriter WriteInteger<TValue>(string fieldName, IBinaryInteger<TValue> value, int length = 0, bool hex = false)
        where TValue : struct, IBinaryInteger<TValue>
    {
        ArgumentNullException.ThrowIfNull(value);
        using var writer = new StringWriter();
        WriterExtensions.WriteInteger(writer, value, length, hex);
        SetField(fieldName, writer.ToString());
        return this;
    }

    public ObjectWriter WriteInteger<TValue>(int fieldIndex, IBinaryInteger<TValue> value, int length = 0, bool hex = false)
        where TValue : struct, IBinaryInteger<TValue>
    {
        return WriteInteger(fieldIndex.ToString(), value, length, hex);
    }

    public ObjectWriter WriteIntegerArray<TValue>(string fieldName, params IBinaryInteger<TValue>[] values)
        where TValue : struct, IBinaryInteger<TValue>
    {
        SetField(fieldName, string.Format("{{ {0} }}", string.Join(", ", values)));
        return this;
    }

    public ObjectWriter WriteIntegerArray<TValue>(int fieldIndex, params IBinaryInteger<TValue>[] values)
        where TValue : struct, IBinaryInteger<TValue>
    {
        return WriteIntegerArray(fieldIndex.ToString(), values);
    }

    public ObjectWriter WriteObject(string fieldName, ObjectWriter objectWriter)
    {
        ArgumentNullException.ThrowIfNull(objectWriter);
        SetField(fieldName, objectWriter.GetExpression());
        return this;
    }

    public ObjectWriter WriteObject(int fieldIndex, ObjectWriter objectWriter)
    {
        return WriteObject(fieldIndex.ToString(), objectWriter);
    }

    public ObjectWriter Conditional()
    {
        _isNextFieldConditional = true;
        return this;
    }

    private void SetField(string fieldName, string value)
    {
        _fields[_declaration.GetIndex(fieldName)] = new Field { Value = value, IsConditional = _isNextFieldConditional };
        _isNextFieldConditional = false;
    }

    public string GetExpression()
    {
        var initializer = _declaration.Initializer;
        var allSet = AllFieldsSet();
        if (initializer == InitializerType.Positional && !allSet)
        {
            throw new InvalidDataException("All values must be set when using positional initializers");
        }
        if (initializer == InitializerType.Auto)
        {
            initializer = allSet ? InitializerType.Positional : InitializerType.Designated;
        }
        var lastFieldIndex = _declaration.Count - 1;
        if (!allSet)
        {
            while (lastFieldIndex >= 0 && _fields[lastFieldIndex].Value == null)
            {
                --lastFieldIndex;
            }
            if (lastFieldIndex < 0)
            {
                throw new InvalidDataException("No fields set");
            }
        }

        var indentString = new string(' ', _indent ?? 0);
        var sb = new StringBuilder();
        sb.Append(_indent.HasValue ? "{" : "{ ");
        if (_indent.HasValue)
        {
            sb.AppendLine();
        }
        for (var i = 0; i < _declaration.Count; ++i)
        {
            if (_fields[i].Value == null)
            {
                continue;
            }
            sb.Append(indentString);
            if (_fields[i].IsConditional)
            {
                sb.Append("ZYDIS_NOTMIN(");
            }
            if (initializer == InitializerType.Designated)
            {
                sb.Append(_declaration.GetDesignatedInitializer(i));
            }
            sb.Append(_fields[i].Value);
            if (_fields[i].IsConditional)
            {
                sb.Append(')');
            }
            if (i < lastFieldIndex)
            {
                if (_fields[i + 1].IsConditional)
                {
                    if (!_indent.HasValue)
                    {
                        sb.Append(' ');
                    }
                }
                else
                {
                    sb.Append(_indent.HasValue ? "," : ", ");
                }
            }
            if (_indent.HasValue)
            {
                sb.AppendLine();
            }
        }
        if (_indent.HasValue)
        {
            sb.Append(indentString![..^IndentSize]);
            sb.Append('}');
        }
        else
        {
            sb.Append(" }");
        }

        return sb.ToString();
    }
}
