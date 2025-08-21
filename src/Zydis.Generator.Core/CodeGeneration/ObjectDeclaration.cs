using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Zydis.Generator.Core.CodeGeneration;

public abstract class ObjectDeclaration
{
    public enum InitializerType
    {
        Auto,
        Positional,
        Designated,
    }

    protected const InitializerType DefaultInitializer = InitializerType.Positional;

    protected FrozenDictionary<string, int>? Fields { get; init; }

    protected ReadOnlyCollection<string>? FieldNames { get; init; }

    public virtual int Count { get; init; }

    public InitializerType Initializer { get; init; }

    public virtual int GetIndex(string fieldName)
    {
        return Fields![fieldName];
    }

    public virtual string GetDesignatedInitializer(int fieldIndex)
    {
        return $".{FieldNames![fieldIndex]} = ";
    }
}

public class SimpleObjectDeclaration : ObjectDeclaration
{
    public SimpleObjectDeclaration(params string[] fields)
        : this(DefaultInitializer, fields)
    {

    }

    public SimpleObjectDeclaration(InitializerType initializer, params string[] fields)
    {
        ArgumentNullException.ThrowIfNull(fields);

        Initializer = initializer;
        FieldNames = new ReadOnlyCollection<string>(fields);
        Fields = Enumerable.Range(0, fields.Length).ToFrozenDictionary(x => fields[x]);
        Count = Fields.Count;
    }
}

public class ObjectDeclaration<T> : ObjectDeclaration
{
    public ObjectDeclaration()
        : this(DefaultInitializer)
    {

    }

    public ObjectDeclaration(InitializerType initializer)
    {
        Initializer = initializer;
        var attributes = GetEmittableAttributes(typeof(T));
        FieldNames = attributes.Select(x => x.FieldName).ToList().AsReadOnly();
        Fields = attributes.ToFrozenDictionary(x => x.FieldName, x => x.Order);
        Count = Fields.Count;
    }

    private static IEnumerable<EmittableAttribute> GetEmittableAttributes(Type type)
    {
        var attributes = type
            .GetMembers(BindingFlags.Instance | BindingFlags.Public)
            .Select(x => x.GetCustomAttribute<EmittableAttribute>(true))
            .Where(x => x != null)
            .Concat(type.GetCustomAttributes<EmittableAttribute>(true))
            .OrderBy(x => x!.Order)
            .Cast<EmittableAttribute>();
        if (!Enumerable.Range(0, attributes.Count()).SequenceEqual(attributes.Select(x => x.Order)))
        {
            throw new InvalidDataException($"Invalid emittable attribute ordering for type {type.Name}");
        }
        return attributes;
    }

    public static List<string> GetFieldNames()
    {
        var attributes = GetEmittableAttributes(typeof(T));
        return [.. attributes.Select(x => x.FieldName)];
    }
}

public class ArrayObjectDeclaration : ObjectDeclaration
{
    public override int Count { get; init; }

    public ArrayObjectDeclaration(int arraySize)
        : this(DefaultInitializer, arraySize)
    {

    }

    public ArrayObjectDeclaration(InitializerType initializer, int arraySize)
    {
        Initializer = initializer;
        Count = arraySize;
    }

    public override int GetIndex(string fieldName)
    {
        return int.Parse(fieldName);
    }

    public override string GetDesignatedInitializer(int fieldIndex)
    {
        return $"[{fieldIndex}] = ";
    }
}
