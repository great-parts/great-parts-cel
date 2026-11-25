using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading;
using CsCheck;
using Great.Parts.Cel.Checking;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Evaluation;
using Great.Parts.Cel.Properties.Generators;
using Great.Parts.Cel.Syntax;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Properties.Evaluation;

/// <summary>A generated expression with the bindings it is evaluated on and the numbers that choose its cost limits.</summary>
/// <param name="Text">The text of the expression.</param>
/// <param name="CrossTypes">Whether the environment enables the ordering across the numeric types.</param>
/// <param name="Bindings">The values of the variables; some variables are unbound.</param>
/// <param name="BindingsText">The bindings as text, for failure messages.</param>
/// <param name="LimitDraw">A number that chooses one of the limits of the case.</param>
internal sealed record FusedCase(string Text, bool CrossTypes, CelBindings Bindings, string BindingsText, uint LimitDraw)
{
    public override string ToString() =>
        string.Create(CultureInfo.InvariantCulture, $"{Text} on {BindingsText}{(CrossTypes ? " with cross-type numeric comparisons" : string.Empty)}, limit draw {LimitDraw}");
}

/// <summary>
/// The fused node of a field and a comparison with a literal equals the pair of nodes (docs/design/evaluation.md, "Fused nodes"): each
/// expression of the corpus is planned with the fusion and without it, and evaluated on the same bindings without a cost limit and with
/// limits — one that is never reached, the sum, the sum less one, half of it and a drawn one. The value, the error with its code, message
/// and place, and the units charged before the end (the sum up to a refusal) are the same.
/// </summary>
public sealed class FusedNodeEquivalenceTests
{
    private const ulong Seed = 20261125;
    private const int Cases = 1500;

    private static readonly CelEnvironment Standard = Declare(new CelEnvironmentBuilder(CelStandardLibrary.V1)).Build().GetEnvironmentOrThrow();

    private static readonly CelEnvironment CrossTypes =
        Declare(new CelEnvironmentBuilder(CelStandardLibrary.V1).EnableCrossTypeNumericComparisons()).Build().GetEnvironmentOrThrow();

    private static readonly Gen<FusedCase> Generator = Gen.Select(Gen.UInt, Gen.ULong, (stream, seed) => Create(new PCG(stream, seed)));

    private static readonly string[] Keys = ["a", "b", "c", "d", "e", "f"];
    private static readonly string[] JsonKeys = ["a", "b", "c", "e", "n", "z", "q"];
    private static readonly string[] Operators = ["==", "!=", "<", "<=", ">", ">="];
    private static readonly string[] Integers = ["0", "1", "2", "-1", "7", "9223372036854775807"];
    private static readonly string[] Unsigned = ["0u", "1u", "3u"];
    private static readonly string[] Doubles = ["0.0", "1.5", "2.0", "1e300"];
    private static readonly string[] Strings = ["\"\"", "\"a\"", "\"ab\"", "\"x\"", "\"é😀\""];
    private static readonly long[] IntegerValues = [0, 1, 2, -1, 7, long.MaxValue, long.MinValue];
    private static readonly ulong[] UnsignedValues = [0, 1, 3, ulong.MaxValue];
    private static readonly double[] DoubleValues = [0.0, -0.0, 1.5, 2.0, double.NaN, double.PositiveInfinity, 1e300];
    private static readonly string[] StringValues = ["", "a", "ab", "x", "é😀", "1.5"];

    // The fields of the variables that have a static type: the type decides the literals the type check accepts against them.
    private static readonly (string Field, FieldKind Kind)[] TypedFields =
    [
        ("msi.a", FieldKind.Integer), ("msi.b", FieldKind.Integer), ("o.n", FieldKind.Integer), ("o.s", FieldKind.Text),
        ("o.w", FieldKind.Wrapper), ("g.n", FieldKind.Integer), ("g.s", FieldKind.Text),
    ];

    [Fact]
    [Trait("Requirement", "R-3")]
    [Trait("Requirement", "R-4")]
    [Trait("Requirement", "R-9")]
    public void SameAsUnfused()
    {
        int[] checkedCases = [0];
        PropertyLoop.Check(nameof(SameAsUnfused), Generator, Seed, Cases, testCase => Violation(testCase, checkedCases), testCase => testCase.ToString());

        // Most of the corpus type checks: a corpus of texts that fail the check would prove nothing.
        Assert.True(
            checkedCases[0] > Cases / 4,
            string.Create(CultureInfo.InvariantCulture, $"only {checkedCases[0]} of {Cases} expressions passed the type check"));
    }

    private static CelEnvironmentBuilder Declare(CelEnvironmentBuilder builder) => builder
        .AddTypes(GeneratedTypes.Provider)
        .AddVariable("m", CelType.Dyn)
        .AddVariable("d", CelType.Dyn)
        .AddVariable("j", CelType.Dyn)
        .AddVariable("msi", CelType.MapOf(CelType.String, CelType.Int))
        .AddVariable("l", CelType.ListOf(CelType.Dyn))
        .AddVariable("b", CelType.Bool)
        .AddVariable("o", GeneratedTypes.ItemName)
        .AddVariable("g", GeneratedTypes.BagName);

    private static string? Violation(FusedCase testCase, int[] checkedCases)
    {
        CelEnvironment environment = testCase.CrossTypes ? CrossTypes : Standard;
        CelParseResult parsed = environment.Parse(testCase.Text);
        if (!parsed.IsSuccess)
        {
            return "the text does not parse";
        }

        CheckResult checkedResult = environment.Check(parsed.ParsedExpr!);
        if (!checkedResult.IsSuccess)
        {
            return null;
        }

        checkedCases[0]++;
        Plan fused = Planner.Plan(checkedResult.Expression, environment.Declarations, environment.TimeZones, fuse: true);
        Plan plain = Planner.Plan(checkedResult.Expression, environment.Declarations, environment.TimeZones, fuse: false);
        (_, ulong units) = Run(plain, testCase.Bindings, ulong.MaxValue);
        foreach (ulong? limit in Limits(units, testCase.LimitDraw))
        {
            if (Difference(Run(fused, testCase.Bindings, limit), Run(plain, testCase.Bindings, limit)) is { } reason)
            {
                return string.Create(CultureInfo.InvariantCulture, $"limit {limit?.ToString(CultureInfo.InvariantCulture) ?? "none"} (the sum is {units}): {reason}");
            }
        }

        return null;
    }

    // No limit, a limit that is never reached, the sum, the sum less one, half the sum and a drawn limit.
    private static List<ulong?> Limits(ulong units, uint draw)
    {
        List<ulong?> limits = [null, ulong.MaxValue, units, units / 2, draw % (units + 2)];
        if (units > 0)
        {
            limits.Add(units - 1);
        }

        return limits;
    }

    private static (CelValue Value, ulong Charged) Run(Plan plan, CelBindings bindings, ulong? limit)
    {
        Frame frame = plan.CreateFrame(bindings.Values, limit, CancellationToken.None);
        CelValue value = plan.Run(frame);
        return (value, frame.ChargedUnits);
    }

    private static string? Difference((CelValue Value, ulong Charged) fused, (CelValue Value, ulong Charged) plain)
    {
        if (fused.Charged != plain.Charged)
        {
            return string.Create(CultureInfo.InvariantCulture, $"{fused.Charged} units charged, {plain.Charged} without the fusion");
        }

        bool fusedFails = fused.Value.TryGetError(out CelError? fusedError);
        bool plainFails = plain.Value.TryGetError(out CelError? plainError);
        if (fusedFails != plainFails)
        {
            return $"fused {Shown(fused.Value)}, plain {Shown(plain.Value)}";
        }

        if (fusedFails)
        {
            return fusedError!.Code == plainError!.Code && fusedError.Message == plainError.Message && fusedError.Span.Equals(plainError.Span)
                ? null
                : $"fused {Shown(fused.Value)}, plain {Shown(plain.Value)}";
        }

        return StructuralEquality.Equal(fused.Value, plain.Value) ? null : $"fused {Shown(fused.Value)}, plain {Shown(plain.Value)}";
    }

    private static string Shown(CelValue value)
    {
        if (value.TryGetError(out CelError? error))
        {
            return string.Create(CultureInfo.InvariantCulture, $"error {error.Code} at {error.Span.Start.Line}:{error.Span.Start.Column}: {error.Message}");
        }

        var text = new StringBuilder();
        TypedExpressions.Print(text, value);
        return text.ToString();
    }

    private static FusedCase Create(PCG random)
    {
        bool crossTypes = random.Next(4) == 0;
        string text = new Writer(random, crossTypes).Condition(0, null);
        var bindings = new CelBindings();
        var shown = new StringBuilder("{");
        Bind(random, bindings, shown);
        return new FusedCase(text, crossTypes, bindings, shown.Append('}').ToString(), random.Next(1000));
    }

    // The variables of the environment: one in eight is unbound.
    private static void Bind(PCG random, CelBindings bindings, StringBuilder shown)
    {
        BindDynamic(random, bindings, shown, "m");
        BindDynamic(random, bindings, shown, "d");
        BindObjects(random, bindings, shown);
        if (random.Next(8) != 0)
        {
            var entries = new List<KeyValuePair<CelValue, CelValue>>();
            foreach (string key in Keys[..3])
            {
                entries.Add(new(CelValue.FromString(key), CelValue.FromInt64(IntegerValues[random.Next((uint)IntegerValues.Length)])));
            }

            CelValue map = CelValue.FromMap(CelMap.Create(entries));
            bindings.Add("msi", map);
            Append(shown, "msi", map);
        }

        if (random.Next(8) != 0)
        {
            CelValue[] items = new CelValue[random.Next(4)];
            for (int index = 0; index < items.Length; index++)
            {
                items[index] = random.Next(8) == 0 ? Scalar(random) : Fields(random);
            }

            CelValue list = CelValue.FromList(CelList.Create(items));
            bindings.Add("l", list);
            Append(shown, "l", list);
        }

        if (random.Next(8) != 0)
        {
            bindings.Add("b", CelValue.FromBoolean(random.Next(2) == 0));
        }
    }

    private static void BindObjects(PCG random, CelBindings bindings, StringBuilder shown)
    {
        if (random.Next(8) != 0)
        {
            shown.Append(", o: ").Append(GeneratedTypes.BindItem(random, bindings));
        }

        if (random.Next(8) != 0)
        {
            shown.Append(", g: ").Append(GeneratedTypes.BindBag(random, bindings));
        }

        if (random.Next(8) != 0)
        {
            shown.Append(", j: ").Append(GeneratedTypes.BindJson(random, bindings));
        }
    }

    private static void BindDynamic(PCG random, CelBindings bindings, StringBuilder shown, string name)
    {
        if (random.Next(8) == 0)
        {
            return;
        }

        CelValue value = random.Next(10) == 0 ? Scalar(random) : Fields(random);
        bindings.Add(name, value);
        Append(shown, name, value);
    }

    private static void Append(StringBuilder shown, string name, CelValue value)
    {
        shown.Append(", ").Append(name).Append(": ");
        TypedExpressions.Print(shown, value);
    }

    // A map with string keys, each of the keys a to f present in three cases of four, with a value of any scalar kind.
    private static CelValue Fields(PCG random)
    {
        var entries = new List<KeyValuePair<CelValue, CelValue>>();
        foreach (string key in Keys)
        {
            if (random.Next(4) != 0)
            {
                entries.Add(new(CelValue.FromString(key), Scalar(random)));
            }
        }

        return CelValue.FromMap(CelMap.Create(entries));
    }

    private static CelValue Scalar(PCG random) => random.Next(7) switch
    {
        0 or 1 => CelValue.FromInt64(IntegerValues[random.Next((uint)IntegerValues.Length)]),
        2 => CelValue.FromUInt64(UnsignedValues[random.Next((uint)UnsignedValues.Length)]),
        3 => CelValue.FromDouble(DoubleValues[random.Next((uint)DoubleValues.Length)]),
        4 => CelValue.FromString(StringValues[random.Next((uint)StringValues.Length)]),
        5 => random.Next(2) == 0 ? CelValue.FromBoolean(random.Next(2) == 0) : CelValue.Null,
        _ => CelValue.FromList(CelList.Create(CelValue.FromInt64(1))),
    };

    // The kind of the static type of a field, which decides the literals it can be compared with.
    private enum FieldKind
    {
        Dynamic,
        Integer,
        Text,
        Wrapper,
    }

    // Writes bool expressions of the form v.f op c, in their closed list and next to it, inside the connectives, the conditional and the
    // standard macros over the list l.
    private sealed class Writer(PCG random, bool crossTypes)
    {
        // A bool expression; element is the variable of the macro being written, or null outside a macro.
        internal string Condition(int depth, string? element)
        {
            if (depth >= 3)
            {
                return Comparison(element);
            }

            return random.Next(12) switch
            {
                4 => $"({Condition(depth + 1, element)} && {Condition(depth + 1, element)})",
                5 => $"({Condition(depth + 1, element)} || {Condition(depth + 1, element)})",
                6 => $"({Condition(depth + 1, element)} && {Condition(depth + 1, element)} && {Condition(depth + 1, element)})",
                7 => $"(b ? {Condition(depth + 1, element)} : {Condition(depth + 1, element)})",
                8 => $"!{Condition(depth + 1, element)}",
                9 when element is null => Macro(depth),
                10 => Near(element),
                _ => Comparison(element),
            };
        }

        private string Macro(int depth)
        {
            string body = Condition(depth + 1, "e");
            return random.Next(5) switch
            {
                0 => $"l.exists(e, {body})",
                1 => $"l.all(e, {body})",
                2 => $"l.exists_one(e, {body})",
                3 => $"(size(l.filter(e, {body})) > 0)",
                _ => $"l.map(e, {body}).exists(x, x)",
            };
        }

        // v.f op c: the form that the planner fuses.
        private string Comparison(string? element)
        {
            (string field, FieldKind kind) = Field(element);
            string op = Operators[random.Next((uint)Operators.Length)];
            string literal = Literal(kind, ref op);
            return $"({field} {op} {literal})";
        }

        // Forms that are not fused, next to those that are.
        private string Near(string? element)
        {
            (string field, FieldKind kind) = Field(element);
            string op = Operators[random.Next((uint)Operators.Length)];
            string literal = Literal(kind, ref op);
            return random.Next(7) switch
            {
                0 => $"({literal} {op} {field})",
                1 => $"(has({field}) == true)",
                2 => $"({field}.x {op} {literal})",
                3 => $"({field} {op} {Field(element).Field})",
                4 => $"(size({field}) {op} 1)",
                5 => $"(string({field}) {op} \"1\")",
                _ => $"(({field}) {op} {literal})",
            };
        }

        // A field of a variable of the environment, or of the variable of the macro when there is one.
        private (string Field, FieldKind Kind) Field(string? element)
        {
            uint pick = random.Next(element is null ? 9u : 13u);
            string key = Keys[random.Next((uint)Keys.Length)];
            if (pick < 4)
            {
                return ($"m.{key}", FieldKind.Dynamic);
            }

            if (pick < 5)
            {
                return ($"d.{key}", FieldKind.Dynamic);
            }

            if (pick < 6)
            {
                return ($"j.{JsonKeys[random.Next((uint)JsonKeys.Length)]}", FieldKind.Dynamic);
            }

            return pick < 9 ? TypedFields[random.Next((uint)TypedFields.Length)] : ($"{element}.{key}", FieldKind.Dynamic);
        }

        // A literal that the type check accepts against the kind of the field: null only with == and !=, so the operator is set for it.
        private string Literal(FieldKind kind, ref string op)
        {
            switch (kind)
            {
                case FieldKind.Integer:
                    return crossTypes && op is not ("==" or "!=") && random.Next(3) == 0 ? CrossLiteral() : Integers[random.Next((uint)Integers.Length)];
                case FieldKind.Text:
                    return Strings[random.Next((uint)Strings.Length)];
                case FieldKind.Wrapper:
                    return random.Next(3) == 0 ? NullLiteral(ref op) : Integers[random.Next((uint)Integers.Length)];
                default:
                    return DynamicLiteral(ref op);
            }
        }

        private string DynamicLiteral(ref string op) => random.Next(7) switch
        {
            0 => Integers[random.Next((uint)Integers.Length)],
            1 => Unsigned[random.Next((uint)Unsigned.Length)],
            2 => Doubles[random.Next((uint)Doubles.Length)],
            3 => Strings[random.Next((uint)Strings.Length)],
            4 => random.Next(2) == 0 ? "true" : "false",
            5 => NullLiteral(ref op),
            _ => Integers[random.Next((uint)Integers.Length)],
        };

        private string CrossLiteral() => random.Next(2) == 0 ? Doubles[random.Next((uint)Doubles.Length)] : Unsigned[random.Next((uint)Unsigned.Length)];

        private static string NullLiteral(ref string op)
        {
            op = op is "==" or "!=" ? op : "==";
            return "null";
        }
    }
}
