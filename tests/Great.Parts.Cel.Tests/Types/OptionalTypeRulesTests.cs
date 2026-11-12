using Great.Parts.Cel.Types;
using Xunit;

namespace Great.Parts.Cel.Tests.Types;

// The rules of optional_type(T) and of abstract types with parameters in the assignability and the most general type: each row of the
// table of the most general type by a pair of types, in both orders, the assignability of the pairs that the rules take and refuse,
// and the deduction through a substitution, which the cases of type_deduction with optional.none() and tuple(…) need. The types
// of the abstract type are made as the sets make them: a name with parameters.
public sealed class OptionalTypeRulesTests
{
    private static CelType Optional(CelType valueType) => CelType.OptionalOf(valueType);

    private static CelType Tuple(params CelType[] parameters) => CelType.AbstractNamed("tuple", [.. parameters]);

    public static TheoryData<CelType, CelType, CelType> MostGeneralRows => new()
    {
        { Optional(CelType.Int), Optional(CelType.Int), Optional(CelType.Int) },
        { Optional(CelType.Int), Optional(CelType.Dyn), Optional(CelType.Dyn) },
        { Optional(CelType.ListOf(CelType.Int)), Optional(CelType.ListOf(CelType.Dyn)), Optional(CelType.ListOf(CelType.Dyn)) },
        { Optional(Optional(CelType.Int)), Optional(Optional(CelType.Dyn)), Optional(Optional(CelType.Dyn)) },
        { Optional(CelType.WrapperOf(CelType.Int)), Optional(CelType.Int), Optional(CelType.WrapperOf(CelType.Int)) },
        { Optional(CelType.TypeOf(CelType.Int)), Optional(CelType.TypeOf(CelType.UInt)), Optional(CelType.TypeOf(CelType.Dyn)) },
        { CelType.ListOf(Optional(CelType.Int)), CelType.ListOf(Optional(CelType.Dyn)), CelType.ListOf(Optional(CelType.Dyn)) },
        {
            CelType.MapOf(CelType.String, Optional(CelType.Int)),
            CelType.MapOf(CelType.String, Optional(CelType.Dyn)),
            CelType.MapOf(CelType.String, Optional(CelType.Dyn))
        },
        { Tuple(CelType.Int, CelType.UInt, CelType.Double), Tuple(CelType.Dyn, CelType.Dyn, CelType.Dyn), Tuple(CelType.Dyn, CelType.Dyn, CelType.Dyn) },
        { Tuple(CelType.Int, CelType.ListOf(CelType.Int)), Tuple(CelType.Dyn, CelType.ListOf(CelType.Dyn)), Tuple(CelType.Dyn, CelType.ListOf(CelType.Dyn)) },
        { Tuple(CelType.Int), Tuple(CelType.Int), Tuple(CelType.Int) },
        { Optional(CelType.Int), CelType.Null, Optional(CelType.Int) },
        { Tuple(CelType.Int), CelType.Null, Tuple(CelType.Int) },
    };

    public static TheoryData<CelType, CelType, bool> AssignableRows => new()
    {
        { Optional(CelType.Int), Optional(CelType.Int), true },
        { Optional(CelType.Int), Optional(CelType.Dyn), true },
        { Optional(CelType.Dyn), Optional(CelType.String), true },
        { Optional(CelType.Int), Optional(CelType.String), false },
        { Optional(CelType.ListOf(CelType.Int)), Optional(CelType.ListOf(CelType.Int)), true },
        { Optional(CelType.ListOf(CelType.Int)), Optional(CelType.ListOf(CelType.String)), false },
        { Optional(Optional(CelType.Int)), Optional(Optional(CelType.Dyn)), true },
        { Optional(Optional(CelType.Int)), Optional(Optional(CelType.String)), false },
        { Optional(CelType.Int), Optional(Optional(CelType.Int)), false },
        { Optional(CelType.Int), CelType.Int, false },
        { CelType.Int, Optional(CelType.Int), false },
        { Optional(CelType.Int), CelType.ListOf(CelType.Int), false },
        { CelType.ListOf(CelType.Int), Optional(CelType.Int), false },
        { Optional(CelType.Int), CelType.Dyn, true },
        { CelType.Dyn, Optional(CelType.Int), true },
        { CelType.Null, Optional(CelType.Int), true },
        { Optional(CelType.Int), Tuple(CelType.Int), false },
        { Tuple(CelType.Int), Optional(CelType.Int), false },
        { Tuple(CelType.Int), Tuple(CelType.Int), true },
        { Tuple(CelType.Int), Tuple(CelType.Dyn), true },
        { Tuple(CelType.Int), Tuple(CelType.String), false },
        { Tuple(CelType.Int), Tuple(CelType.Int, CelType.Int), false },
        { Tuple(CelType.Int), CelType.AbstractNamed("other", [CelType.Int]), false },
        { Tuple(CelType.Int), CelType.Null, true },
    };

    [Theory]
    [MemberData(nameof(MostGeneralRows))]
    public static void MostGeneral_IsTheSameInBothOrders(CelType first, CelType second, CelType expected)
    {
        var substitution = new TypeSubstitution();

        Assert.Equal(expected, AssignabilityRules.MostGeneral(first, second, substitution));
        Assert.Equal(expected, AssignabilityRules.MostGeneral(second, first, substitution));
    }

    [Theory]
    [MemberData(nameof(AssignableRows))]
    public static void IsAssignable_TakesTheParametersOfOptionalAndAbstractTypesOneByOne(CelType from, CelType to, bool expected) =>
        Assert.Equal(expected, AssignabilityRules.IsAssignable(from, to, new TypeSubstitution(), []));

    // Types that are not assignable are not generalized: the join of a literal takes dyn for them.
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public static void Join_OfTypesThatAreNotAssignable_IsDyn(int row)
    {
        (CelType first, CelType second) = row switch
        {
            0 => (Optional(CelType.Int), CelType.ListOf(CelType.Int)),
            1 => (Optional(CelType.Int), Optional(CelType.String)),
            2 => (Tuple(CelType.Int), Tuple(CelType.String)),
            _ => (Tuple(CelType.Int), Optional(CelType.Int)),
        };

        Assert.Equal(CelType.Dyn, AssignabilityRules.Join(first, second, new TypeSubstitution()));
        Assert.Equal(CelType.Dyn, AssignabilityRules.Join(second, first, new TypeSubstitution()));
    }

    // optional.none() is optional_type of a free variable; the join binds the variable first and then both parameters are int.
    [Fact]
    public static void Join_OfOptionalNoneAndOptionalInt_BindsTheFreeVariable()
    {
        var substitution = new TypeSubstitution();
        CelType free = CelType.TypeParameter("_var0");

        CelType joined = AssignabilityRules.Join(Optional(free), Optional(CelType.Int), substitution);

        Assert.Equal(Optional(CelType.Int), joined);
        Assert.True(substitution.TryGet("_var0", out CelType? bound));
        Assert.Equal(CelType.Int, bound);
    }

    // The parameter of an optional type is bound once: a second type that does not fit the binding is refused, and the order of the
    // two arguments makes no difference to the join.
    [Fact]
    public static void IsAssignable_BindsTheVariableInsideAnOptionalOnce()
    {
        var substitution = new TypeSubstitution();
        CelType free = CelType.TypeParameter("_var0");

        Assert.True(AssignabilityRules.IsAssignable(Optional(CelType.Int), Optional(free), substitution, []));
        Assert.True(substitution.TryGet("_var0", out CelType? bound));
        Assert.Equal(CelType.Int, bound);
        Assert.False(AssignabilityRules.IsAssignable(Optional(CelType.String), Optional(free), substitution, []));
        Assert.True(AssignabilityRules.IsAssignable(Optional(CelType.Dyn), Optional(free), substitution, []));
    }

    // Inside a type value only the deducible variables are bound: an optional type in type(…) deduces its parameter, and a variable
    // that is not deducible stays free.
    [Fact]
    public static void TypeValueOfAnOptional_BindsOnlyTheDeducibleVariables()
    {
        var substitution = new TypeSubstitution();
        CelType deducible = CelType.TypeParameter("T");
        CelType other = CelType.TypeParameter("U");

        Assert.True(AssignabilityRules.IsAssignable(CelType.TypeOf(Optional(CelType.Int)), CelType.TypeOf(Optional(deducible)), substitution, [deducible]));
        Assert.True(AssignabilityRules.IsAssignable(CelType.TypeOf(Optional(CelType.String)), CelType.TypeOf(Optional(other)), substitution, [deducible]));

        Assert.True(substitution.TryGet("T", out CelType? bound));
        Assert.Equal(CelType.Int, bound);
        Assert.False(substitution.TryGet("U", out _));
    }

    // A tuple(T, U, V) of an overload takes the types of its arguments: the parameters are bound one by one, and the most general of
    // two bindings keeps the more general type.
    [Fact]
    public static void AbstractTypeWithParameters_BindsEachParameterOfTheOverload()
    {
        var substitution = new TypeSubstitution();
        CelType first = CelType.TypeParameter("_var0");
        CelType second = CelType.TypeParameter("_var1");
        CelType declared = Tuple(first, second);

        Assert.True(AssignabilityRules.IsAssignable(Tuple(CelType.Int, CelType.UInt), declared, substitution, []));
        Assert.True(AssignabilityRules.IsAssignable(Tuple(CelType.Dyn, CelType.UInt), declared, substitution, []));

        Assert.Equal(Tuple(CelType.Dyn, CelType.UInt), SubstitutionRules.Apply(substitution, declared, freeToDyn: true));
    }

    // An optional type that holds a type parameter is generalized only once the parameter is bound: before, the parameter itself is
    // the most general type, as for a bare type parameter.
    [Fact]
    public static void MostGeneral_OfAnOptionalWithAFreeParameter_WaitsForTheBinding()
    {
        var substitution = new TypeSubstitution();
        CelType free = CelType.TypeParameter("_var0");

        CelType? beforeBinding = AssignabilityRules.MostGeneral(Optional(free), Optional(CelType.Int), substitution);
        Assert.Equal(Optional(free), beforeBinding);

        Assert.True(AssignabilityRules.IsAssignable(CelType.Int, free, substitution, []));
        Assert.Equal(Optional(CelType.Int), AssignabilityRules.MostGeneral(Optional(free), Optional(CelType.Int), substitution));
    }
}
