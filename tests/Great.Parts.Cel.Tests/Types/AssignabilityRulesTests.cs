using Great.Parts.Cel.Types;
using Xunit;

namespace Great.Parts.Cel.Tests.Types;

// AssignabilityRules: the checks of assignability in their order, type values as trials that bind only deducible
// variables, the most general type, the join of literals and the overlap of erased parameter types.
public sealed class AssignabilityRulesTests
{
    private static readonly CelType A = CelType.TypeParameter("A");
    private static readonly CelType Fresh = CelType.TypeParameter("_var0");
    private static readonly CelType Other = CelType.TypeParameter("_var1");
    private static readonly CelType Item = CelType.ObjectNamed("acme.store.Item");
    private static readonly CelType Order = CelType.ObjectNamed("acme.store.Order");

    public static TheoryData<string, CelType?, CelType?, bool> TypesWithoutParameters => new()
    {
        { "same primitive", CelType.Int, CelType.Int, true },
        { "other primitive", CelType.Int, CelType.UInt, false },
        { "string to bytes", CelType.String, CelType.Bytes, false },
        { "to dyn", CelType.Int, CelType.Dyn, true },
        { "from dyn", CelType.Dyn, CelType.ListOf(CelType.Int), true },
        { "from error", null, CelType.Bool, true },
        { "to error", CelType.Bool, null, true },
        { "null to null", CelType.Null, CelType.Null, true },
        { "null to wrapper", CelType.Null, CelType.WrapperOf(CelType.Int), true },
        { "wrapper to null", CelType.WrapperOf(CelType.Int), CelType.Null, true },
        { "null to object", CelType.Null, Item, true },
        { "null to primitive", CelType.Null, CelType.String, false },
        { "primitive to null", CelType.Double, CelType.Null, false },
        { "null to list", CelType.Null, CelType.ListOf(CelType.Int), false },
        { "null to map", CelType.Null, CelType.MapOf(CelType.Int, CelType.Int), false },
        { "null to type", CelType.Null, CelType.TypeOf(CelType.Int), false },
        { "wrapper to its primitive", CelType.WrapperOf(CelType.Int), CelType.Int, true },
        { "primitive to its wrapper", CelType.Int, CelType.WrapperOf(CelType.Int), true },
        { "wrapper to the same wrapper", CelType.WrapperOf(CelType.Int), CelType.WrapperOf(CelType.Int), true },
        { "wrapper to another wrapper", CelType.WrapperOf(CelType.Int), CelType.WrapperOf(CelType.UInt), false },
        { "wrapper to another primitive", CelType.WrapperOf(CelType.Int), CelType.UInt, false },
        { "type values of different types", CelType.TypeOf(CelType.Int), CelType.TypeOf(CelType.String), true },
        { "type value to its type", CelType.TypeOf(CelType.Int), CelType.Int, false },
        { "list of same elements", CelType.ListOf(CelType.Int), CelType.ListOf(CelType.Int), true },
        { "list to list of dyn", CelType.ListOf(CelType.Int), CelType.ListOf(CelType.Dyn), true },
        { "list of other elements", CelType.ListOf(CelType.Int), CelType.ListOf(CelType.String), false },
        { "map to map of dyn values", CelType.MapOf(CelType.String, CelType.Int), CelType.MapOf(CelType.String, CelType.Dyn), true },
        { "map of other keys", CelType.MapOf(CelType.String, CelType.Int), CelType.MapOf(CelType.Int, CelType.Int), false },
        { "list to map", CelType.ListOf(CelType.Int), CelType.MapOf(CelType.Int, CelType.Int), false },
        { "same object", Item, CelType.ObjectNamed("acme.store.Item"), true },
        { "other object", Item, Order, false },
        { "object to primitive", Item, CelType.String, false },
    };

    [Theory]
    [MemberData(nameof(TypesWithoutParameters))]
    public static void IsAssignable_WithoutTypeParameters_FollowsTheRules(string name, CelType? from, CelType? to, bool expected)
    {
        Assert.True(expected == AssignabilityRules.IsAssignable(from, to, new TypeSubstitution(), []), name);
    }

    [Fact]
    public void FreeParameter_IsBoundToTheTypeOfTheValue()
    {
        var substitution = new TypeSubstitution();

        Assert.True(AssignabilityRules.IsAssignable(CelType.Int, A, substitution, []));

        Assert.Equal(CelType.Int, Bound(substitution, A));
        Assert.False(AssignabilityRules.IsAssignable(CelType.String, A, substitution, []));
        Assert.True(AssignabilityRules.IsAssignable(CelType.Int, A, substitution, []));
    }

    [Fact]
    public void BoundParameter_IsBoundAgainToTheMostGeneralType()
    {
        var substitution = new TypeSubstitution();
        Assert.True(AssignabilityRules.IsAssignable(CelType.Int, A, substitution, []));

        Assert.True(AssignabilityRules.IsAssignable(CelType.WrapperOf(CelType.Int), A, substitution, []));

        Assert.Equal(CelType.WrapperOf(CelType.Int), Bound(substitution, A));
    }

    [Fact]
    public void ParameterAsTheTypeOfTheValue_IsBoundToTheExpectedType()
    {
        var substitution = new TypeSubstitution();

        Assert.True(AssignabilityRules.IsAssignable(CelType.ListOf(A), CelType.ListOf(CelType.Double), substitution, []));

        Assert.Equal(CelType.Double, Bound(substitution, A));
    }

    [Fact]
    public void OccursCheck_RefusesABindingThatRefersToItself()
    {
        var substitution = new TypeSubstitution();

        Assert.False(AssignabilityRules.IsAssignable(CelType.ListOf(A), A, substitution, []));
        Assert.False(AssignabilityRules.IsAssignable(A, CelType.ListOf(A), substitution, []));

        Assert.False(substitution.TryGet("A", out _));
    }

    [Fact]
    public void ParameterAgainstItselfDynAndError_IsAssignable()
    {
        var substitution = new TypeSubstitution();

        Assert.True(AssignabilityRules.IsAssignable(A, A, substitution, []));
        Assert.True(AssignabilityRules.IsAssignable(null, A, substitution, []));
        Assert.False(substitution.TryGet("A", out _));
        Assert.True(AssignabilityRules.IsAssignable(CelType.Dyn, A, substitution, []));
        Assert.Equal(CelType.Dyn, Bound(substitution, A));
    }

    // A deducible variable inside a type value is deduced: (dyn, type(T)) -> T with type(int) gives T = int.
    [Fact]
    public void DeducibleVariableInsideATypeValue_IsBound()
    {
        var substitution = new TypeSubstitution();

        Assert.True(AssignabilityRules.IsAssignable(CelType.TypeOf(CelType.ListOf(CelType.Bool)), CelType.TypeOf(CelType.ListOf(Fresh)), substitution, [Fresh]));
        Assert.True(AssignabilityRules.IsAssignable(CelType.TypeOf(CelType.TypeOf(CelType.Bytes)), CelType.TypeOf(CelType.TypeOf(Other)), substitution, [Other]));

        Assert.Equal(CelType.Bool, Bound(substitution, Fresh));
        Assert.Equal(CelType.Bytes, Bound(substitution, Other));
    }

    [Fact]
    public void VariableInsideATypeValue_ThatIsNotDeducible_IsNeitherBoundNorBoundAgain()
    {
        var substitution = new TypeSubstitution();
        Assert.True(AssignabilityRules.IsAssignable(CelType.UInt, Other, substitution, []));

        Assert.True(AssignabilityRules.IsAssignable(CelType.TypeOf(CelType.Int), CelType.TypeOf(Fresh), substitution, []));
        Assert.True(AssignabilityRules.IsAssignable(CelType.TypeOf(CelType.Int), CelType.TypeOf(Other), substitution, []));
        Assert.True(AssignabilityRules.IsAssignable(CelType.TypeOf(Fresh), CelType.TypeOf(CelType.String), substitution, [Other]));

        Assert.False(substitution.TryGet(Fresh.Name, out _));
        Assert.Equal(CelType.UInt, Bound(substitution, Other));
    }

    // A free variable that is not deducible matches a free deducible one, which is bound to it.
    [Fact]
    public void FreeDeducibleVariable_IsBoundToAVariableThatIsNotDeducible()
    {
        var substitution = new TypeSubstitution();

        Assert.True(AssignabilityRules.IsAssignable(CelType.TypeOf(Fresh), CelType.TypeOf(Other), substitution, [Other]));

        Assert.Equal(Fresh, Bound(substitution, Other));
        Assert.False(substitution.TryGet(Fresh.Name, out _));
    }

    [Fact]
    public void FailedTrialInsideATypeValue_BindsNothing()
    {
        var substitution = new TypeSubstitution();

        Assert.True(AssignabilityRules.IsAssignable(
            CelType.TypeOf(CelType.MapOf(CelType.Int, CelType.String)),
            CelType.TypeOf(CelType.MapOf(Fresh, Fresh)),
            substitution,
            [Fresh]));

        Assert.False(substitution.TryGet(Fresh.Name, out _));
    }

    // type(x) == int with x of a free variable: the comparison binds the parameter of _==_ and never narrows x.
    [Fact]
    public void ComparisonOfTypeValues_DoesNotNarrowTheOperand()
    {
        var substitution = new TypeSubstitution();
        CelType equalsParameter = CelType.TypeParameter("_var7");

        Assert.True(AssignabilityRules.IsAssignable(CelType.TypeOf(Fresh), equalsParameter, substitution, [equalsParameter]));
        Assert.True(AssignabilityRules.IsAssignable(CelType.TypeOf(CelType.Int), equalsParameter, substitution, [equalsParameter]));

        Assert.Equal(CelType.TypeOf(CelType.Dyn), Bound(substitution, equalsParameter));
        Assert.False(substitution.TryGet(Fresh.Name, out _));
        Assert.True(AssignabilityRules.IsAssignable(CelType.String, Fresh, substitution, []));
    }

    public static TheoryData<string, CelType?, CelType?, CelType?> MostGeneralCases => new()
    {
        { "dyn first", CelType.Dyn, CelType.Int, CelType.Dyn },
        { "dyn second", CelType.Int, CelType.Dyn, CelType.Dyn },
        { "free parameter first", A, CelType.Int, A },
        { "free parameter second", CelType.Int, A, A },
        { "equal type values", CelType.TypeOf(CelType.Int), CelType.TypeOf(CelType.Int), CelType.TypeOf(CelType.Int) },
        { "different type values", CelType.TypeOf(CelType.Int), CelType.TypeOf(CelType.UInt), CelType.TypeOf(CelType.Dyn) },
        { "type value with a free parameter", CelType.TypeOf(CelType.ListOf(A)), CelType.TypeOf(CelType.ListOf(CelType.Int)), CelType.TypeOf(CelType.Dyn) },
        { "lists of type values", CelType.ListOf(CelType.TypeOf(CelType.Int)), CelType.ListOf(CelType.TypeOf(CelType.Bool)), CelType.ListOf(CelType.TypeOf(CelType.Dyn)) },
        { "maps of type values", CelType.MapOf(CelType.String, CelType.TypeOf(CelType.Int)), CelType.MapOf(CelType.String, CelType.TypeOf(CelType.Bool)), CelType.MapOf(CelType.String, CelType.TypeOf(CelType.Dyn)) },
        { "wrapper and primitive", CelType.WrapperOf(CelType.Int), CelType.Int, CelType.WrapperOf(CelType.Int) },
        { "primitive and wrapper", CelType.Int, CelType.WrapperOf(CelType.Int), CelType.WrapperOf(CelType.Int) },
        { "null and wrapper", CelType.Null, CelType.WrapperOf(CelType.String), CelType.WrapperOf(CelType.String) },
        { "wrapper and null", CelType.WrapperOf(CelType.String), CelType.Null, CelType.WrapperOf(CelType.String) },
        { "null and object", CelType.Null, Item, Item },
        { "equal types", CelType.ListOf(CelType.Int), CelType.ListOf(CelType.Int), CelType.ListOf(CelType.Int) },
        { "error first", null, CelType.Int, CelType.Int },
        { "error second", CelType.Int, null, CelType.Int },
    };

    [Theory]
    [MemberData(nameof(MostGeneralCases))]
    public static void MostGeneral_IsTheLeastUpperBound(string name, CelType? a, CelType? b, CelType? expected)
    {
        Assert.True(Equals(expected, AssignabilityRules.MostGeneral(a, b, new TypeSubstitution())), name);
    }

    [Fact]
    public void MostGeneral_AppliesTheSubstitutionFirst()
    {
        var substitution = new TypeSubstitution();
        Assert.True(SubstitutionRules.TryBind(substitution, A, CelType.TypeOf(CelType.Int)));

        Assert.Equal(CelType.TypeOf(CelType.Int), AssignabilityRules.MostGeneral(A, CelType.TypeOf(CelType.Int), substitution));
        Assert.Equal(CelType.TypeOf(CelType.Dyn), AssignabilityRules.MostGeneral(CelType.TypeOf(CelType.UInt), A, substitution));
    }

    public static TheoryData<string, CelType, CelType?, CelType> JoinCases => new()
    {
        { "different primitives", CelType.Int, CelType.String, CelType.Dyn },
        { "primitive and null", CelType.Int, CelType.Null, CelType.Dyn },
        { "primitive then wrapper", CelType.Int, CelType.WrapperOf(CelType.Int), CelType.WrapperOf(CelType.Int) },
        { "wrapper then primitive", CelType.WrapperOf(CelType.Int), CelType.Int, CelType.WrapperOf(CelType.Int) },
        { "type values", CelType.TypeOf(CelType.Bool), CelType.TypeOf(CelType.Double), CelType.TypeOf(CelType.Dyn) },
        { "equal lists", CelType.ListOf(CelType.Bool), CelType.ListOf(CelType.Bool), CelType.ListOf(CelType.Bool) },
        { "error", CelType.Bool, null, CelType.Bool },
    };

    [Theory]
    [MemberData(nameof(JoinCases))]
    public static void Join_IsTheMostGeneralTypeOrDyn(string name, CelType joined, CelType? next, CelType expected)
    {
        Assert.True(Equals(expected, AssignabilityRules.Join(joined, next, new TypeSubstitution())), name);
    }

    [Fact]
    public void Join_BindsTheVariableOfAnEmptyLiteral()
    {
        var substitution = new TypeSubstitution();

        Assert.Equal(CelType.ListOf(CelType.Int), AssignabilityRules.Join(CelType.ListOf(Fresh), CelType.ListOf(CelType.Int), substitution));

        Assert.Equal(CelType.Int, Bound(substitution, Fresh));
    }

    [Fact]
    public void Join_BindsNothingInsideTypeValuesOrWhenItFails()
    {
        var substitution = new TypeSubstitution();

        Assert.Equal(CelType.TypeOf(CelType.Dyn), AssignabilityRules.Join(CelType.TypeOf(CelType.ListOf(Fresh)), CelType.TypeOf(CelType.ListOf(CelType.Int)), substitution));
        Assert.Equal(CelType.Dyn, AssignabilityRules.Join(CelType.MapOf(Fresh, CelType.String), CelType.MapOf(CelType.Int, CelType.Int), substitution));

        Assert.False(substitution.TryGet(Fresh.Name, out _));
    }

    public static TheoryData<string, CelType, CelType, bool> OverlapCases => new()
    {
        { "parameter and concrete element", CelType.ListOf(A), CelType.ListOf(CelType.Int), true },
        { "dyn and anything", CelType.Dyn, CelType.MapOf(CelType.String, CelType.Int), true },
        { "primitive and its wrapper", CelType.Int, CelType.WrapperOf(CelType.Int), true },
        { "different elements", CelType.ListOf(CelType.Int), CelType.ListOf(CelType.String), false },
        { "different primitives", CelType.Int, CelType.Double, false },
        { "type values", CelType.TypeOf(CelType.Int), CelType.TypeOf(A), true },
    };

    [Theory]
    [MemberData(nameof(OverlapCases))]
    public static void Overlap_ComparesErasedTypesBothWays(string name, CelType first, CelType second, bool expected)
    {
        Assert.True(expected == AssignabilityRules.Overlap(first, second), name);
        Assert.True(expected == AssignabilityRules.Overlap(second, first), name);
    }

    [Fact]
    public void Erase_ReplacesEveryTypeParameterByDyn()
    {
        CelType concrete = CelType.MapOf(CelType.String, CelType.Int);

        Assert.Equal(CelType.MapOf(CelType.Dyn, CelType.ListOf(CelType.TypeOf(CelType.Dyn))), AssignabilityRules.Erase(CelType.MapOf(A, CelType.ListOf(CelType.TypeOf(Fresh)))));
        Assert.Same(concrete, AssignabilityRules.Erase(concrete));
    }

    private static CelType Bound(TypeSubstitution substitution, CelType parameter)
    {
        Assert.True(substitution.TryGet(parameter.Name, out CelType? type), parameter.Name);
        return type;
    }
}
