using System;
using System.Text.Json.Serialization;

namespace Zydis.Generator.Core.Serialization;

internal sealed class SnakeCaseStringEnumConverter<T>() :
    JsonStringEnumConverter<T>(new SnakeCaseNamingPolicy())
    where T : struct, Enum;
