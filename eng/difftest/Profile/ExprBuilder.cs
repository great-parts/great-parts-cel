using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using System.Text;
using CsCheck;

namespace Great.Parts.Cel.Eng.Difftest.Profile;

/// <summary>
/// Builds a well-typed expression of the profile for a target type (docs/design/eng-difftest.md, "Profile of the walking
/// skeleton"), within the limits of depth and nodes.
/// </summary>
internal sealed class ExprBuilder
{
    // A node gets parentheses that precedence does not need with a chance of one in this.
    private const int ParenthesesOneIn = 10;

    // An operand of _+_ or _/_ is a dyn variable with a chance of one in this.
    private const int DynOperandOneIn = 4;

    // A list or map literal ends with a comma with a chance of one in this.
    private const int TrailingCommaOneIn = 4;

    // The target types of the root, list elements and map values: every type of the profile, int and bool more often, since
    // the functions of the profile take them.
    private static readonly ProfileType[] WeightedTargets =
    [
        .. ProfileTables.TargetTypes,
        ProfileType.IntType,
        ProfileType.IntType,
        ProfileType.BoolType,
    ];

    private static readonly string[] IntVariables = ["i1", "i2"];
    private static readonly string[] BoolVariables = ["b1", "b2", "x1", "x2"];
    private static readonly string[] DynVariables = ["x1", "x2"];

    private readonly PCG pcg;

    /// <summary>Creates a builder that draws every choice from a random source.</summary>
    /// <param name="pcg">The random source.</param>
    internal ExprBuilder(PCG pcg) => this.pcg = pcg;

    /// <summary>Builds an expression.</summary>
    /// <param name="type">The target type: a member of <see cref="ProfileTables.TargetTypes"/>.</param>
    /// <param name="depth">The depth of the node, 1 for the root.</param>
    /// <param name="limit">The largest number of nodes of the expression, at least 1.</param>
    /// <returns>The expression and its number of nodes.</returns>
    internal (Expr Expr, int Nodes) Build(ProfileType type, int depth, int limit)
    {
        (Expr expr, int nodes) = type switch
        {
            ProfileType.IntType => BuildInt(depth, limit),
            ProfileType.BoolType => BuildBool(depth, limit),
            ProfileType.ListType => BuildList(depth, limit),
            ProfileType.MapType => BuildMap(depth, limit),
            _ => (BuildScalar(type), 1),
        };
        return (Maybe(expr), nodes);
    }

    /// <summary>A target type for the root, a list element or a map value: any type of the profile.</summary>
    internal ProfileType Target() => pcg.Pick(WeightedTargets);

    /// <summary>A literal of a type of the profile.</summary>
    /// <param name="type">A scalar type or <c>null_type</c>.</param>
    internal Expr Literal(ProfileType type) => type switch
    {
        ProfileType.IntType => new IntLiteral(ProfileValues.IntValue(pcg), pcg.Pick(Enum.GetValues<IntegerForm>())),
        ProfileType.UintType => new UintLiteral(ProfileValues.UintValue(pcg), pcg.Pick(Enum.GetValues<IntegerForm>()), pcg.OneIn(2) ? 'u' : 'U'),
        ProfileType.DoubleType => new DoubleLiteral(ProfileValues.DoubleValue(pcg), pcg.Pick(Enum.GetValues<DoubleForm>())),
        ProfileType.BoolType => new BoolLiteral(pcg.OneIn(2)),
        ProfileType.StringType => StringLiteral(ProfileValues.StringValue(pcg)),
        ProfileType.BytesType => BytesLiteral(ProfileValues.BytesValue(pcg)),
        ProfileType.NullType => new NullLiteral(),
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "The type has no literal."),
    };

    /// <summary>
    /// The identity of a map key under CEL equality: equal keys have equal identities. Numbers compare by value across
    /// <c>int</c> and <c>uint</c>, so <c>1</c> and <c>1u</c> have one identity.
    /// </summary>
    /// <param name="key">A literal of <c>int</c>, <c>uint</c>, <c>bool</c> or <c>string</c>.</param>
    internal static string KeyIdentity(Expr key) => key switch
    {
        IntLiteral i => "number " + new BigInteger(i.Value).ToString(CultureInfo.InvariantCulture),
        UintLiteral u => "number " + new BigInteger(u.Value).ToString(CultureInfo.InvariantCulture),
        BoolLiteral b => b.Value ? "bool true" : "bool false",
        StringLiteral s => "string " + s.Value,
        _ => throw new ArgumentOutOfRangeException(nameof(key), key, "Not a key of the profile."),
    };

    private Expr BuildScalar(ProfileType type)
    {
        string? variable = type switch
        {
            ProfileType.UintType => "u1",
            ProfileType.DoubleType => "d1",
            ProfileType.StringType => "s1",
            ProfileType.BytesType => "y1",
            _ => null,
        };
        return variable is not null && pcg.OneIn(2) ? new Ident(variable) : Literal(type);
    }

    // int: a literal, i1, i2, or a call of _+_, _/_ or -_ on int operands. A dyn variable is an operand of _+_ and _/_ only
    // next to an operand of the static type int, and never of -_.
    private (Expr, int) BuildInt(int depth, int limit)
    {
        int choice = depth < ProfileTables.MaxDepth && limit >= 3 ? pcg.Below(5) : pcg.Below(2);
        switch (choice)
        {
            case 0:
                return (Literal(ProfileType.IntType), 1);
            case 1:
                return (new Ident(pcg.Pick(IntVariables)), 1);
            case 2:
                (Expr operand, int nodes) = Build(ProfileType.IntType, depth + 1, limit - 1);
                return (new Call(ProfileTables.Negate, [operand]), nodes + 1);
            default:
                return BuildBinaryInt(choice == 3 ? ProfileTables.Add : ProfileTables.Divide, depth, limit);
        }
    }

    private (Expr, int) BuildBinaryInt(string function, int depth, int limit)
    {
        if (pcg.OneIn(DynOperandOneIn))
        {
            Expr dyn = Maybe(new Ident(pcg.Pick(DynVariables)));
            (Expr other, int otherNodes) = Build(ProfileType.IntType, depth + 1, limit - 2);
            Expr[] args = pcg.OneIn(2) ? [dyn, other] : [other, dyn];
            return (new Call(function, args), otherNodes + 2);
        }

        (Expr left, int leftNodes) = Build(ProfileType.IntType, depth + 1, limit - 2);
        (Expr right, int rightNodes) = Build(ProfileType.IntType, depth + 1, limit - 1 - leftNodes);
        return (new Call(function, [left, right]), leftNodes + rightNodes + 1);
    }

    // bool: a literal, b1, b2, x1, x2, or a chain of || with 2–4 such operands, nested to the left as the parser reads it.
    private (Expr, int) BuildBool(int depth, int limit)
    {
        int longest = Math.Min(Math.Min(4, ProfileTables.MaxDepth - depth + 1), (limit + 1) / 2);
        if (longest < 2 || pcg.OneIn(2))
        {
            return (BoolAtom(), 1);
        }

        int operands = 2 + pcg.Below(longest - 1);
        Expr chain = BoolAtom();
        for (int i = 1; i < operands; i++)
        {
            chain = Maybe(new Call(ProfileTables.LogicalOr, [chain, BoolAtom()]));
        }

        return (chain, (2 * operands) - 1);
    }

    private Expr BoolAtom() => Maybe(pcg.OneIn(3) ? Literal(ProfileType.BoolType) : new Ident(pcg.Pick(BoolVariables)));

    // Parentheses around any node at random.
    private Expr Maybe(Expr expr) => pcg.OneIn(ParenthesesOneIn) ? expr with { Parenthesized = true } : expr;

    private (Expr, int) BuildList(int depth, int limit)
    {
        int most = depth < ProfileTables.MaxDepth ? Math.Min(ProfileTables.MaxElements, limit - 1) : 0;
        int count = pcg.Below(most + 1);
        List<Expr> elements = [];
        int nodes = 1;
        for (int i = 0; i < count; i++)
        {
            (Expr element, int elementNodes) = Build(Target(), depth + 1, limit - nodes - (count - 1 - i));
            elements.Add(element);
            nodes += elementNodes;
        }

        return (new ListLiteral(elements, TrailingComma()), nodes);
    }

    // Map keys are literals of int, uint, bool and string, pairwise unequal by CEL equality; a key equal to an earlier one is
    // drawn again, and after a few draws the entry is left out.
    private (Expr, int) BuildMap(int depth, int limit)
    {
        int most = depth < ProfileTables.MaxDepth ? Math.Min(ProfileTables.MaxElements, (limit - 1) / 2) : 0;
        int count = pcg.Below(most + 1);
        List<MapEntry> entries = [];
        HashSet<string> keys = new(StringComparer.Ordinal);
        int nodes = 1;
        for (int i = 0; i < count; i++)
        {
            Expr? key = DistinctKey(keys);
            if (key is null)
            {
                continue;
            }

            (Expr value, int valueNodes) = Build(Target(), depth + 1, limit - nodes - 1 - (2 * (count - 1 - i)));
            entries.Add(new MapEntry(key, value));
            nodes += 1 + valueNodes;
        }

        return (new MapLiteral(entries, TrailingComma()), nodes);
    }

    // A list or map literal sometimes ends with a comma, also without elements: [,] and {,}.
    private bool TrailingComma() => pcg.OneIn(TrailingCommaOneIn);

    private Expr? DistinctKey(HashSet<string> keys)
    {
        for (int attempt = 0; attempt < 4; attempt++)
        {
            Expr key = Literal(pcg.Pick(ProfileTables.KeyTypes));
            if (keys.Add(KeyIdentity(key)))
            {
                return Maybe(key);
            }
        }

        return null;
    }

    private StringLiteral StringLiteral(string value)
    {
        Quote quote = pcg.Pick(Enum.GetValues<Quote>());
        char? raw = RawPrefix();
        List<int> codePoints = [];
        foreach (Rune rune in value.EnumerateRunes())
        {
            codePoints.Add(rune.Value);
        }

        if (raw is not null && !codePoints.TrueForAll(codePoint => TextUnits.CanBeLiteral(codePoint, quote, raw: true)))
        {
            raw = null;
        }

        List<Escape> units = [];
        foreach (int codePoint in codePoints)
        {
            units.Add(PickForm(TextUnits.StringForms(codePoint, quote, raw is not null)));
        }

        return new StringLiteral(value, new TextForm(quote, raw, units));
    }

    private BytesLiteral BytesLiteral(byte[] value)
    {
        Quote quote = pcg.Pick(Enum.GetValues<Quote>());
        char? raw = RawPrefix();
        if (raw is not null && !AllLiteral(value, quote))
        {
            raw = null;
        }

        List<Escape> units = [];
        for (int index = 0; index < value.Length;)
        {
            Escape escape = PickForm(TextUnits.BytesForms(value, index, quote, raw is not null));
            units.Add(escape);
            int length = 1;
            if (escape == Escape.Literal)
            {
                TextUnits.LiteralCodePoint(value, index, out _, out length);
            }

            index += length;
        }

        return new BytesLiteral(value, pcg.OneIn(2) ? 'b' : 'B', new TextForm(quote, raw, units));
    }

    private static bool AllLiteral(byte[] value, Quote quote)
    {
        for (int index = 0; index < value.Length;)
        {
            if (!TextUnits.LiteralCodePoint(value, index, out int codePoint, out int length) || !TextUnits.CanBeLiteral(codePoint, quote, raw: true))
            {
                return false;
            }

            index += length;
        }

        return true;
    }

    private char? RawPrefix() => pcg.Below(4) switch
    {
        0 => 'r',
        1 => 'R',
        _ => null,
    };

    // A unit stands as itself half of the time when it may.
    private Escape PickForm(List<Escape> forms) =>
        forms[0] == Escape.Literal && pcg.OneIn(2) ? Escape.Literal : pcg.Pick(forms);
}
