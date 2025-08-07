using System.Text.Json.Serialization;

namespace Zydis.Generator.Core.Definitions;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(AnnotationName), typeDiscriminator: "name")]
[JsonDerivedType(typeof(AnnotationSwappable), typeDiscriminator: "swappable")]
[JsonDerivedType(typeof(AnnotationApxCc), typeDiscriminator: "apx_cc")]
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
