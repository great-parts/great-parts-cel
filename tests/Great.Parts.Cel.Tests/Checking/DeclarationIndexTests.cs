using System;
using System.Collections.Immutable;
using System.Linq;
using Great.Parts.Cel.Checking;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Types;
using Xunit;

namespace Great.Parts.Cel.Tests.Checking;

// The lookups of the declaration index: variables by name, function names, overloads by function name and
// argument count in the order of their declaration.
public sealed class DeclarationIndexTests
{
    private static readonly DeclarationIndex Index = DeclarationIndex.Create(
        [
            Overload("f", "f_int", [CelType.Int]),
            Overload("g", "g_int_int", [CelType.Int, CelType.Int]),
            Overload("f", "f_int_int", [CelType.Int, CelType.Int]),
            Overload("f", "f_string", [CelType.String]),
        ],
        [new CelVariableDecl("x", CelType.Int), new CelVariableDecl("y", CelType.String), new CelVariableDecl("x", CelType.Int)]);

    [Theory]
    [InlineData("f", 1, "f_int f_string")]
    [InlineData("f", 2, "f_int_int")]
    [InlineData("g", 2, "g_int_int")]
    public static void Overloads_AreGroupedByFunctionAndArgumentCountInTheOrderOfDeclaration(string function, int argumentCount, string overloadIds)
    {
        ArgumentNullException.ThrowIfNull(overloadIds);

        Assert.Equal(overloadIds.Split(' '), Index.GetOverloads(function, argumentCount).Select(overload => overload.Declaration.OverloadId));
    }

    [Theory]
    [InlineData("f", 0)]
    [InlineData("g", 1)]
    [InlineData("h", 1)]
    public static void Overloads_OfAnotherArgumentCountOrFunction_AreEmpty(string function, int argumentCount)
    {
        Assert.Empty(Index.GetOverloads(function, argumentCount));
    }

    [Theory]
    [InlineData("f", true)]
    [InlineData("g", true)]
    [InlineData("h", false)]
    [InlineData("x", false)]
    public static void FunctionNames_AreTheNamesOfTheOverloads(string name, bool isFunction)
    {
        Assert.Equal(isFunction, Index.IsFunction(name));
    }

    // The same declaration twice is one declaration.
    [Fact]
    public static void Variables_AreFoundByName()
    {
        Assert.True(Index.TryGetVariable("x", out CelVariableDecl? x));
        Assert.Equal(CelType.Int, x.Type);
        Assert.True(Index.TryGetVariable("y", out CelVariableDecl? y));
        Assert.Equal(CelType.String, y.Type);
        Assert.False(Index.TryGetVariable("f", out _));
    }

    private static OverloadBinding Overload(string function, string overloadId, ImmutableArray<CelType> parameterTypes) =>
        new(function, new CelOverloadDecl(overloadId, false, parameterTypes, CelType.Bool), null, 1, null, "test");
}
