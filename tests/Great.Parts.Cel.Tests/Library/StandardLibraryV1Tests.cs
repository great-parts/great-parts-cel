using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Text;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Library;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Library;

// The table of the standard library, version 1: each overload with its function,
// identifier, signature and cost, in the order of cel-go.
public sealed class StandardLibraryV1Tests
{
    // "function | identifier | signature | cost", and "| dyn" for an overload that is a candidate only when an
    // argument has the static type dyn. A signature is written as the language definition writes it: an overload
    // called on its receiver as receiver.function(arguments). The cost is the formula of the cost page.
    private const string Table = """
        _?_:_ | conditional | (bool, A, A) -> A | 0
        _&&_ | logical_and | (bool, bool) -> bool | 0
        _||_ | logical_or | (bool, bool) -> bool | 0
        !_ | logical_not | (bool) -> bool | 1
        @not_strictly_false | not_strictly_false | (bool) -> bool | 0
        _==_ | equals | (A, A) -> bool | eq(a, b)
        _!=_ | not_equals | (A, A) -> bool | eq(a, b)
        _+_ | add_bytes | (bytes, bytes) -> bytes | 1 + ⌈Lₐ/10⌉ + ⌈Lᵣ/10⌉, Lᵣ = Lₐ
        _+_ | add_double | (double, double) -> double | 1
        _+_ | add_duration_duration | (duration, duration) -> duration | 1
        _+_ | add_duration_timestamp | (duration, timestamp) -> timestamp | 1
        _+_ | add_timestamp_duration | (timestamp, duration) -> timestamp | 1
        _+_ | add_int64 | (int, int) -> int | 1
        _+_ | add_list | (list(A), list(A)) -> list(A) | 1 + n
        _+_ | add_string | (string, string) -> string | 1 + ⌈Lₐ/10⌉ + ⌈Lᵣ/10⌉, Lᵣ = Lₐ
        _+_ | add_uint64 | (uint, uint) -> uint | 1
        _/_ | divide_double | (double, double) -> double | 1
        _/_ | divide_int64 | (int, int) -> int | 1
        _/_ | divide_uint64 | (uint, uint) -> uint | 1
        _%_ | modulo_int64 | (int, int) -> int | 1
        _%_ | modulo_uint64 | (uint, uint) -> uint | 1
        _*_ | multiply_double | (double, double) -> double | 1
        _*_ | multiply_int64 | (int, int) -> int | 1
        _*_ | multiply_uint64 | (uint, uint) -> uint | 1
        -_ | negate_double | (double) -> double | 1
        -_ | negate_int64 | (int) -> int | 1
        _-_ | subtract_double | (double, double) -> double | 1
        _-_ | subtract_duration_duration | (duration, duration) -> duration | 1
        _-_ | subtract_int64 | (int, int) -> int | 1
        _-_ | subtract_timestamp_duration | (timestamp, duration) -> timestamp | 1
        _-_ | subtract_timestamp_timestamp | (timestamp, timestamp) -> duration | 1
        _-_ | subtract_uint64 | (uint, uint) -> uint | 1
        _<_ | less_bool | (bool, bool) -> bool | 1
        _<_ | less_int64 | (int, int) -> bool | 1
        _<_ | less_int64_double | (int, double) -> bool | 1 | dyn
        _<_ | less_int64_uint64 | (int, uint) -> bool | 1 | dyn
        _<_ | less_uint64 | (uint, uint) -> bool | 1
        _<_ | less_uint64_double | (uint, double) -> bool | 1 | dyn
        _<_ | less_uint64_int64 | (uint, int) -> bool | 1 | dyn
        _<_ | less_double | (double, double) -> bool | 1
        _<_ | less_double_int64 | (double, int) -> bool | 1 | dyn
        _<_ | less_double_uint64 | (double, uint) -> bool | 1 | dyn
        _<_ | less_string | (string, string) -> bool | 1 + ⌈Lₐ/10⌉
        _<_ | less_bytes | (bytes, bytes) -> bool | 1 + ⌈Lₐ/10⌉
        _<_ | less_timestamp | (timestamp, timestamp) -> bool | 1
        _<_ | less_duration | (duration, duration) -> bool | 1
        _<=_ | less_equals_bool | (bool, bool) -> bool | 1
        _<=_ | less_equals_int64 | (int, int) -> bool | 1
        _<=_ | less_equals_int64_double | (int, double) -> bool | 1 | dyn
        _<=_ | less_equals_int64_uint64 | (int, uint) -> bool | 1 | dyn
        _<=_ | less_equals_uint64 | (uint, uint) -> bool | 1
        _<=_ | less_equals_uint64_double | (uint, double) -> bool | 1 | dyn
        _<=_ | less_equals_uint64_int64 | (uint, int) -> bool | 1 | dyn
        _<=_ | less_equals_double | (double, double) -> bool | 1
        _<=_ | less_equals_double_int64 | (double, int) -> bool | 1 | dyn
        _<=_ | less_equals_double_uint64 | (double, uint) -> bool | 1 | dyn
        _<=_ | less_equals_string | (string, string) -> bool | 1 + ⌈Lₐ/10⌉
        _<=_ | less_equals_bytes | (bytes, bytes) -> bool | 1 + ⌈Lₐ/10⌉
        _<=_ | less_equals_timestamp | (timestamp, timestamp) -> bool | 1
        _<=_ | less_equals_duration | (duration, duration) -> bool | 1
        _>_ | greater_bool | (bool, bool) -> bool | 1
        _>_ | greater_int64 | (int, int) -> bool | 1
        _>_ | greater_int64_double | (int, double) -> bool | 1 | dyn
        _>_ | greater_int64_uint64 | (int, uint) -> bool | 1 | dyn
        _>_ | greater_uint64 | (uint, uint) -> bool | 1
        _>_ | greater_uint64_double | (uint, double) -> bool | 1 | dyn
        _>_ | greater_uint64_int64 | (uint, int) -> bool | 1 | dyn
        _>_ | greater_double | (double, double) -> bool | 1
        _>_ | greater_double_int64 | (double, int) -> bool | 1 | dyn
        _>_ | greater_double_uint64 | (double, uint) -> bool | 1 | dyn
        _>_ | greater_string | (string, string) -> bool | 1 + ⌈Lₐ/10⌉
        _>_ | greater_bytes | (bytes, bytes) -> bool | 1 + ⌈Lₐ/10⌉
        _>_ | greater_timestamp | (timestamp, timestamp) -> bool | 1
        _>_ | greater_duration | (duration, duration) -> bool | 1
        _>=_ | greater_equals_bool | (bool, bool) -> bool | 1
        _>=_ | greater_equals_int64 | (int, int) -> bool | 1
        _>=_ | greater_equals_int64_double | (int, double) -> bool | 1 | dyn
        _>=_ | greater_equals_int64_uint64 | (int, uint) -> bool | 1 | dyn
        _>=_ | greater_equals_uint64 | (uint, uint) -> bool | 1
        _>=_ | greater_equals_uint64_double | (uint, double) -> bool | 1 | dyn
        _>=_ | greater_equals_uint64_int64 | (uint, int) -> bool | 1 | dyn
        _>=_ | greater_equals_double | (double, double) -> bool | 1
        _>=_ | greater_equals_double_int64 | (double, int) -> bool | 1 | dyn
        _>=_ | greater_equals_double_uint64 | (double, uint) -> bool | 1 | dyn
        _>=_ | greater_equals_string | (string, string) -> bool | 1 + ⌈Lₐ/10⌉
        _>=_ | greater_equals_bytes | (bytes, bytes) -> bool | 1 + ⌈Lₐ/10⌉
        _>=_ | greater_equals_timestamp | (timestamp, timestamp) -> bool | 1
        _>=_ | greater_equals_duration | (duration, duration) -> bool | 1
        _[_] | index_list | (list(A), int) -> A | 1
        _[_] | index_map | (map(A, B), A) -> B | 1 + ⌈L/10⌉ of a string key
        @in | in_list | (A, list(A)) -> bool | 1 + Σ eq(x, eᵢ)
        @in | in_map | (A, map(A, B)) -> bool | 1 + ⌈L/10⌉ of a string key
        size | size_bytes | (bytes) -> int | 1
        size | bytes_size | bytes.size() -> int | 1
        size | size_list | (list(A)) -> int | 1
        size | list_size | list(A).size() -> int | 1
        size | size_map | (map(A, B)) -> int | 1
        size | map_size | map(A, B).size() -> int | 1
        size | size_string | (string) -> int | 1 + ⌈Lₐ/10⌉
        size | string_size | string.size() -> int | 1 + ⌈Lₐ/10⌉
        type | type | (A) -> type(A) | 1
        bool | bool_to_bool | (bool) -> bool | 1
        bool | string_to_bool | (string) -> bool | 1 + ⌈Lₐ/10⌉
        bytes | bytes_to_bytes | (bytes) -> bytes | 1
        bytes | string_to_bytes | (string) -> bytes | 1 + ⌈Lₐ/10⌉ + ⌈Lᵣ/10⌉
        double | double_to_double | (double) -> double | 1
        double | int64_to_double | (int) -> double | 1
        double | string_to_double | (string) -> double | 1 + ⌈Lₐ/10⌉
        double | uint64_to_double | (uint) -> double | 1
        duration | duration_to_duration | (duration) -> duration | 1
        duration | string_to_duration | (string) -> duration | 1 + ⌈Lₐ/10⌉
        dyn | to_dyn | (A) -> dyn | 1
        int | int64_to_int64 | (int) -> int | 1
        int | double_to_int64 | (double) -> int | 1
        int | duration_to_int64 | (duration) -> int | 1
        int | string_to_int64 | (string) -> int | 1 + ⌈Lₐ/10⌉
        int | timestamp_to_int64 | (timestamp) -> int | 1
        int | uint64_to_int64 | (uint) -> int | 1
        string | string_to_string | (string) -> string | 1
        string | bool_to_string | (bool) -> string | 1 + ⌈Lᵣ/10⌉
        string | bytes_to_string | (bytes) -> string | 1 + ⌈Lₐ/10⌉ + ⌈Lᵣ/10⌉
        string | double_to_string | (double) -> string | 1 + ⌈Lᵣ/10⌉
        string | duration_to_string | (duration) -> string | 1 + ⌈Lᵣ/10⌉
        string | int64_to_string | (int) -> string | 1 + ⌈Lᵣ/10⌉
        string | timestamp_to_string | (timestamp) -> string | 1 + ⌈Lᵣ/10⌉
        string | uint64_to_string | (uint) -> string | 1 + ⌈Lᵣ/10⌉
        timestamp | timestamp_to_timestamp | (timestamp) -> timestamp | 1
        timestamp | int64_to_timestamp | (int) -> timestamp | 1
        timestamp | string_to_timestamp | (string) -> timestamp | 1 + ⌈Lₐ/10⌉
        uint | uint64_to_uint64 | (uint) -> uint | 1
        uint | double_to_uint64 | (double) -> uint | 1
        uint | int64_to_uint64 | (int) -> uint | 1
        uint | string_to_uint64 | (string) -> uint | 1 + ⌈Lₐ/10⌉
        contains | contains_string | string.contains(string) -> bool | 1 + ⌈Lₐ/10⌉
        endsWith | ends_with_string | string.endsWith(string) -> bool | 1 + ⌈Lₐ/10⌉
        startsWith | starts_with_string | string.startsWith(string) -> bool | 1 + ⌈Lₐ/10⌉
        matches | matches | (string, string) -> bool | parse + build + match
        matches | matches_string | string.matches(string) -> bool | parse + build + match
        getFullYear | timestamp_to_year | timestamp.getFullYear() -> int | 1
        getFullYear | timestamp_to_year_with_tz | timestamp.getFullYear(string) -> int | 1 + ⌈Lₐ/10⌉
        getMonth | timestamp_to_month | timestamp.getMonth() -> int | 1
        getMonth | timestamp_to_month_with_tz | timestamp.getMonth(string) -> int | 1 + ⌈Lₐ/10⌉
        getDayOfYear | timestamp_to_day_of_year | timestamp.getDayOfYear() -> int | 1
        getDayOfYear | timestamp_to_day_of_year_with_tz | timestamp.getDayOfYear(string) -> int | 1 + ⌈Lₐ/10⌉
        getDayOfMonth | timestamp_to_day_of_month | timestamp.getDayOfMonth() -> int | 1
        getDayOfMonth | timestamp_to_day_of_month_with_tz | timestamp.getDayOfMonth(string) -> int | 1 + ⌈Lₐ/10⌉
        getDate | timestamp_to_day_of_month_1_based | timestamp.getDate() -> int | 1
        getDate | timestamp_to_day_of_month_1_based_with_tz | timestamp.getDate(string) -> int | 1 + ⌈Lₐ/10⌉
        getDayOfWeek | timestamp_to_day_of_week | timestamp.getDayOfWeek() -> int | 1
        getDayOfWeek | timestamp_to_day_of_week_with_tz | timestamp.getDayOfWeek(string) -> int | 1 + ⌈Lₐ/10⌉
        getHours | timestamp_to_hours | timestamp.getHours() -> int | 1
        getHours | timestamp_to_hours_with_tz | timestamp.getHours(string) -> int | 1 + ⌈Lₐ/10⌉
        getHours | duration_to_hours | duration.getHours() -> int | 1
        getMinutes | timestamp_to_minutes | timestamp.getMinutes() -> int | 1
        getMinutes | timestamp_to_minutes_with_tz | timestamp.getMinutes(string) -> int | 1 + ⌈Lₐ/10⌉
        getMinutes | duration_to_minutes | duration.getMinutes() -> int | 1
        getSeconds | timestamp_to_seconds | timestamp.getSeconds() -> int | 1
        getSeconds | timestamp_to_seconds_tz | timestamp.getSeconds(string) -> int | 1 + ⌈Lₐ/10⌉
        getSeconds | duration_to_seconds | duration.getSeconds() -> int | 1
        getMilliseconds | timestamp_to_milliseconds | timestamp.getMilliseconds() -> int | 1
        getMilliseconds | timestamp_to_milliseconds_with_tz | timestamp.getMilliseconds(string) -> int | 1 + ⌈Lₐ/10⌉
        getMilliseconds | duration_to_milliseconds | duration.getMilliseconds() -> int | 1
        """;

    // The argument of each parameter type for the cost and kind checks: a string of 21 code units, bytes of 15
    // bytes, a list of 3 elements, a map of one entry with the key 7; a type parameter takes an int. The two strings of
    // matches are a pattern of 21 literal characters, whose program has 22 instructions with the match, and a subject.
    private const string Text = "abcdefghijklmnopqrstu";

    private static readonly byte[] Bytes = Encoding.ASCII.GetBytes("abcdefghijklmno");

    private static readonly LineTable Lines = new("x");

    public static TheoryData<string> Identifiers => new(Rows().Select(row => row.Id));

    [Fact]
    public static void Table_HasTheOverloadsOfThisPartInTheOrderOfCelGo()
    {
        Assert.Equal(
            Rows().Select(row => (row.Function, row.Id, row.Signature, row.DynOnly)),
            StandardLibraryV1.Overloads.Select(overload => (overload.Function, overload.Declaration.OverloadId, Signature(overload), overload.DynOnly)));
        Assert.All(StandardLibraryV1.Overloads, overload => Assert.False(overload.IsApplication));
        Assert.All(StandardLibraryV1.Overloads, overload => Assert.Equal("standard library v1", overload.Source));
    }

    // Only the special forms, which the planner evaluates as nodes of their own, have no implementation.
    [Fact]
    public static void SpecialForms_HaveNoImplementation()
    {
        Assert.Equal(
            ["conditional", "logical_and", "logical_or", "not_strictly_false"],
            StandardLibraryV1.Overloads.Where(overload => overload.Implementation is null).Select(overload => overload.Declaration.OverloadId));
    }

    // The units of a call: the fixed units and the cost function of the arguments, which the call node charges,
    // and what the implementation charges itself — equality pair by pair, a result whose length depends on its
    // content before it is created. Their sum is the formula of the cost page on the arguments of the call.
    [Theory]
    [MemberData(nameof(Identifiers))]
    public static void Cost_IsTheFormulaOfTheCostPage(string id)
    {
        string formula = Rows().Single(row => row.Id == id).Formula;
        OverloadBinding overload = Assert.Single(StandardLibraryV1.Overloads, overload => overload.Declaration.OverloadId == id);
        CelValue[] arguments = Arguments(overload);
        var meter = new CountingMeter();
        ulong units = overload.FixedCost + (overload.ArgumentCost?.Invoke(arguments) ?? 0);
        CelValue result = overload.Implementation is { } implementation
            ? implementation(arguments, new CallSite(overload.Function, 0, Lines, meter))
            : default;

        Assert.Equal(Expected(formula, arguments, result), units + meter.Units);
    }

    // Every implementation rejects a value of another kind for each parameter that is not a type parameter, at
    // whatever depth it reads: a static type is no guarantee of the value.
    [Theory]
    [MemberData(nameof(Identifiers))]
    public static void Implementation_OfAValueOfAnotherKind_IsEvalNoMatchingOverload(string id)
    {
        OverloadBinding overload = Assert.Single(StandardLibraryV1.Overloads, overload => overload.Declaration.OverloadId == id);
        ImmutableArray<CelType> parameters = overload.Declaration.ParameterTypes;
        for (int i = 0; i < parameters.Length; i++)
        {
            if (overload.Implementation is not { } implementation || parameters[i].Kind == CelTypeKind.TypeParameter)
            {
                continue;
            }

            CelValue[] arguments = Arguments(overload);
            arguments[i] = parameters[i].Kind == CelTypeKind.String ? CelValue.FromInt64(7) : CelValue.FromString("x");
            CelValue result = implementation(arguments, new CallSite(overload.Function, 0, Lines));

            Assert.True(result.TryGetError(out CelError? error), id);
            Assert.Equal(CelErrorCode.EvalNoMatchingOverload, error.Code);
        }
    }

    [Fact]
    public static void TypeIdentifiers_AreTheTwelveOfVersionOne()
    {
        Assert.Equal(
            [
                ("bool", "bool"), ("bytes", "bytes"), ("double", "double"), ("google.protobuf.Duration", "duration"),
                ("google.protobuf.Timestamp", "timestamp"), ("int", "int"), ("list", "list(dyn)"), ("map", "map(dyn, dyn)"),
                ("null_type", "null_type"), ("string", "string"), ("type", "type(dyn)"), ("uint", "uint"),
            ],
            StandardLibraryV1.TypeIdentifiers.OrderBy(entry => entry.Key, StringComparer.Ordinal).Select(entry => (entry.Key, entry.Value.ToString())));
        var reserved = (ISet<string>)CelStandardLibrary.V1.Declarations.ReservedNames;
        Assert.Contains("google.protobuf.Timestamp", reserved);
        Assert.Contains("google.protobuf.Duration", reserved);
    }

    [Fact]
    public static void VersionOne_IsAHandleToTheTable()
    {
        Assert.Equal(StandardLibraryV1.Overloads, CelStandardLibrary.V1.Overloads);
        Assert.Same(StandardLibraryV1.TypeIdentifiers, CelStandardLibrary.V1.Declarations.TypeIdentifiers);
    }

    private static ulong Expected(string formula, CelValue[] arguments, CelValue result)
    {
        ulong argumentLength = 0;
        foreach (CelValue argument in arguments)
        {
            argumentLength += Length(argument);
        }

        return formula switch
        {
            "0" or "1" => ulong.Parse(formula, CultureInfo.InvariantCulture),

            // The arguments of equality are two ints, whose eq is 1; in_list compares the int with each element of
            // the list, which it does not find.
            "eq(a, b)" => 1,
            "1 + Σ eq(x, eᵢ)" when arguments[1].TryGetList(out CelList? elements) => 1 + (ulong)elements.Count,
            "1 + ⌈L/10⌉ of a string key" => 1 + Tenths(StringLength(arguments)),
            "1 + ⌈Lₐ/10⌉" => 1 + Tenths(argumentLength),
            "1 + ⌈Lₐ/10⌉ + ⌈Lᵣ/10⌉, Lᵣ = Lₐ" => 1 + (2 * Tenths(argumentLength)),
            "1 + ⌈Lₐ/10⌉ + ⌈Lᵣ/10⌉" => 1 + Tenths(argumentLength) + Tenths(Length(result)),
            "1 + ⌈Lᵣ/10⌉" => 1 + Tenths(Length(result)),
            "1 + n" when result.TryGetList(out CelList? list) => 1 + (ulong)list.Count,

            // The pattern is not a literal: the parse 1 + ⌈L/10⌉, the build ⌈m/4⌉ and the match 1 + ⌈(n + 1) · m/10⌉, with m the
            // 21 instructions of the literal characters and the one of the match.
            "parse + build + match" => (1 + Tenths(Length(arguments[1]))) + ((Length(arguments[1]) + 1 + 3) / 4)
                + 1 + Tenths((Length(arguments[0]) + 1) * (Length(arguments[1]) + 1)),
            _ => throw new InvalidOperationException(formula),
        };
    }

    // The length of a string in UTF-16 code units, of bytes in bytes; 0 for other kinds.
    private static ulong Length(CelValue value)
    {
        if (value.TryGetString(out string? text))
        {
            return (ulong)text.Length;
        }

        return value.TryGetBytes(out ReadOnlyMemory<byte> bytes) ? (ulong)bytes.Length : 0;
    }

    private static ulong Tenths(ulong length) => (length + 9) / 10;

    private static ulong StringLength(CelValue[] arguments) =>
        (ulong)arguments.Sum(argument => argument.TryGetString(out string? text) ? text.Length : 0);

    private static CelValue[] Arguments(OverloadBinding overload) => [.. overload.Declaration.ParameterTypes.Select(Argument)];

    private static CelValue Argument(CelType type) => type.Kind switch
    {
        CelTypeKind.Bool => CelValue.FromBoolean(true),
        CelTypeKind.UInt => CelValue.FromUInt64(7),
        CelTypeKind.Double => CelValue.FromDouble(2.5),
        CelTypeKind.String => CelValue.FromString(Text),
        CelTypeKind.Bytes => CelValue.FromBytes(Bytes),
        CelTypeKind.Timestamp => CelValue.FromTimestamp(new CelTimestamp(1_234_567_890, 123_456_789)),
        CelTypeKind.Duration => CelValue.FromDuration(new CelDuration(1_500_000_000)),
        CelTypeKind.List => CelValue.FromList(CelList.Create(CelValue.FromInt64(1), CelValue.FromInt64(2), CelValue.FromInt64(3))),
        CelTypeKind.Map => CelValue.FromMap(CelMap.Create([new(CelValue.FromInt64(7), CelValue.FromInt64(7))])),
        _ => CelValue.FromInt64(7),
    };

    private static IEnumerable<(string Function, string Id, string Signature, string Formula, bool DynOnly)> Rows()
    {
        foreach (string line in Table.ReplaceLineEndings("\n").Split('\n'))
        {
            string[] cells = line.Split(" | ");
            yield return (cells[0], cells[1], cells[2], cells[3], cells.Length > 4);
        }
    }

    private static string Signature(OverloadBinding overload)
    {
        ImmutableArray<CelType> parameters = overload.Declaration.ParameterTypes;
        string call = overload.Declaration.IsReceiverStyle
            ? parameters[0] + "." + overload.Function + "(" + string.Join(", ", parameters.Skip(1)) + ")"
            : "(" + string.Join(", ", parameters) + ")";
        return call + " -> " + overload.Declaration.ResultType;
    }

    // A meter that sums the units charged through the call site.
    private sealed class CountingMeter : IEvaluationMeter
    {
        internal ulong Units { get; private set; }

        public void Charge(ulong units) => Units += units;

        public void Event()
        {
        }
    }
}
