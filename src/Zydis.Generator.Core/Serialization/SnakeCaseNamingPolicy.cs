using System.Text.Json;

namespace Zydis.Generator.Core.Serialization;

internal sealed class SnakeCaseNamingPolicy :
    JsonNamingPolicy
{
    public static readonly SnakeCaseNamingPolicy Instance = new();

    public override string ConvertName(string name)
    {
        return name.ToSnakeCase();
    }
}
