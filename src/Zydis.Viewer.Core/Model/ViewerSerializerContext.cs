using System.Text.Json;
using System.Text.Json.Serialization;

using Zydis.Viewer.Core.Tree;

namespace Zydis.Viewer.Core.Model;

/// <summary>
/// The source generated serializer of the documents the viewer hands to the web app. The browser build runs without
/// reflection based serialization.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    Converters = [typeof(ViewNodeKindConverter)])]
[JsonSerializable(typeof(ViewModel))]
[JsonSerializable(typeof(JsonElement))]
internal sealed partial class ViewerSerializerContext :
    JsonSerializerContext;

/// <summary>
/// Writes a <see cref="ViewNodeKind"/> as its camel case name, which for the one-word kind names is the lower-case
/// name the web app compares against.
/// </summary>
internal sealed class ViewNodeKindConverter() :
    JsonStringEnumConverter<ViewNodeKind>(JsonNamingPolicy.CamelCase);
