using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Tests.Values;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Declarations;

// Whether argument values fit an overload at evaluation: the top level of every argument for nothing, and at
// any depth, charged, each argument that the planner marked.
public sealed class OverloadBindingTests
{
    private static readonly CelValue Ints = CelValue.FromList(CelList.Create(CelValue.FromInt64(1), CelValue.FromInt64(2)));

    private static readonly CelValue Mixed = CelValue.FromList(CelList.Create(CelValue.FromInt64(1), CelValue.FromString("x")));

    private static readonly OverloadBinding ListOfInt = Binding("total_list", CelType.ListOf(CelType.Int), CelType.String);

    [Fact]
    public void UnmarkedArguments_AreCheckedAtTheTopLevelOnlyAndChargeNothing()
    {
        var meter = new RecordingMeter();

        Assert.True(ListOfInt.Matches([Mixed, CelValue.FromString("a")], [], meter).IsFit);
        Assert.True(ListOfInt.Matches([Mixed, CelValue.FromString("a")], [false, false], meter).IsFit);
        Assert.False(ListOfInt.Matches([CelValue.FromInt64(1), CelValue.FromString("a")], [], meter).IsFit);
        Assert.False(ListOfInt.Matches([Ints, CelValue.FromInt64(1)], [], meter).IsFit);
        Assert.Empty(meter.Charges);
    }

    [Fact]
    public void MarkedArguments_AreCheckedAtAnyDepthWithTheDescentCharged()
    {
        var meter = new RecordingMeter();

        Assert.True(ListOfInt.Matches([Ints, CelValue.FromString("a")], [true, true], meter).IsFit);
        Assert.Equal("2", string.Join(' ', meter.Charges));
        Assert.False(ListOfInt.Matches([Mixed, CelValue.FromString("a")], [true, false], meter).IsFit);
        Assert.False(ListOfInt.Matches([Mixed, CelValue.FromString("a")], [true, true], null).IsFit);
    }

    [Fact]
    public void TypeParametersAndDyn_TakeAnyValueButAnErrorValue()
    {
        OverloadBinding generic = Binding("pick_list", CelType.ListOf(CelType.TypeParameter("A")), CelType.Dyn);
        CelValue error = CelValue.FromError(new CelError(CelErrorCode.Overflow, CelErrorStage.Evaluate, "overflow", default, null, null));

        Assert.True(generic.Matches([Mixed, CelValue.Null], [true, true], null).IsFit);
        Assert.False(generic.Matches([Mixed, error], [true, true], null).IsFit);
    }

    // index_list of the library also takes an index of the kinds uint and double; an application overload
    // with that identifier does not.
    [Fact]
    public void IndexList_TakesANumericIndex()
    {
        OverloadBinding index = new(
            "_[_]",
            new CelOverloadDecl("index_list", false, [CelType.ListOf(CelType.TypeParameter("A")), CelType.Int], CelType.TypeParameter("A")),
            null,
            1,
            null,
            "standard library v1");
        OverloadBinding application = new(
            "_[_]",
            new CelOverloadDecl("index_list", false, [CelType.ListOf(CelType.Dyn), CelType.Int], CelType.Dyn),
            null,
            1,
            null,
            "application",
            isApplication: true);

        Assert.True(index.Matches([Ints, CelValue.FromUInt64(1)], [true, true], null).IsFit);
        Assert.True(index.Matches([Ints, CelValue.FromDouble(1.5)], [true, true], null).IsFit);
        Assert.True(index.Matches([Ints, CelValue.FromInt64(0)], [], null).IsFit);
        Assert.False(index.Matches([Ints, CelValue.FromString("0")], [true, true], null).IsFit);
        Assert.False(application.Matches([Ints, CelValue.FromUInt64(1)], [true, true], null).IsFit);
    }

    [Fact]
    public void Flags_AreKeptAsGiven()
    {
        OverloadBinding plain = new("total", CelOverloadDecl.Global("total_int", [CelType.Int], CelType.Int), null, 1, null, "standard library v1");
        OverloadBinding ordering = new("_<_", new CelOverloadDecl("less_int64_uint64", false, [CelType.Int, CelType.UInt], CelType.Bool), null, 1, null, "standard library v1", dynOnly: true);

        Assert.False(plain.IsApplication);
        Assert.False(plain.DynOnly);
        Assert.True(ordering.DynOnly);
        Assert.True(ListOfInt.IsApplication);
    }

    private static OverloadBinding Binding(string id, CelType first, CelType second) =>
        new("total", CelOverloadDecl.Global(id, [first, second], CelType.Int), null, 1, null, "application", isApplication: true);
}
