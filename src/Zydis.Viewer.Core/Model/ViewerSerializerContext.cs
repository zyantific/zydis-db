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
    Converters = [typeof(DiffStatusConverter), typeof(ViewNodeKindConverter)])]
[JsonSerializable(typeof(DiffModel))]
[JsonSerializable(typeof(JsonElement))]
[JsonSerializable(typeof(ViewModel))]
internal sealed partial class ViewerSerializerContext :
    JsonSerializerContext;

/// <summary>
/// Writes a <see cref="DiffStatus"/> as its camel case name, which for the one-word status names is the lower-case
/// name the web app compares against.
/// </summary>
internal sealed class DiffStatusConverter() :
    JsonStringEnumConverter<DiffStatus>(JsonNamingPolicy.CamelCase);

/// <summary>
/// Writes a <see cref="ViewNodeKind"/> as its camel case name, which for the one-word kind names is the lower-case
/// name the web app compares against.
/// </summary>
internal sealed class ViewNodeKindConverter() :
    JsonStringEnumConverter<ViewNodeKind>(JsonNamingPolicy.CamelCase);
