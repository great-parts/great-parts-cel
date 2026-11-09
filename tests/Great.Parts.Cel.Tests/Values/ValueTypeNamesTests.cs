using System;
using System.Collections.Generic;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Values;

// ValueTypeNames: every kind of value has the name of its CEL type, as cel-go writes it in messages, or is
// not implemented yet.
public sealed class ValueTypeNamesTests
{
    private static readonly Dictionary<CelValueKind, string> Names = new()
    {
        [CelValueKind.Null] = "null_type",
        [CelValueKind.Bool] = "bool",
        [CelValueKind.Int] = "int",
        [CelValueKind.UInt] = "uint",
        [CelValueKind.Double] = "double",
        [CelValueKind.String] = "string",
        [CelValueKind.Bytes] = "bytes",
        [CelValueKind.List] = "list",
        [CelValueKind.Map] = "map",
        [CelValueKind.Type] = "type",
        [CelValueKind.Timestamp] = "timestamp",
        [CelValueKind.Duration] = "duration",
        [CelValueKind.Error] = "error",
    };

    public static TheoryData<CelValueKind> Kinds => [.. Enum.GetValues<CelValueKind>()];

    [Theory]
    [MemberData(nameof(Kinds))]
    public static void Of_GivesTheNameOfTheTypeOfTheKind(CelValueKind kind)
    {
        if (Names.TryGetValue(kind, out string? name))
        {
            Assert.Equal(name, ValueTypeNames.Of(kind));
        }
        else
        {
            Assert.Throws<NotImplementedException>(() => ValueTypeNames.Of(kind));
        }
    }
}
