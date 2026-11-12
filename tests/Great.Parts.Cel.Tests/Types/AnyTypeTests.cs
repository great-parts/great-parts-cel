using Great.Parts.Cel.Types;
using Xunit;

namespace Great.Parts.Cel.Tests.Types;

// CelType.Any: its kind, name and text form, and the places where the rules of types treat it as a type that takes every type —
// assignability in both directions, erasure and overlap, the most general type and the join of the elements of a literal.
public sealed class AnyTypeTests
{
    private static readonly CelType Item = CelType.ObjectNamed("acme.store.Item");

    public static TheoryData<string, CelType?> Others => new()
    {
        { "int", CelType.Int },
        { "string", CelType.String },
        { "null_type", CelType.Null },
        { "wrapper", CelType.WrapperOf(CelType.Int) },
        { "timestamp", CelType.Timestamp },
        { "list", CelType.ListOf(CelType.Int) },
        { "map", CelType.MapOf(CelType.String, CelType.Double) },
        { "type value", CelType.TypeOf(CelType.Int) },
        { "object", Item },
        { "dyn", CelType.Dyn },
        { "error", null },
    };

    [Fact]
    public void Any_IsOfTheKindAnyAndTheNameOfTheMessage_AndIsWrittenAny()
    {
        CelType any = CelType.Any;

        Assert.Equal((CelTypeKind.Any, "google.protobuf.Any"), (any.Kind, any.Name));
        Assert.Empty(any.Parameters);
        Assert.Equal("any", any.ToString());
        Assert.Equal("list(any)", CelType.ListOf(any).ToString());
        Assert.Equal("map(string, any)", CelType.MapOf(CelType.String, any).ToString());
    }

    [Fact]
    public void Any_IsNotDyn_AndIsTheTypeThatTheNameOfItsMessageStandsFor()
    {
        Assert.NotEqual(CelType.Dyn, CelType.Any);
        Assert.NotEqual(CelType.ObjectNamed("google.protobuf.Any.Other"), CelType.Any);
        Assert.Equal(CelType.Any, CelType.ObjectNamed("google.protobuf.Any"));
        Assert.Equal(CelType.Any.GetHashCode(), CelType.ObjectNamed("google.protobuf.Any").GetHashCode());
    }

    [Theory]
    [MemberData(nameof(Others))]
    public static void Any_TakesEveryType_AndEveryTypeTakesIt(string name, CelType? other)
    {
        var substitution = new TypeSubstitution();

        Assert.True(AssignabilityRules.IsAssignable(CelType.Any, other, substitution, []), name + " takes Any");
        Assert.True(AssignabilityRules.IsAssignable(other, CelType.Any, substitution, []), "Any takes " + name);
    }

    [Fact]
    public void Any_TakesNull_BeforeTheRuleOfNullTypeIsAsked()
    {
        var substitution = new TypeSubstitution();

        Assert.True(AssignabilityRules.IsAssignable(CelType.Null, CelType.Any, substitution, []));
        Assert.True(AssignabilityRules.IsAssignable(CelType.Any, CelType.Null, substitution, []));
    }

    [Fact]
    public void ListOfAny_TakesAListOfAnyElementType_AndIsTakenByIt()
    {
        var substitution = new TypeSubstitution();

        Assert.True(AssignabilityRules.IsAssignable(CelType.ListOf(CelType.Int), CelType.ListOf(CelType.Any), substitution, []));
        Assert.True(AssignabilityRules.IsAssignable(CelType.ListOf(CelType.Any), CelType.ListOf(CelType.String), substitution, []));
        Assert.True(AssignabilityRules.IsAssignable(CelType.MapOf(CelType.String, CelType.Any), CelType.MapOf(CelType.String, CelType.Int), substitution, []));
        Assert.False(AssignabilityRules.IsAssignable(CelType.ListOf(CelType.Any), CelType.MapOf(CelType.String, CelType.Int), substitution, []));
    }

    [Fact]
    public void Erase_LeavesAnyAsItIs()
    {
        Assert.Equal(CelType.Any, AssignabilityRules.Erase(CelType.Any));
        Assert.Equal(CelType.ListOf(CelType.Any), AssignabilityRules.Erase(CelType.ListOf(CelType.Any)));
        Assert.Equal(CelType.ListOf(CelType.Dyn), AssignabilityRules.Erase(CelType.ListOf(CelType.TypeParameter("T"))));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public static void Overlap_OfAnyWithAnyType_IsTrueInBothOrders(bool anyFirst)
    {
        CelType first = anyFirst ? CelType.Any : CelType.Int;
        CelType second = anyFirst ? CelType.Int : CelType.Any;

        Assert.True(AssignabilityRules.Overlap(first, second));
        Assert.True(AssignabilityRules.Overlap(CelType.ListOf(first), CelType.ListOf(second)));
    }

    [Fact]
    public void MostGeneral_OfAnyWithAny_IsAny()
    {
        Assert.Equal(CelType.Any, AssignabilityRules.MostGeneral(CelType.Any, CelType.Any, new TypeSubstitution()));
    }

    [Theory]
    [MemberData(nameof(Others))]
    public static void MostGeneral_OfAnyWithAnotherType_IsDyn_InBothOrders(string name, CelType? other)
    {
        var substitution = new TypeSubstitution();

        CelType? left = AssignabilityRules.MostGeneral(CelType.Any, other, substitution);
        CelType? right = AssignabilityRules.MostGeneral(other, CelType.Any, substitution);

        // The error type is no type: the other side stays, which is Any.
        CelType expected = other is null ? CelType.Any : CelType.Dyn;
        Assert.True(expected.Equals(left), name + ": Any first");
        Assert.True(expected.Equals(right), name + ": Any second");
    }

    [Fact]
    public void MostGeneral_OfListsOfAnyAndOfInt_IsAListOfDyn()
    {
        CelType? general = AssignabilityRules.MostGeneral(CelType.ListOf(CelType.Any), CelType.ListOf(CelType.Int), new TypeSubstitution());

        Assert.Equal(CelType.ListOf(CelType.Dyn), general);
    }

    [Fact]
    public void Join_OfAnyAndAnotherType_IsDyn_ButOfAnyAndAnyIsAny()
    {
        var substitution = new TypeSubstitution();

        Assert.Equal(CelType.Dyn, AssignabilityRules.Join(CelType.Any, CelType.Int, substitution));
        Assert.Equal(CelType.Dyn, AssignabilityRules.Join(CelType.Int, CelType.Any, substitution));
        Assert.Equal(CelType.Any, AssignabilityRules.Join(CelType.Any, CelType.Any, substitution));
    }

    [Fact]
    public void TypeParameterBoundToAny_IsBoundAgainToDynByAnotherType()
    {
        var substitution = new TypeSubstitution();
        CelType parameter = CelType.TypeParameter("T");

        Assert.True(AssignabilityRules.IsAssignable(CelType.Any, parameter, substitution, []));
        Assert.True(substitution.TryGet("T", out CelType? first));
        Assert.Equal(CelType.Any, first);

        Assert.True(AssignabilityRules.IsAssignable(CelType.Int, parameter, substitution, []));

        Assert.True(substitution.TryGet("T", out CelType? second));
        Assert.Equal(CelType.Dyn, second);
    }
}
