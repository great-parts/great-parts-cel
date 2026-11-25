using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using CsCheck;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Library.Extensions;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Properties.Library;

// Hashing in distinct and in the functions of the extension set sets is the pairwise pass of version 1.0: on random lists of numbers
// of three kinds, both zeros, NaN, strings, bytes, nested lists and maps, optionals and objects of the application whose comparison
// sometimes throws, of lengths 0 to 200 and so on both sides of the length at which the library
// changes from scanning the hashes to buckets, the result and the outcome with an error are those of the pass over every pair, the code
// of the application is called in the same order with the same pairs, and the cost is the formula of the page of cost: the
// hashing of every element, and the comparisons of the pairs that are equal, that read the application or that hold a number of
// 2^53 or more, never those that only share a hash. The formula is computed here from the values, by the comparison of the library
// on single pairs, so it does not depend on the seed of the hash of the process. A random seed hardly gives two values one hash, so
// the same lists go again through the functions with the hash of every value made one: the cost, the result, the outcome with an
// error and the calls of the application are those of the ordinary hash. The index scans the hashes of a short list and uses buckets
// for a longer one; the lists go again through the functions with each of the two ways forced for a list of any length, with the
// ordinary hash and with the constant one: the cost, the result, the outcome with an error and the calls of the application are the
// same in all of them.
public sealed class HashedListsTests
{
    private const ulong Seed = 20261119;
    private const int Cases = 400;

    private static readonly BoxType Boxes = new();

    private static readonly LineTable Lines = new("x");

    private static readonly CelEnvironment ListEnvironment = new CelEnvironmentBuilder(CelStandardLibrary.V1)
        .AddExtension(CelExtensions.Lists(4))
        .AddExtension(CelExtensions.Sets(0))
        .AddTypes(new BoxProvider(Boxes))
        .AddVariable("x", CelType.Dyn)
        .AddVariable("p", CelType.Dyn)
        .Build()
        .GetEnvironmentOrThrow();

    private static readonly bool[] Hashes = [false, true];

    private static readonly ValueIndexMode[] Modes = [ValueIndexMode.Automatic, ValueIndexMode.Scan, ValueIndexMode.Buckets];

    private static readonly string[] Words =["", "a", "b", "ab", "aaaaaaaaaaaaaaaaaaaaaaaaa", "é"];

    private static readonly double[] Doubles = [0.0, -0.0, 1.0, 2.0, 0.5, 1.5, double.PositiveInfinity, double.NaN, double.NaN];

    // 2^53: the numbers of this size or more in absolute value are compared pairwise and not hashed.
    private const long ExactLimit = 1L << 53;

    // The values that stand for numbers of one value in different kinds, and the numbers that round to one double.
    private static readonly long[] Integers = [0, 1, 2, 3, long.MaxValue, 9007199254740992, 9007199254740993];

    private static readonly Gen<ListsCase> Generator = Gen.Select(Gen.UInt, Gen.ULong, (stream, seed) => Create(new PCG(stream, seed)));

    [Fact]
    [Trait("Requirement", "R-3")]
    [Trait("Requirement", "R-4")]
    [Trait("Requirement", "R-9")]
    public void SameAsPairwise() =>
        PropertyLoop.Check(nameof(SameAsPairwise), Generator, Seed, Cases, Violation, testCase => testCase.Text);

    private static string? Violation(ListsCase testCase)
    {
        CelValue[] first = testCase.First;
        CelValue[] second = testCase.Second;
        return Distinct(first)
            ?? Sets("sets.contains(x, p)", first, second)
            ?? Sets("sets.contains(p, x)", first, second)
            ?? Sets("sets.intersects(x, p)", first, second)
            ?? Sets("sets.equivalent(x, p)", first, second)
            ?? Sets("sets.equivalent(x, x)", first, first)
            ?? OtherWays(first, second);
    }

    // The functions of the library on the two lists with the ordinary hash and with a constant one, which puts every value of the
    // group Hashed in one bucket, each with the index that scans the hashes and with the one that uses buckets: the same outcome, the
    // same cost and the same calls of the application.
    private static string? OtherWays(CelValue[] first, CelValue[] second)
    {
        CelList left = CelList.Create(first);
        CelList right = CelList.Create(second);
        CelValue argument = CelValue.FromList(left);
        (string Name, Func<CallSite, bool, ValueIndexMode, CelValue> Run)[] functions =
        [
            ("distinct", (site, constant, mode) => HashedLists.Distinct(argument, left, in site, constantHash: constant, mode: mode)),
            ("contains", (site, constant, mode) => HashedLists.AllIn(right, left, in site, constantHash: constant, mode: mode)),
            ("intersects", (site, constant, mode) => HashedLists.Intersects(left, right, in site, constantHash: constant, mode: mode)),
            ("equivalent", (site, constant, mode) =>
            {
                CelValue forward = HashedLists.AllIn(right, left, in site, constantHash: constant, mode: mode);
                return forward.TryGetBoolean(out bool holds) && holds ? HashedLists.AllIn(left, right, in site, constantHash: constant, mode: mode) : forward;
            }),
        ];
        foreach ((string name, Func<CallSite, bool, ValueIndexMode, CelValue> run) in functions)
        {
            Outcome ordinary = Observe(run, constant: false, mode: ValueIndexMode.Automatic);
            foreach (bool constantHash in Hashes)
            {
                foreach (ValueIndexMode mode in Modes)
                {
                    Outcome other = Observe(run, constantHash, mode);
                    if (!ordinary.Equals(other))
                    {
                        return name + " with " + (constantHash ? "a constant" : "the ordinary") + " hash and the mode " + mode + ": " + other + "; with the ordinary hash and the automatic mode: " + ordinary;
                    }
                }
            }
        }

        return null;
    }

    // One call of a function of the library with a meter of its own, through a call site; the exception of the application ends it.
    private static Outcome Observe(Func<CallSite, bool, ValueIndexMode, CelValue> run, bool constant, ValueIndexMode mode)
    {
        Boxes.Calls.Clear();
        var meter = new CountingMeter();
        string? result = null;
        string? thrown = null;
        try
        {
            result = Rendered(run(new CallSite("f", 0, Lines, meter), constant, mode));
        }
        catch (InvalidOperationException exception)
        {
            thrown = exception.Message;
        }

        return new Outcome(result, thrown, meter.Units, string.Join(';', Boxes.Calls));
    }

    private static string? Distinct(CelValue[] items)
    {
        Boxes.Calls.Clear();
        Reference reference = Pairwise.Distinct(items);
        List<string> expectedCalls = [.. Boxes.Calls];
        Boxes.Calls.Clear();
        CelEvalResult<CelValue> actual = Run("x.distinct()", items, null);

        string? mismatch = Compared("distinct", reference, actual, expectedCalls);
        if (mismatch is not null || reference.Thrown is not null)
        {
            return mismatch;
        }

        if (!actual.GetValueOrThrow().TryGetList(out CelList? list) || list.Count != reference.Kept!.Count)
        {
            return "distinct: the result has the wrong size";
        }

        for (int i = 0; i < list.Count; i++)
        {
            if (!list[i].Equals(reference.Kept[i]))
            {
                return "distinct: the element " + i.ToString(CultureInfo.InvariantCulture) + " of the result is not that of the pairwise pass";
            }
        }

        // The read of the variable and the unit of the call, and the formula.
        ulong expected = 2 + Formula.Distinct(items);
        return CostMismatch("distinct", expected, actual);
    }

    private static string? Sets(string text, CelValue[] first, CelValue[] second)
    {
        bool equivalent = text.StartsWith("sets.equivalent", StringComparison.Ordinal);
        bool intersects = text.StartsWith("sets.intersects", StringComparison.Ordinal);
        bool swapped = text.Contains("(p, x)", StringComparison.Ordinal);
        CelValue[] left = swapped ? second : first;
        CelValue[] right = swapped ? first : second;

        Boxes.Calls.Clear();
        Reference reference = equivalent ? Pairwise.Equivalent(left, right) : intersects ? Pairwise.Intersects(left, right) : Pairwise.Contains(left, right);
        List<string> expectedCalls = [.. Boxes.Calls];
        Boxes.Calls.Clear();
        CelEvalResult<CelValue> actual = Run(text, first, second);

        string? mismatch = Compared(text, reference, actual, expectedCalls);
        if (mismatch is not null || reference.Thrown is not null)
        {
            return mismatch;
        }

        if (!actual.GetValueOrThrow().TryGetBoolean(out bool value) || value != reference.Result)
        {
            return text + ": the result is not that of the pairwise pass";
        }

        // The reads of the two variables and the unit of the call, and the sums of the searches.
        ulong expected = 3 + (equivalent ? Formula.Equivalent(left, right) : intersects ? Formula.Intersects(left, right) : Formula.Contains(left, right));
        return CostMismatch(text, expected, actual);
    }

    // The result, the error with its exception, and the calls of the code of the application: the same as of the pairwise pass.
    private static string? Compared(string what, Reference reference, CelEvalResult<CelValue> actual, List<string> expectedCalls)
    {
        if (!expectedCalls.SequenceEqual(Boxes.Calls))
        {
            return what + ": the code of the application was called as [" + string.Join("; ", Boxes.Calls) + "], the pairwise pass calls [" + string.Join("; ", expectedCalls) + "]";
        }

        if (reference.Thrown is null)
        {
            return actual.IsSuccess ? null : what + ": the evaluation failed with " + actual.Errors[0].Code + ", the pairwise pass does not fail";
        }

        if (actual.IsSuccess)
        {
            return what + ": the evaluation succeeded, the pairwise pass throws " + reference.Thrown.Message;
        }

        CelError error = actual.Errors[0];
        return actual.Errors.Length == 1 && error.Code == CelErrorCode.ApplicationException
            && string.Equals(error.Exception?.Message, reference.Thrown.Message, StringComparison.Ordinal)
            ? null
            : what + ": the error is " + error.Code + " " + error.Exception?.Message + ", the pairwise pass throws " + reference.Thrown.Message;
    }

    private static string? CostMismatch(string what, ulong expected, CelEvalResult<CelValue> actual) =>
        actual.Cost == expected
            ? null
            : what + ": the cost is " + actual.Cost?.ToString(CultureInfo.InvariantCulture) + ", the table of cost gives " + expected.ToString(CultureInfo.InvariantCulture);

    private static CelEvalResult<CelValue> Run(string text, CelValue[] first, CelValue[]? second)
    {
        CelBindings bindings = new CelBindings().Add("x", CelValue.FromList(CelList.Create(first)));
        if (second is not null)
        {
            bindings.Add("p", CelValue.FromList(CelList.Create(second)));
        }

        return ListEnvironment.Compile<CelValue>(text).GetProgramOrThrow()
            .Evaluate(bindings, new CelEvalOptions { CostLimit = ulong.MaxValue }, CancellationToken.None);
    }

    private static ListsCase Create(PCG random)
    {
        CelValue[] first = Elements(random, Length(random));
        CelValue[] second = Elements(random, Length(random));
        var text = new StringBuilder();
        text.Append("x = ").Append(Rendered(first)).Append("; p = ").Append(Rendered(second));
        return new ListsCase(first, second, text.ToString());
    }

    // Lengths on both sides of the 32 elements at which the library changes from the scan to buckets, up to 64 and beyond.
    private static int Length(PCG random) => random.Next(6) switch
    {
        0 => (int)random.Next(8),
        1 or 2 => 8 + (int)random.Next(23),
        3 => 24 + (int)random.Next(41),
        4 => 40 + (int)random.Next(81),
        _ => 100 + (int)random.Next(101),
    };

    private static CelValue[] Elements(PCG random, int length)
    {
        var items = new CelValue[length];
        for (int i = 0; i < items.Length; i++)
        {
            items[i] = Value(random, 2);
        }

        return items;
    }

    private static CelValue Value(PCG random, int depth)
    {
        switch (random.Next(depth > 0 ? 16u : 11u))
        {
            case 0 or 1:
                return CelValue.FromInt64(Number(random));
            case 2:
                return CelValue.FromUInt64((ulong)Number(random));
            case 3:
                return CelValue.FromDouble(Doubles[random.Next((uint)Doubles.Length)]);
            case 4:
                return CelValue.FromString(Words[random.Next((uint)Words.Length)]);
            case 5:
                return CelValue.FromBytes(Encoding.UTF8.GetBytes(Words[random.Next((uint)Words.Length)]));
            case 6:
                return CelValue.FromBoolean(random.Next(2) == 0);
            case 7:
                return CelValue.Null;
            case 8:
                return CelValue.FromDouble(Number(random));
            case 9 or 10:
                return CelValue.FromObject(Boxes, new Box(random.Next(6)));
            case 11 or 12:
                return CelValue.FromList(CelList.Create(Elements(random, (int)random.Next(4), depth - 1)));
            case 13 or 14:
                return Map(random, depth - 1);
            default:
                return random.Next(4) == 0
                    ? CelValue.FromOptional(default)
                    : CelValue.FromOptional(new CelOptional<CelValue>(Value(random, depth - 1)));
        }
    }

    private static CelValue[] Elements(PCG random, int length, int depth)
    {
        var items = new CelValue[length];
        for (int i = 0; i < items.Length; i++)
        {
            items[i] = Value(random, depth);
        }

        return items;
    }

    private static long Number(PCG random) => Integers[random.Next((uint)Integers.Length)];

    // A map of up to three string keys, in a random order, so that two maps of one content are often written in two orders.
    private static CelValue Map(PCG random, int depth)
    {
        string[] keys = ["k0", "k1", "k2"];
        for (int i = keys.Length - 1; i > 0; i--)
        {
            int other = (int)random.Next((uint)i + 1);
            (keys[i], keys[other]) = (keys[other], keys[i]);
        }

        var entries = new List<KeyValuePair<CelValue, CelValue>>();
        int count = (int)random.Next(4);
        for (int i = 0; i < count; i++)
        {
            entries.Add(new KeyValuePair<CelValue, CelValue>(CelValue.FromString(keys[i]), Value(random, depth)));
        }

        return CelValue.FromMap(CelMap.Create(entries));
    }

    private static string Rendered(CelValue[] items)
    {
        var text = new StringBuilder("[");
        for (int i = 0; i < items.Length; i++)
        {
            text.Append(i > 0 ? ", " : string.Empty).Append(Rendered(items[i]));
        }

        return text.Append(']').ToString();
    }

    private static string Rendered(CelValue value)
    {
        switch (value.Kind)
        {
            case CelValueKind.List:
                _ = value.TryGetList(out CelList? list);
                return Rendered([.. list!]);
            case CelValueKind.Map:
                _ = value.TryGetMap(out CelMap? map);
                var text = new StringBuilder("{");
                foreach (KeyValuePair<CelValue, CelValue> entry in map!)
                {
                    text.Append(Rendered(entry.Key)).Append(": ").Append(Rendered(entry.Value)).Append(", ");
                }

                return text.Append('}').ToString();
            case CelValueKind.Optional:
                _ = value.TryGetOptional(out CelOptional<CelValue> optional);
                return optional.HasValue ? "?(" + Rendered(optional.Value) + ")" : "?()";
            case CelValueKind.Object:
                _ = value.TryGetObject(out object? instance);
                return "box" + ((Box)instance!).Id.ToString(CultureInfo.InvariantCulture);
            default:
                return value.Kind + ":" + value;
        }
    }

    // The pairwise pass of version 1.0, as the code of that version had it: the equality of the library on each pair in the order of
    // the pairs; an exception of the application stops the pass and is the outcome.
    private static class Pairwise
    {
        internal static Reference Distinct(CelValue[] items)
        {
            var kept = new List<CelValue>();
            foreach (CelValue item in items)
            {
                bool found = false;
                foreach (CelValue other in kept)
                {
                    ValueEquality result = Compare(item, other, null, out Exception? thrown);
                    if (thrown is not null)
                    {
                        return new Reference(null, false, thrown);
                    }

                    if (result == ValueEquality.Equal)
                    {
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    kept.Add(item);
                }
            }

            return new Reference(kept, false, null);
        }

        // Whether every needle is in the haystack: the needles in order, each looked up in the haystack in order.
        internal static Reference Contains(CelValue[] haystack, CelValue[] needles) => AllIn(needles, haystack);

        internal static Reference Intersects(CelValue[] needles, CelValue[] haystack)
        {
            foreach (CelValue needle in needles)
            {
                (bool found, Exception? thrown) = Lookup(needle, haystack, null);
                if (thrown is not null)
                {
                    return new Reference(null, false, thrown);
                }

                if (found)
                {
                    return new Reference(null, true, null);
                }
            }

            return new Reference(null, false, null);
        }

        internal static Reference Equivalent(CelValue[] first, CelValue[] second)
        {
            Reference forward = AllIn(second, first);
            return forward.Thrown is null && forward.Result ? AllIn(first, second) : forward;
        }

        private static Reference AllIn(CelValue[] needles, CelValue[] haystack)
        {
            foreach (CelValue needle in needles)
            {
                (bool found, Exception? thrown) = Lookup(needle, haystack, null);
                if (thrown is not null)
                {
                    return new Reference(null, false, thrown);
                }

                if (!found)
                {
                    return new Reference(null, false, null);
                }
            }

            return new Reference(null, true, null);
        }

        internal static (bool Found, Exception? Thrown) Lookup(CelValue needle, CelValue[] haystack, CountingMeter? meter)
        {
            foreach (CelValue item in haystack)
            {
                ValueEquality result = Compare(needle, item, meter, out Exception? thrown);
                if (thrown is not null)
                {
                    return (false, thrown);
                }

                if (result == ValueEquality.Equal)
                {
                    return (true, null);
                }
            }

            return (false, null);
        }

        internal static ValueEquality Compare(CelValue left, CelValue right, CountingMeter? meter, out Exception? thrown)
        {
            thrown = null;
            try
            {
                return EqualityRules.AreEqual(left, right, meter, out _);
            }
            catch (InvalidOperationException exception)
            {
                thrown = exception;
                return ValueEquality.Error;
            }
        }
    }

    // The cost of the page of cost, without the units of the nodes around the call: the hashing of the values, and the comparisons that
    // the table names.
    private static class Formula
    {
        internal static ulong Distinct(CelValue[] items)
        {
            ulong units = (ulong)items.Length;
            var hashed = new List<CelValue>();
            var pairwise = new List<CelValue>();
            foreach (CelValue item in items)
            {
                units += Hashing(item);
                switch (GroupOf(item))
                {
                    case Group.Hashed:
                        if (!TryFirstEqual(item, hashed, ref units))
                        {
                            hashed.Add(item);
                        }

                        break;
                    case Group.Pairwise:
                        var meter = new CountingMeter();
                        (bool found, _) = Pairwise.Lookup(item, [.. pairwise], meter);
                        units += meter.Units;
                        if (!found)
                        {
                            pairwise.Add(item);
                        }

                        break;
                    default:
                        break;
                }
            }

            return units;
        }

        // 1 for the call and the sum of the search (the table of cost, rows contains and intersects).
        internal static ulong Contains(CelValue[] haystack, CelValue[] needles) => Search(needles, haystack, stopAtFirstFound: false);

        internal static ulong Intersects(CelValue[] needles, CelValue[] haystack) => Search(needles, haystack, stopAtFirstFound: true);

        internal static ulong Equivalent(CelValue[] first, CelValue[] second)
        {
            ulong units = Search(second, first, stopAtFirstFound: false);
            Reference forward = Pairwise.Contains(first, second);
            return forward.Result ? units + Search(first, second, stopAtFirstFound: false) : units;
        }

        // The hashing of the whole haystack, and for each needle in order up to the first that decides: its hashing, 1, and the comparisons.
        private static ulong Search(CelValue[] needles, CelValue[] haystack, bool stopAtFirstFound)
        {
            if (needles.Length == 0)
            {
                return 0;
            }

            ulong units = 0;
            var hashed = new List<CelValue>();
            var pairwise = new List<CelValue>();
            foreach (CelValue item in haystack)
            {
                units += Hashing(item);
                switch (GroupOf(item))
                {
                    case Group.Hashed:
                        hashed.Add(item);
                        break;
                    case Group.Pairwise:
                        pairwise.Add(item);
                        break;
                    default:
                        break;
                }
            }

            foreach (CelValue needle in needles)
            {
                units += Hashing(needle) + 1;
                bool found = false;
                switch (GroupOf(needle))
                {
                    case Group.Hashed:
                        found = TryFirstEqual(needle, hashed, ref units);
                        break;
                    case Group.Pairwise:
                        var meter = new CountingMeter();
                        (found, _) = Pairwise.Lookup(needle, [.. pairwise], meter);
                        units += meter.Units;
                        break;
                    default:
                        break;
                }

                if (found == stopAtFirstFound)
                {
                    break;
                }
            }

            return units;
        }

        // Looks for the first value equal to the one given and adds the one comparison that found it.
        private static bool TryFirstEqual(CelValue value, List<CelValue> candidates, ref ulong units)
        {
            foreach (CelValue candidate in candidates)
            {
                if (Pairwise.Compare(value, candidate, null, out _) == ValueEquality.Equal)
                {
                    var meter = new CountingMeter();
                    _ = Pairwise.Compare(value, candidate, meter, out _);
                    units += meter.Units;
                    return true;
                }
            }

            return false;
        }

        // h of the table: 1 for a node, ⌈L/10⌉ more for a string or bytes, the elements of a list, the keys and values of a map, the
        // value of an optional; an object is one node.
        private static ulong Hashing(CelValue value)
        {
            switch (value.Kind)
            {
                case CelValueKind.String:
                    _ = value.TryGetString(out string? text);
                    return 1 + (((ulong)text!.Length + 9) / 10);
                case CelValueKind.Bytes:
                    _ = value.TryGetBytes(out ReadOnlyMemory<byte> bytes);
                    return 1 + (((ulong)bytes.Length + 9) / 10);
                case CelValueKind.List:
                    _ = value.TryGetList(out CelList? list);
                    ulong sum = 1;
                    foreach (CelValue element in list!)
                    {
                        sum += Hashing(element);
                    }

                    return sum;
                case CelValueKind.Map:
                    _ = value.TryGetMap(out CelMap? map);
                    ulong total = 1;
                    foreach (KeyValuePair<CelValue, CelValue> entry in map!)
                    {
                        total += Hashing(entry.Key) + Hashing(entry.Value);
                    }

                    return total;
                case CelValueKind.Optional:
                    _ = value.TryGetOptional(out CelOptional<CelValue> optional);
                    return optional.HasValue ? 1 + Hashing(optional.Value) : 1;
                default:
                    return 1;
            }
        }

        // The group of a value: an object or a number of 2^53 or more in absolute value at any depth (an infinity is one), else a NaN
        // at any depth, else the rest.
        private static Group GroupOf(CelValue value)
        {
            bool hasObject = false;
            bool hasNaN = false;
            Visit(value, ref hasObject, ref hasNaN);
            return hasObject ? Group.Pairwise : hasNaN ? Group.Single : Group.Hashed;
        }

        private static void Visit(CelValue value, ref bool hasObject, ref bool hasNaN)
        {
            switch (value.Kind)
            {
                case CelValueKind.Object:
                    hasObject = true;
                    break;
                case CelValueKind.Int:
                    hasObject |= value.TryGetInt64(out long integer) && (integer >= ExactLimit || integer <= -ExactLimit);
                    break;
                case CelValueKind.UInt:
                    hasObject |= value.TryGetUInt64(out ulong unsigned) && unsigned >= ExactLimit;
                    break;
                case CelValueKind.Double:
                    _ = value.TryGetDouble(out double number);
                    hasNaN |= double.IsNaN(number);
                    hasObject |= Math.Abs(number) >= ExactLimit;
                    break;
                case CelValueKind.List:
                    _ = value.TryGetList(out CelList? list);
                    foreach (CelValue element in list!)
                    {
                        Visit(element, ref hasObject, ref hasNaN);
                    }

                    break;
                case CelValueKind.Map:
                    _ = value.TryGetMap(out CelMap? map);
                    foreach (KeyValuePair<CelValue, CelValue> entry in map!)
                    {
                        Visit(entry.Key, ref hasObject, ref hasNaN);
                        Visit(entry.Value, ref hasObject, ref hasNaN);
                    }

                    break;
                case CelValueKind.Optional:
                    _ = value.TryGetOptional(out CelOptional<CelValue> optional);
                    if (optional.HasValue)
                    {
                        Visit(optional.Value, ref hasObject, ref hasNaN);
                    }

                    break;
                default:
                    break;
            }
        }
    }

    private enum Group
    {
        Hashed,
        Pairwise,
        Single,
    }

    // The outcome of a pairwise pass: the kept elements of distinct or the boolean of a function of sets, or the exception of the application.
    private sealed class Reference(List<CelValue>? kept, bool result, Exception? thrown)
    {
        internal List<CelValue>? Kept { get; } = kept;

        internal bool Result { get; } = result;

        internal Exception? Thrown { get; } = thrown;
    }

    // What one call gave: the rendered result or the message of the exception of the application, the units charged, and the pairs of
    // the application that were compared, in order.
    private sealed record Outcome(string? Result, string? Thrown, ulong Units, string Calls);

    private sealed class ListsCase(CelValue[] first, CelValue[] second, string text)
    {
        internal CelValue[] First { get; } = first;

        internal CelValue[] Second { get; } = second;

        internal string Text { get; } = text;
    }

    // The cost that a comparison charges.
    internal sealed class CountingMeter : IEvaluationMeter
    {
        internal ulong Units { get; private set; }

        public void Charge(ulong units) => Units += units;

        public void Event()
        {
        }
    }

    private sealed class Box(long id)
    {
        internal long Id { get; } = id;
    }

    private sealed class BoxProvider(CelObjectType type) : CelTypeProvider("box", [type], []);

    // A description whose equality is its own, charged 3 units, and sometimes throws: the application code of the property.
    private sealed class BoxType : CelObjectType
    {
        internal BoxType()
            : base("acme.test.Box", typeof(Box), [new CelObjectField(0, "id", CelType.Int)])
        {
        }

        internal List<string> Calls { get; } = [];

        protected internal override CelValue GetField(object instance, int number) => CelValue.FromInt64(((Box)instance).Id);

        protected internal override bool HasField(object instance, int number) => true;

        protected internal override long? EqualityCost(object left, object right) => 3;

        protected internal override CelValue CelEquals(object left, object right)
        {
            long a = ((Box)left).Id;
            long b = ((Box)right).Id;
            Calls.Add(a.ToString(CultureInfo.InvariantCulture) + "," + b.ToString(CultureInfo.InvariantCulture));
            if (a != b && (a + b) % 5 == 4)
            {
                throw new InvalidOperationException("pair " + a.ToString(CultureInfo.InvariantCulture) + "," + b.ToString(CultureInfo.InvariantCulture));
            }

            return CelValue.FromBoolean(a == b);
        }
    }
}
