using System.Text.Json.Serialization;

namespace Zydis.Generator.Core.Definitions;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(AnnotationName), typeDiscriminator: "name")]
[JsonDerivedType(typeof(AnnotationSwappable), typeDiscriminator: "swappable")]
[JsonDerivedType(typeof(AnnotationApxCc), typeDiscriminator: "apx_cc")]
[JsonDerivedType(typeof(AnnotationDecodeOnly), typeDiscriminator: "decode_only")]
public abstract class Annotation
{

}

public class AnnotationName : Annotation
{
    public required string Name { get; set; }
}

public class AnnotationSwappable : Annotation
{
    public required string Next { get; set; }
}

public class AnnotationApxCc : Annotation
{

}

/// <summary>
/// The encoder never uses a definition that carries this annotation.
/// </summary>
public class AnnotationDecodeOnly : Annotation
{

}
