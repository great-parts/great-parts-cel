using System;
using System.Collections.Generic;
using System.Threading;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Evaluation;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Evaluation;

// Collection arguments of typed delegates: the CEL type of the parameter comes from its .NET type by the
// expected-type rows of the type mapping, and the argument is a lazy view when no element can fail its
// conversion and a copy, charged first, otherwise: a list of n elements 1 + n, a map of n pairs 1 + 2n and
// ⌈L/10⌉ for each string key of length L, a byte array of L bytes 1 + ⌈L/10⌉. The delegate form of AddFunction infers its
// declaration by the input rows, which have no IReadOnlyList<…> row, so those parameters are
// typed implementations of a declared overload first(list(int)) -> int.
public sealed class ApplicationArgumentTests
{
    private static readonly Dictionary<string, CelValue> Three = new()
    {
        ["ys"] = CelValue.FromList(CelList.Create(CelValue.FromInt64(2), CelValue.FromInt64(3), CelValue.FromInt64(4))),
    };

    // A CelList parameter takes list(dyn) as it is.
    [Fact]
    public static void CelListParameter_GetsTheListAsItIs()
    {
        CelList? received = null;
        Plan plan = Compile(
            new CelEnvironmentBuilder(CelStandardLibrary.V1).AddFunction("keep", (CelList items) =>
            {
                received = items;
                return (long)items.Count;
            }),
            "keep(ys)");

        Assert.Equal(CelValue.FromInt64(3), plan.Evaluate(Three, null, CancellationToken.None));
        Assert.True(Three["ys"].TryGetList(out CelList? bound));
        Assert.Same(bound, received);
    }

    // The read of ys and its check against list(int) 1 + 3, the check of the argument 3, the call 1; the
    // view adds nothing.
    [Fact]
    public static void ViewParameter_IsNotCharged()
    {
        Plan plan = Compile(
            First().AddFunctionImplementation("first_list_int", (IReadOnlyList<long> items) => items[0]),
            "first(ys)");
        Frame frame = plan.CreateFrame(Three, 100, CancellationToken.None);

        Assert.Equal((CelValue.FromInt64(2), 8UL), (plan.Run(frame), frame.ChargedUnits));
    }

    // The same with a copy to IReadOnlyList<int>: 1 + 3 more.
    [Fact]
    public static void CopyParameter_IsChargedOnePlusN()
    {
        Plan plan = Compile(
            First().AddFunctionImplementation("first_list_int", (IReadOnlyList<int> items) => (long)items[0]),
            "first(ys)");
        Frame frame = plan.CreateFrame(Three, 100, CancellationToken.None);

        Assert.Equal((CelValue.FromInt64(2), 12UL), (plan.Run(frame), frame.ChargedUnits));
    }

    // An element outside Int32 fails the copy.
    [Fact]
    public static void CopyWithAnElementOutsideTheParameterType_IsArgumentConversion()
    {
        Plan plan = Compile(
            First().AddFunctionImplementation("first_list_int", (IReadOnlyList<int> items) => (long)items[0]),
            "first(ys)");
        var bindings = new Dictionary<string, CelValue> { ["ys"] = CelValue.FromList(CelList.Create(CelValue.FromInt64(1L << 33))) };

        CelError error = Plans.Error(plan.Evaluate(bindings, null, CancellationToken.None));

        Assert.Equal(CelErrorCode.ArgumentConversion, error.Code);
    }

    // A copy of a map of 3 pairs with keys of lengths 1, 10 and 11 costs 1 + 2 · 3 + 1 + 1 + 2 = 11 more than the view.
    [Fact]
    public static void MapCopyParameter_IsChargedOnePlusTwoNAndItsStringKeys()
    {
        CelValue prices = CelValue.FromMap(CelMap.Create(
        [
            new(CelValue.FromString("a"), CelValue.FromInt64(1)),
            new(CelValue.FromString("0123456789"), CelValue.FromInt64(2)),
            new(CelValue.FromString("0123456789A"), CelValue.FromInt64(3)),
        ]));
        CelFunctionDecl count = new("count", CelOverloadDecl.Global("count_map", [CelType.MapOf(CelType.String, CelType.Int)], CelType.Int));

        ulong copy = ChargedUnits(
            new CelEnvironmentBuilder(CelStandardLibrary.V1).AddFunction(count)
                .AddFunctionImplementation("count_map", (IReadOnlyDictionary<string, int> map) => (long)map.Count),
            "count(x)",
            prices);
        ulong view = ChargedUnits(
            new CelEnvironmentBuilder(CelStandardLibrary.V1).AddFunction(count)
                .AddFunctionImplementation("count_map", (IReadOnlyDictionary<string, long> map) => (long)map.Count),
            "count(x)",
            prices);

        Assert.Equal(11UL, copy - view);
    }

    // A byte array of 25 bytes costs 1 + ⌈25/10⌉ = 4 more than ReadOnlyMemory<byte>.
    [Fact]
    public static void BytesCopyParameter_IsChargedOnePlusATenthOfItsLength()
    {
        CelValue raw = CelValue.FromBytes(new byte[25]);

        ulong copy = ChargedUnits(
            new CelEnvironmentBuilder(CelStandardLibrary.V1).AddFunction("length", (byte[] bytes) => (long)bytes.Length),
            "length(x)",
            raw);
        ulong view = ChargedUnits(
            new CelEnvironmentBuilder(CelStandardLibrary.V1).AddFunction("length", (ReadOnlyMemory<byte> bytes) => (long)bytes.Length),
            "length(x)",
            raw);

        Assert.Equal(4UL, copy - view);
    }

    // The units of an evaluation with x: dyn bound to the value.
    private static ulong ChargedUnits(CelEnvironmentBuilder builder, string expression, CelValue value)
    {
        Plan plan = builder.AddVariable("x", CelType.Dyn).Build().GetEnvironmentOrThrow()
            .Compile<CelValue>(expression).GetProgramOrThrow().Plan;
        Frame frame = plan.CreateFrame(new Dictionary<string, CelValue> { ["x"] = value }, 1000, CancellationToken.None);

        Assert.Equal(CelValueKind.Int, plan.Run(frame).Kind);
        return frame.ChargedUnits;
    }

    private static CelEnvironmentBuilder First() =>
        new CelEnvironmentBuilder(CelStandardLibrary.V1)
            .AddFunction(new CelFunctionDecl("first", CelOverloadDecl.Global("first_list_int", [CelType.ListOf(CelType.Int)], CelType.Int)));

    private static Plan Compile(CelEnvironmentBuilder builder, string expression) =>
        builder.AddVariable("ys", CelType.ListOf(CelType.Int)).Build().GetEnvironmentOrThrow()
            .Compile<CelValue>(expression).GetProgramOrThrow().Plan;
}
