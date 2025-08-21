using System;
using System.Runtime.CompilerServices;

using Zydis.Generator.Core.Serialization;

namespace Zydis.Generator.Core.CodeGeneration;

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Class, AllowMultiple = true)]
internal sealed class EmittableAttribute : Attribute
{
    public int Order { get; internal init; }
    public string FieldName { get; internal init; }

    public EmittableAttribute(int order, [CallerMemberName] string fieldName = "<INVALID>")
    {
        Order = order;
        FieldName = fieldName.ToSnakeCase();
    }
}
