using System.Collections.Generic;

namespace Great.Parts.Cel.Eng.Difftest.Profile;

/// <summary>A variable of the run manifest.</summary>
/// <param name="Name">The name as the manifest declares it; it is also the key of a binding.</param>
/// <param name="Type">
/// The kind of the type the generator treats the variable as: <c>int</c> for an <c>int_wrapper</c>, <c>list</c> for every
/// <c>list&lt;E&gt;</c>, <c>map</c> for every <c>map&lt;K, V&gt;</c>.
/// </param>
internal sealed record ProfileVariable(string Name, ProfileType Type)
{
    /// <summary>The type record of the manifest: <c>int</c>, <c>list&lt;int&gt;</c>, <c>map&lt;string, int&gt;</c>, <c>int_wrapper</c>.</summary>
    internal string TypeRecord { get; init; } = Type.Name();

    /// <summary>The static type the generator knows: the parameters of a collection, <c>int</c> for <c>int_wrapper</c>.</summary>
    internal StaticType Declared { get; init; } = StaticType.Of(Type);

    /// <summary>Whether the variable is a wrapper: its binding is a value or <c>null</c>.</summary>
    internal bool Wrapper { get; init; }

    /// <summary>The ways to write the variable in an expression: the declared name, or a name the container resolves to it.</summary>
    internal IReadOnlyList<string> Spellings { get; init; } = [Name];
}

/// <summary>An overload of the profile.</summary>
/// <param name="Id">The overload identifier of cel-go, for example <c>add_int64</c>.</param>
/// <param name="Function">The function, for example <c>_+_</c>.</param>
/// <param name="Parameters">The parameter types, the receiver first; <c>list</c> stands for <c>list(A)</c>, <c>map</c> for <c>map(A, B)</c>.</param>
/// <param name="Result">The result type.</param>
/// <param name="Receiver">Whether the overload is called on its receiver, the first parameter: <c>s.contains(t)</c>.</param>
/// <param name="DynOnly">Whether the overload is a candidate only when an argument has the static type <c>dyn</c>: ordering across numeric kinds.</param>
internal sealed record ProfileOverload(
    string Id,
    string Function,
    IReadOnlyList<ProfileType> Parameters,
    ProfileType Result,
    bool Receiver = false,
    bool DynOnly = false);

/// <summary>
/// The tables of the profile (docs/design/eng-difftest.md, "Profile"): the variables of the manifest, the functions of the
/// standard library version 1 without <c>matches</c> and their overloads, the macros, the type identifiers, and
/// the limits of a generated case.
/// </summary>
internal static class ProfileTables
{
    /// <summary>The path of the run manifest from the repository root.</summary>
    internal const string ManifestPath = "eng/difftest/Profile/environment.json";

    /// <summary>
    /// The path of the time zone data file from the repository root, with <c>/</c>: the archive <c>zoneinfo.zip</c> of the tests,
    /// byte for byte the one of the pinned Go. The library side of a run takes its database from it, the oracle names it with
    /// <c>ZONEINFO</c>, the generator takes zone names from it, and <c>ENG822</c> compares the offsets of its zones.
    /// </summary>
    internal const string ZoneInfoPath = "tests/Great.Parts.Cel.Conformance/ZoneInfo/zoneinfo.zip";

    /// <summary>The version of the release of the time zone database in <see cref="ZoneInfoPath"/>.</summary>
    internal const string ZoneInfoVersion = "2026c";

    /// <summary>The string variable that is, half the time, bound to a zone argument: the zone of a receiver may be this variable.</summary>
    internal const string ZoneVariable = "s1";

    /// <summary>The function <c>timestamp</c>.</summary>
    internal const string TimestampConversion = "timestamp";

    /// <summary>The function <c>duration</c>.</summary>
    internal const string DurationConversion = "duration";

    /// <summary>The function <c>_?_:_</c>.</summary>
    internal const string Conditional = "_?_:_";

    /// <summary>The function <c>_&amp;&amp;_</c>.</summary>
    internal const string LogicalAnd = "_&&_";

    /// <summary>The function <c>_||_</c>.</summary>
    internal const string LogicalOr = "_||_";

    /// <summary>The function <c>!_</c>.</summary>
    internal const string LogicalNot = "!_";

    /// <summary>The function <c>_==_</c>.</summary>
    internal const string Equal = "_==_";

    /// <summary>The function <c>_!=_</c>.</summary>
    internal const string NotEqual = "_!=_";

    /// <summary>The function <c>_+_</c>.</summary>
    internal const string Add = "_+_";

    /// <summary>The function <c>_-_</c>.</summary>
    internal const string Subtract = "_-_";

    /// <summary>The function <c>_*_</c>.</summary>
    internal const string Multiply = "_*_";

    /// <summary>The function <c>_/_</c>.</summary>
    internal const string Divide = "_/_";

    /// <summary>The function <c>_%_</c>.</summary>
    internal const string Modulo = "_%_";

    /// <summary>The function <c>-_</c>.</summary>
    internal const string Negate = "-_";

    /// <summary>The function <c>_&lt;_</c>.</summary>
    internal const string Less = "_<_";

    /// <summary>The function <c>_&lt;=_</c>.</summary>
    internal const string LessEqual = "_<=_";

    /// <summary>The function <c>_&gt;_</c>.</summary>
    internal const string Greater = "_>_";

    /// <summary>The function <c>_&gt;=_</c>.</summary>
    internal const string GreaterEqual = "_>=_";

    /// <summary>The function <c>_[_]</c>.</summary>
    internal const string Index = "_[_]";

    /// <summary>The function <c>@in</c>, the operator <c>in</c>.</summary>
    internal const string In = "@in";

    /// <summary>The function <c>size</c>, global and on a receiver.</summary>
    internal const string Size = "size";

    /// <summary>The function <c>contains</c>, on a receiver.</summary>
    internal const string Contains = "contains";

    /// <summary>The function <c>startsWith</c>, on a receiver.</summary>
    internal const string StartsWith = "startsWith";

    /// <summary>The function <c>endsWith</c>, on a receiver.</summary>
    internal const string EndsWith = "endsWith";

    /// <summary>The function <c>type</c>.</summary>
    internal const string TypeOf = "type";

    /// <summary>The function <c>dyn</c>.</summary>
    internal const string Dyn = "dyn";

    /// <summary>The conversion <c>int</c>.</summary>
    internal const string IntConversion = "int";

    /// <summary>The conversion <c>uint</c>.</summary>
    internal const string UintConversion = "uint";

    /// <summary>The conversion <c>double</c>.</summary>
    internal const string DoubleConversion = "double";

    /// <summary>The conversion <c>string</c>.</summary>
    internal const string StringConversion = "string";

    /// <summary>The conversion <c>bytes</c>.</summary>
    internal const string BytesConversion = "bytes";

    /// <summary>The conversion <c>bool</c>.</summary>
    internal const string BoolConversion = "bool";

    /// <summary>The macro <c>all</c>.</summary>
    internal const string All = "all";

    /// <summary>The macro <c>exists</c>.</summary>
    internal const string Exists = "exists";

    /// <summary>The macro <c>exists_one</c>.</summary>
    internal const string ExistsOne = "exists_one";

    /// <summary>The macro <c>map</c>, with 2 or 3 arguments.</summary>
    internal const string MapMacro = "map";

    /// <summary>The macro <c>filter</c>.</summary>
    internal const string Filter = "filter";

    /// <summary>The largest depth of an expression; a literal or an identifier alone has depth 1.</summary>
    internal const int MaxDepth = 6;

    /// <summary>
    /// The largest number of nodes of an expression: literals, identifiers, calls, lists, maps, keys and values of entries,
    /// selections, presence tests and macros.
    /// </summary>
    internal const int MaxNodes = 40;

    /// <summary>The largest number of Unicode scalar values of a string, in a literal or a binding.</summary>
    internal const int MaxCodePoints = 8;

    /// <summary>The largest number of bytes of a <c>bytes</c> value, in a literal or a binding.</summary>
    internal const int MaxBytes = 8;

    /// <summary>The largest number of elements of a list literal and of entries of a map literal, also of a bound list or map.</summary>
    internal const int MaxElements = 3;

    /// <summary>The largest number of macros nested in each other.</summary>
    internal const int MaxMacroNesting = 2;

    /// <summary>The largest number of operands of a chain of <c>&amp;&amp;</c> or <c>||</c>.</summary>
    internal const int MaxChain = 4;

    /// <summary>The container of the manifest: an identifier <c>q</c> is tried as <c>gp.test.q</c>, <c>gp.q</c> and <c>q</c>.</summary>
    internal const string Container = "gp.test";

    /// <summary>The variables of the manifest, in its order.</summary>
    internal static IReadOnlyList<ProfileVariable> Variables { get; } =
    [
        new("i1", ProfileType.IntType),
        new("i2", ProfileType.IntType),
        new("u1", ProfileType.UintType),
        new("d1", ProfileType.DoubleType),
        new("b1", ProfileType.BoolType),
        new("b2", ProfileType.BoolType),
        new("s1", ProfileType.StringType),
        new("y1", ProfileType.BytesType),
        new("x1", ProfileType.DynType),
        new("x2", ProfileType.DynType),
        new("li", ProfileType.ListType) { TypeRecord = "list<int>", Declared = StaticType.ListOf(StaticType.Of(ProfileType.IntType)) },
        new("ls", ProfileType.ListType) { TypeRecord = "list<string>", Declared = StaticType.ListOf(StaticType.Of(ProfileType.StringType)) },
        new("lx", ProfileType.ListType) { TypeRecord = "list<dyn>", Declared = StaticType.ListOf(StaticType.Dyn) },
        new("ms", ProfileType.MapType) { TypeRecord = "map<string, int>", Declared = StaticType.MapOf(StaticType.Of(ProfileType.StringType), StaticType.Of(ProfileType.IntType)) },
        new("mx", ProfileType.MapType) { TypeRecord = "map<int, dyn>", Declared = StaticType.MapOf(StaticType.Of(ProfileType.IntType), StaticType.Dyn) },
        new("wi", ProfileType.IntType) { TypeRecord = "int_wrapper", Wrapper = true },
        new("gp.q", ProfileType.IntType) { Spellings = ["q", "gp.q", ".gp.q"] },
        new("gp.test.r", ProfileType.StringType) { Spellings = ["r", "test.r", "gp.test.r", ".gp.test.r"] },
    ];

    /// <summary>The type identifiers of the standard library version 1; each is a value of the type <c>type</c>.</summary>
    internal static IReadOnlyList<string> TypeIdentifiers { get; } =
        ["bool", "bytes", "double", "int", "uint", "string", "null_type", "type", "list", "map", "google.protobuf.Timestamp", "google.protobuf.Duration"];

    /// <summary>The iteration variables: the first for a macro outside any other, the second for a macro inside it.</summary>
    internal static IReadOnlyList<string> IterationVariables { get; } = ["v", "w"];

    /// <summary>The fields of selections and presence tests; map literals also take them as string keys.</summary>
    internal static IReadOnlyList<string> FieldNames { get; } = ["a", "b", "f"];

    /// <summary>The macros of the profile.</summary>
    internal static IReadOnlyList<string> Macros { get; } = [All, Exists, ExistsOne, MapMacro, Filter];

    /// <summary>The overloads of the profile, each function in the order of cel-go.</summary>
    internal static IReadOnlyList<ProfileOverload> Overloads { get; } = BuildOverloads();

    /// <summary>The types for which the generator builds an expression: the root, list elements and map values take any of them.</summary>
    internal static IReadOnlyList<ProfileType> TargetTypes { get; } =
    [
        ProfileType.IntType,
        ProfileType.UintType,
        ProfileType.DoubleType,
        ProfileType.BoolType,
        ProfileType.StringType,
        ProfileType.BytesType,
        ProfileType.NullType,
        ProfileType.ListType,
        ProfileType.MapType,
        ProfileType.TypeType,
        ProfileType.TimestampType,
        ProfileType.DurationType,
        ProfileType.DynType,
    ];

    /// <summary>The types of map keys.</summary>
    internal static IReadOnlyList<ProfileType> KeyTypes { get; } =
        [ProfileType.IntType, ProfileType.UintType, ProfileType.BoolType, ProfileType.StringType];

    /// <summary>The scalar kinds and <c>null</c>: the elements and values of bound lists and maps, and most values of <c>dyn</c> variables.</summary>
    internal static IReadOnlyList<ProfileType> ScalarKinds { get; } =
    [
        ProfileType.IntType,
        ProfileType.UintType,
        ProfileType.DoubleType,
        ProfileType.BoolType,
        ProfileType.StringType,
        ProfileType.BytesType,
        ProfileType.NullType,
    ];

    /// <summary>The kinds of the value of a <c>dyn</c> variable: the scalar kinds, <c>null</c>, a list and a map.</summary>
    internal static IReadOnlyList<ProfileType> DynKinds { get; } = [.. ScalarKinds, ProfileType.ListType, ProfileType.MapType];

    // The table of the standard library version 1 without matches, as the library and cel-go v0.32.0 have it.
    private static List<ProfileOverload> BuildOverloads()
    {
        const ProfileType A = ProfileType.ParameterAType;
        const ProfileType B = ProfileType.ParameterBType;
        const ProfileType Int = ProfileType.IntType;
        const ProfileType Uint = ProfileType.UintType;
        const ProfileType Dbl = ProfileType.DoubleType;
        const ProfileType Bool = ProfileType.BoolType;
        const ProfileType Str = ProfileType.StringType;
        const ProfileType Byt = ProfileType.BytesType;
        const ProfileType Ts = ProfileType.TimestampType;
        const ProfileType Dur = ProfileType.DurationType;
        const ProfileType ListA = ProfileType.ListType;
        const ProfileType MapAB = ProfileType.MapType;

        List<ProfileOverload> table =
        [
            new("conditional", Conditional, [Bool, A, A], A),
            new("logical_and", LogicalAnd, [Bool, Bool], Bool),
            new("logical_or", LogicalOr, [Bool, Bool], Bool),
            new("logical_not", LogicalNot, [Bool], Bool),
            new("equals", Equal, [A, A], Bool),
            new("not_equals", NotEqual, [A, A], Bool),
            new("add_bytes", Add, [Byt, Byt], Byt),
            new("add_double", Add, [Dbl, Dbl], Dbl),
            new("add_int64", Add, [Int, Int], Int),
            new("add_list", Add, [ListA, ListA], ListA),
            new("add_string", Add, [Str, Str], Str),
            new("add_uint64", Add, [Uint, Uint], Uint),
            new("add_timestamp_duration", Add, [Ts, Dur], Ts),
            new("add_duration_timestamp", Add, [Dur, Ts], Ts),
            new("add_duration_duration", Add, [Dur, Dur], Dur),
            new("divide_double", Divide, [Dbl, Dbl], Dbl),
            new("divide_int64", Divide, [Int, Int], Int),
            new("divide_uint64", Divide, [Uint, Uint], Uint),
            new("modulo_int64", Modulo, [Int, Int], Int),
            new("modulo_uint64", Modulo, [Uint, Uint], Uint),
            new("multiply_double", Multiply, [Dbl, Dbl], Dbl),
            new("multiply_int64", Multiply, [Int, Int], Int),
            new("multiply_uint64", Multiply, [Uint, Uint], Uint),
            new("negate_double", Negate, [Dbl], Dbl),
            new("negate_int64", Negate, [Int], Int),
            new("subtract_double", Subtract, [Dbl, Dbl], Dbl),
            new("subtract_int64", Subtract, [Int, Int], Int),
            new("subtract_uint64", Subtract, [Uint, Uint], Uint),
            new("subtract_timestamp_duration", Subtract, [Ts, Dur], Ts),
            new("subtract_timestamp_timestamp", Subtract, [Ts, Ts], Dur),
            new("subtract_duration_duration", Subtract, [Dur, Dur], Dur),
        ];

        // Ordering: for each relation bool, then each number with itself and the other two numbers, then string and bytes;
        // the pairs across numeric kinds are candidates only with a dyn argument.
        (ProfileType Left, ProfileType Right)[] pairs =
        [
            (Bool, Bool), (Int, Int), (Int, Dbl), (Int, Uint), (Uint, Uint), (Uint, Dbl), (Uint, Int),
            (Dbl, Dbl), (Dbl, Int), (Dbl, Uint), (Str, Str), (Byt, Byt), (Ts, Ts), (Dur, Dur),
        ];
        foreach ((string function, string prefix) in new[] { (Less, "less"), (LessEqual, "less_equals"), (Greater, "greater"), (GreaterEqual, "greater_equals") })
        {
            foreach ((ProfileType left, ProfileType right) in pairs)
            {
                bool across = left != right;
                string id = prefix + "_" + Suffix(left) + (across ? "_" + Suffix(right) : string.Empty);
                table.Add(new(id, function, [left, right], Bool, DynOnly: across));
            }
        }

        table.AddRange(
        [
            new("index_list", Index, [ListA, Int], A),
            new("index_map", Index, [MapAB, A], B),
            new("in_list", In, [A, ListA], Bool),
            new("in_map", In, [A, MapAB], Bool),
            new("size_bytes", Size, [Byt], Int),
            new("bytes_size", Size, [Byt], Int, Receiver: true),
            new("size_list", Size, [ListA], Int),
            new("list_size", Size, [ListA], Int, Receiver: true),
            new("size_map", Size, [MapAB], Int),
            new("map_size", Size, [MapAB], Int, Receiver: true),
            new("size_string", Size, [Str], Int),
            new("string_size", Size, [Str], Int, Receiver: true),
            new("type", TypeOf, [A], ProfileType.TypeType),
            new("bool_to_bool", BoolConversion, [Bool], Bool),
            new("string_to_bool", BoolConversion, [Str], Bool),
            new("bytes_to_bytes", BytesConversion, [Byt], Byt),
            new("string_to_bytes", BytesConversion, [Str], Byt),
            new("double_to_double", DoubleConversion, [Dbl], Dbl),
            new("int64_to_double", DoubleConversion, [Int], Dbl),
            new("string_to_double", DoubleConversion, [Str], Dbl),
            new("uint64_to_double", DoubleConversion, [Uint], Dbl),
            new("to_dyn", Dyn, [A], ProfileType.DynType),
            new("int64_to_int64", IntConversion, [Int], Int),
            new("double_to_int64", IntConversion, [Dbl], Int),
            new("string_to_int64", IntConversion, [Str], Int),
            new("uint64_to_int64", IntConversion, [Uint], Int),
            new("string_to_string", StringConversion, [Str], Str),
            new("bool_to_string", StringConversion, [Bool], Str),
            new("bytes_to_string", StringConversion, [Byt], Str),
            new("double_to_string", StringConversion, [Dbl], Str),
            new("int64_to_string", StringConversion, [Int], Str),
            new("uint64_to_string", StringConversion, [Uint], Str),
            new("uint64_to_uint64", UintConversion, [Uint], Uint),
            new("timestamp_to_timestamp", TimestampConversion, [Ts], Ts),
            new("int64_to_timestamp", TimestampConversion, [Int], Ts),
            new("string_to_timestamp", TimestampConversion, [Str], Ts),
            new("duration_to_duration", DurationConversion, [Dur], Dur),
            new("string_to_duration", DurationConversion, [Str], Dur),
            new("timestamp_to_int64", IntConversion, [Ts], Int),
            new("duration_to_int64", IntConversion, [Dur], Int),
            new("timestamp_to_string", StringConversion, [Ts], Str),
            new("duration_to_string", StringConversion, [Dur], Str),
            new("double_to_uint64", UintConversion, [Dbl], Uint),
            new("int64_to_uint64", UintConversion, [Int], Uint),
            new("string_to_uint64", UintConversion, [Str], Uint),
            new("contains_string", Contains, [Str, Str], Bool, Receiver: true),
            new("ends_with_string", EndsWith, [Str, Str], Bool, Receiver: true),
            new("starts_with_string", StartsWith, [Str, Str], Bool, Receiver: true),
        ]);

        // The receivers of a timestamp, without a zone argument and with one (the ids are those of cel-go, among them
        // timestamp_to_seconds_tz), and of a duration.
        (string Function, string Id, string ZoneId)[] timestampReceivers =
        [
            ("getFullYear", "timestamp_to_year", "timestamp_to_year_with_tz"),
            ("getMonth", "timestamp_to_month", "timestamp_to_month_with_tz"),
            ("getDayOfYear", "timestamp_to_day_of_year", "timestamp_to_day_of_year_with_tz"),
            ("getDayOfMonth", "timestamp_to_day_of_month", "timestamp_to_day_of_month_with_tz"),
            ("getDate", "timestamp_to_day_of_month_1_based", "timestamp_to_day_of_month_1_based_with_tz"),
            ("getDayOfWeek", "timestamp_to_day_of_week", "timestamp_to_day_of_week_with_tz"),
            ("getHours", "timestamp_to_hours", "timestamp_to_hours_with_tz"),
            ("getMinutes", "timestamp_to_minutes", "timestamp_to_minutes_with_tz"),
            ("getSeconds", "timestamp_to_seconds", "timestamp_to_seconds_tz"),
            ("getMilliseconds", "timestamp_to_milliseconds", "timestamp_to_milliseconds_with_tz"),
        ];
        foreach ((string function, string id, string zoneId) in timestampReceivers)
        {
            table.Add(new(id, function, [Ts], Int, Receiver: true));
            table.Add(new(zoneId, function, [Ts, Str], Int, Receiver: true));
        }

        table.AddRange(
        [
            new("duration_to_hours", "getHours", [Dur], Int, Receiver: true),
            new("duration_to_minutes", "getMinutes", [Dur], Int, Receiver: true),
            new("duration_to_seconds", "getSeconds", [Dur], Int, Receiver: true),
            new("duration_to_milliseconds", "getMilliseconds", [Dur], Int, Receiver: true),
        ]);
        return table;
    }

    private static string Suffix(ProfileType type) => type switch
    {
        ProfileType.IntType => "int64",
        ProfileType.UintType => "uint64",
        _ => type.Name(),
    };
}
