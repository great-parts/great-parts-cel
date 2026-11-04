using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using Great.Parts.Cel.Types;
using Xunit;

namespace Great.Parts.Cel.Tests.Types;

// SubstitutionRules and TypeSubstitution: binding with the occurs check, applying bindings recursively, and rolling a
// failed trial back.
public sealed class SubstitutionRulesTests
{
    private static readonly CelType A = CelType.TypeParameter("A");
    private static readonly CelType B = CelType.TypeParameter("B");

    [Fact]
    public void TryBind_RefusesAParameterThatOccursInTheType()
    {
        var substitution = new TypeSubstitution();
        Assert.True(SubstitutionRules.TryBind(substitution, B, CelType.ListOf(A)));

        Assert.False(SubstitutionRules.TryBind(substitution, A, A));
        Assert.False(SubstitutionRules.TryBind(substitution, A, CelType.MapOf(CelType.String, CelType.TypeOf(A))));
        Assert.False(SubstitutionRules.TryBind(substitution, A, B));
        Assert.False(substitution.TryGet("A", out _));

        Assert.True(SubstitutionRules.TryBind(substitution, A, CelType.WrapperOf(CelType.Int)));
        Assert.True(substitution.TryGet("A", out CelType? bound));
        Assert.Equal(CelType.WrapperOf(CelType.Int), bound);
    }

    [Fact]
    public void Occurs_FollowsBindings()
    {
        var substitution = new TypeSubstitution();
        Assert.True(SubstitutionRules.TryBind(substitution, B, CelType.ListOf(A)));

        Assert.True(SubstitutionRules.Occurs(substitution, A, CelType.MapOf(CelType.Int, B)));
        Assert.False(SubstitutionRules.Occurs(substitution, B, CelType.MapOf(CelType.Int, A)));
    }

    [Fact]
    public void Apply_ReplacesBoundParametersRecursively()
    {
        var substitution = new TypeSubstitution();
        Assert.True(SubstitutionRules.TryBind(substitution, A, CelType.ListOf(B)));
        Assert.True(SubstitutionRules.TryBind(substitution, B, CelType.UInt));

        CelType applied = SubstitutionRules.Apply(substitution, CelType.MapOf(CelType.String, CelType.TypeOf(A)), freeToDyn: false);

        Assert.Equal(CelType.MapOf(CelType.String, CelType.TypeOf(CelType.ListOf(CelType.UInt))), applied);
    }

    [Fact]
    public void Apply_ReplacesFreeParametersByDynOnlyWhenAsked()
    {
        var substitution = new TypeSubstitution();
        CelType type = CelType.ListOf(A);

        Assert.Same(type, SubstitutionRules.Apply(substitution, type, freeToDyn: false));
        Assert.Equal(CelType.ListOf(CelType.Dyn), SubstitutionRules.Apply(substitution, type, freeToDyn: true));
    }

    // A chain of bindings from each variable to the next, as long as the elements of a list of empty lists in 64 KB and
    // more: applying, the occurs check and applying with kept results follow it without running out of stack.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public static void Apply_AndOccurs_FollowALongChainOfBindings(bool freeEnd)
    {
        CelType[] chain = Chain(100_000);
        var substitution = new TypeSubstitution();
        for (int i = 0; i < chain.Length - 1; i++)
        {
            substitution.Set(chain[i].Name, chain[i + 1]);
        }

        if (!freeEnd)
        {
            substitution.Set(chain[^1].Name, CelType.Int);
        }

        CelType expected = CelType.ListOf(freeEnd ? CelType.Dyn : CelType.Int);
        Assert.Equal(expected, SubstitutionRules.Apply(substitution, CelType.ListOf(chain[0]), freeToDyn: true));
        Assert.Equal(expected, SubstitutionRules.Apply(substitution, CelType.ListOf(chain[0]), freeToDyn: true, new Dictionary<string, StrongBox<CelType>>(StringComparer.Ordinal)));
        Assert.Equal(freeEnd ? chain[^1] : CelType.Int, SubstitutionRules.Apply(substitution, chain[0], freeToDyn: false));
        Assert.True(SubstitutionRules.Occurs(substitution, chain[^1], chain[0]));
        Assert.False(SubstitutionRules.Occurs(substitution, A, CelType.ListOf(chain[0])));
    }

    // The results kept for one variable are those of every variable of its chain, so a later type finds them without
    // the chain; they equal the results without keeping.
    [Fact]
    public void Apply_WithKeptResults_KeepsTheResultOfEveryVariableOfTheChain()
    {
        CelType[] chain = Chain(4);
        var substitution = new TypeSubstitution();
        substitution.Set(chain[0].Name, chain[1]);
        substitution.Set(chain[1].Name, CelType.MapOf(CelType.String, chain[2]));
        substitution.Set(chain[2].Name, chain[3]);
        var kept = new Dictionary<string, StrongBox<CelType>>(StringComparer.Ordinal);

        CelType first = SubstitutionRules.Apply(substitution, chain[0], freeToDyn: true, kept);

        Assert.Equal(CelType.MapOf(CelType.String, CelType.Dyn), first);
        Assert.Equal(first, SubstitutionRules.Apply(substitution, chain[0], freeToDyn: true));
        Assert.Equal(
            new (string, CelType?)[] { ("_v0", first), ("_v1", first), ("_v2", CelType.Dyn), ("_v3", CelType.Dyn) },
            kept.OrderBy(entry => entry.Key, StringComparer.Ordinal).Select(entry => (entry.Key, entry.Value.Value)));
        Assert.Same(kept["_v0"], kept["_v1"]);
        Assert.Same(first, SubstitutionRules.Apply(substitution, chain[1], freeToDyn: true, kept));
    }

    [Fact]
    public void Rollback_UndoesEveryChangeAfterTheMark()
    {
        var substitution = new TypeSubstitution();
        substitution.Set("A", CelType.Int);
        int mark = substitution.Mark;

        substitution.Set("A", CelType.WrapperOf(CelType.Int));
        substitution.Set("B", CelType.String);
        substitution.Rollback(mark);

        Assert.True(substitution.TryGet("A", out CelType? a));
        Assert.Equal(CelType.Int, a);
        Assert.False(substitution.TryGet("B", out _));
        Assert.Equal(mark, substitution.Mark);
        substitution.Rollback(0);
        Assert.False(substitution.TryGet("A", out _));
    }

    private static CelType[] Chain(int length)
    {
        var chain = new CelType[length];
        for (int i = 0; i < length; i++)
        {
            chain[i] = CelType.TypeParameter("_v" + i.ToString(CultureInfo.InvariantCulture));
        }

        return chain;
    }
}
