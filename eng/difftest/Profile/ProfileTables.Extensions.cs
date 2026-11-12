using System;
using System.Collections.Generic;
using System.Linq;

namespace Great.Parts.Cel.Eng.Difftest.Profile;

/// <summary>
/// The tables of the extension sets of the profile (docs/design/eng-difftest.md, "Profile of the extension sets"): the sets of the
/// manifest, their functions and overloads with the identifiers of cel-go, and the names that the macros of the sets bind.
/// </summary>
internal static partial class ProfileTables
{
    /// <summary>The set <c>strings</c>.</summary>
    internal const string StringsSet = "strings";

    /// <summary>The set <c>math</c>.</summary>
    internal const string MathSet = "math";

    /// <summary>The set <c>lists</c>.</summary>
    internal const string ListsSet = "lists";

    /// <summary>The set <c>sets</c>.</summary>
    internal const string SetsSet = "sets";

    /// <summary>The set <c>encoders</c>.</summary>
    internal const string EncodersSet = "encoders";

    /// <summary>The set <c>bindings</c>.</summary>
    internal const string BindingsSet = "bindings";

    /// <summary>The set <c>two-var-comprehensions</c>.</summary>
    internal const string TwoVariableSet = "two-var-comprehensions";

    /// <summary>The set <c>network</c>.</summary>
    internal const string NetworkSet = "network";

    /// <summary>The set <c>optional</c>.</summary>
    internal const string OptionalSet = "optional";

    /// <summary>The function <c>_[?_]</c>, the optional index <c>e[?k]</c>.</summary>
    internal const string OptionalIndex = "_[?_]";

    /// <summary>The macro <c>optMap</c>.</summary>
    internal const string OptMapMacro = "optMap";

    /// <summary>The macro <c>optFlatMap</c>.</summary>
    internal const string OptFlatMapMacro = "optFlatMap";

    /// <summary>The identifier <c>optional_type</c>: the value of the type <c>type</c> that <c>type(e)</c> gives for an optional <c>e</c>.</summary>
    internal const string OptionalTypeIdentifier = "optional_type";

    /// <summary>The overload of the optional selection <c>e.?f</c>, which the node <see cref="Select"/> with <see cref="Select.Optional"/> carries.</summary>
    internal const string OptionalSelectOverload = "select_optional_field";

    /// <summary>The macro <c>cel.bind</c>.</summary>
    internal const string BindMacro = "cel.bind";

    /// <summary>The macro <c>sortBy</c>.</summary>
    internal const string SortByMacro = "sortBy";

    /// <summary>The macros of the set <c>two-var-comprehensions</c> that give a <c>bool</c>.</summary>
    internal static readonly string[] TwoVariableQuantifiers = [All, Exists, "existsOne", ExistsOne];

    /// <summary>The macros of the set <c>two-var-comprehensions</c> that give a list or a map.</summary>
    internal static readonly string[] TwoVariableTransforms = ["transformList", "transformMap", "transformMapEntry"];

    /// <summary>
    /// The sets of the run manifest, in its order, with their versions as the manifest writes them: <c>decimal</c> and the
    /// internal set <c>block</c> are not in it.
    /// </summary>
    internal static IReadOnlyList<(string Name, string Version)> Extensions { get; } =
    [
        (StringsSet, "5"), (MathSet, "3"), (ListsSet, "4"), (SetsSet, "0"), (EncodersSet, "1"), (BindingsSet, "1"),
        (TwoVariableSet, "0"), (NetworkSet, "1"), (OptionalSet, "2"),
    ];

    /// <summary>The names of the two variables of a two-variable macro, the outer macro first, so that a macro inside another has other names.</summary>
    internal static IReadOnlyList<(string First, string Second)> TwoVariableNames { get; } = [("i", "e"), ("j", "t")];

    /// <summary>The names that <c>cel.bind</c> binds when it hides no variable of the manifest.</summary>
    internal static IReadOnlyList<string> BindingNames { get; } = ["bn1", "bn2"];

    /// <summary>The variables of the manifest that <c>cel.bind</c> may hide: scalars, whose binding gives the name the same type.</summary>
    internal static IReadOnlyList<string> HidableVariables { get; } = ["i1", "i2", "u1", "d1", "b1", "b2", "y1", "x1", "x2"];

    /// <summary>The identifiers of the abstract types: values of the type <c>type</c> of the set <c>network</c>.</summary>
    internal static IReadOnlyList<string> AbstractTypeIdentifiers { get; } = ["net.IP", "net.CIDR"];

    /// <summary>The overloads of the extension sets, by set.</summary>
    internal static IReadOnlyList<ProfileOverload> ExtensionOverloads { get; } = ExtensionRows();

    /// <summary>
    /// The abstract type of a node, or <see langword="null"/> when its type is not abstract: <c>ip(s)</c> and <c>c.ip()</c> are
    /// <c>net.IP</c>, <c>cidr(s)</c> and <c>c.masked()</c> are <c>net.CIDR</c>. The generator makes no other node of an
    /// abstract type, and one stands only as an argument of <c>string</c>, <c>==</c>, <c>!=</c>, <c>type</c>, <c>containsIP</c> and
    /// <c>containsCIDR</c> or as the receiver of the methods of the set; the minimizer evaluates it as <c>string(node)</c>.
    /// </summary>
    /// <param name="expr">The node.</param>
    internal static ProfileType? AbstractResult(Expr expr) => expr is Call { Function: "ip" or "cidr" or "masked" } call
        ? ExtensionOverloads.FirstOrDefault(overload => overload.Result.IsAbstract() && overload.Function == call.Function
            && overload.Receiver == call.Receiver && overload.Parameters.Count == call.Args.Count)?.Result
        : null;

    /// <summary>Whether a function of an extension set is called globally, with a namespace or without one: <c>math.abs</c>, <c>ip</c>.</summary>
    /// <param name="function">The function.</param>
    internal static bool IsExtensionGlobal(string function) => ExtensionOverloads.Any(overload => !overload.Receiver && overload.Function == function);

    // The overloads of the set optional whose result is an optional, by identifier: a call that carries one is an optional node. A table
    // of its own, not read from the rows, so that it does not depend on the order of the initializers of the partial class.
    private static readonly HashSet<string> OptionalResultIds = new(StringComparer.Ordinal)
    {
        "optional_of", "optional_ofNonZeroValue", "optional_none", "optional_or_optional", OptionalSelectOverload,
        "list_optindex_optional_int", "optional_list_optindex_optional_int", "map_optindex_optional_value",
        "optional_map_optindex_optional_value", "optional_list_index_int", "optional_map_index_value", "list_first", "list_last",
    };

    /// <summary>
    /// Whether the value of a node is an optional: a call of an overload of the set <c>optional</c> that gives one, <c>e.?f</c>, a
    /// selection on an optional operand, <c>optMap</c> and <c>optFlatMap</c>, a conditional whose branches are both optional nodes, and
    /// the selection of an entry of a map literal whose value is an optional node. The oracle returns a value as <c>cel.expr.Value</c>,
    /// which has no form for an optional, so the generator keeps such a node away from the root, from <c>dyn()</c>, from the element of
    /// a plain literal, from a range and from <c>in</c>, and a case that is such a node is asked for as a pair of its own
    /// (<see cref="ExprPrinter.PrintCase"/>).
    /// </summary>
    /// <param name="expr">The node.</param>
    internal static bool IsOptional(Expr expr) => expr switch
    {
        Call { Overload: { } id } => OptionalResultIds.Contains(id),
        Call { Function: Conditional, Args: [_, { } first, { } second] } => IsOptional(first) && IsOptional(second),
        Select { Optional: true } => true,
        Select { Operand: MapLiteral map } select => map.Entries.Any(entry =>
            !entry.Optional && entry.Key is StringLiteral key && key.Value == select.Field && IsOptional(entry.Value)),
        Select select => IsOptional(select.Operand),
        Comprehension { Macro: OptMapMacro or OptFlatMapMacro } => true,
        _ => false,
    };

    /// <summary>
    /// Whether a node is a list or map literal that holds an optional node as a plain element or value, as <c>optional.unwrap([…])</c>
    /// takes one, or as the shape <c>has({'k': optional.none()}.k.f)</c> has: the oracle cannot return such a literal either, so a
    /// case that is one is asked for as its size (<see cref="ExprPrinter.PrintCase"/>).
    /// </summary>
    /// <param name="expr">The node.</param>
    internal static bool HoldsOptional(Expr expr) => expr switch
    {
        ListLiteral list => list.Elements.Where((element, position) => !list.OptionalPositions.Contains(position)).Any(IsOptional),
        MapLiteral map => map.Entries.Any(entry => !entry.Optional && IsOptional(entry.Value)),
        _ => false,
    };

    // The rows of the extension sets. The identifiers are those of cel-go and of docs/extensions.md. Self-contained: the order of the
    // initializers of a partial class does not follow the files.
    private static List<ProfileOverload> ExtensionRows()
    {
        List<ProfileOverload> rows = [];
        AddStringsRows(rows);
        AddMathRows(rows);
        AddListsRows(rows);
        AddSetsAndEncodersRows(rows);
        AddNetworkRows(rows);
        AddOptionalRows(rows);
        return rows;
    }

    private static void AddRow(List<ProfileOverload> rows, string set, string id, string function, bool receiver, ProfileType result, ProfileType? element, params ProfileType[] parameters) =>
        rows.Add(new ProfileOverload(id, function, parameters, result, receiver, Set: set, Element: element));

    private static void AddStringsRows(List<ProfileOverload> rows)
    {
        const ProfileType Int = ProfileType.IntType;
        const ProfileType Str = ProfileType.StringType;
        const ProfileType Lst = ProfileType.ListType;
        const ProfileType Dyn = ProfileType.DynType;
        AddRow(rows, StringsSet, "string_char_at_int", "charAt", true, Str, null, Str, Int);
        AddRow(rows, StringsSet, "string_index_of_string", "indexOf", true, Int, null, Str, Str);
        AddRow(rows, StringsSet, "string_index_of_string_int", "indexOf", true, Int, null, Str, Str, Int);
        AddRow(rows, StringsSet, "string_last_index_of_string", "lastIndexOf", true, Int, null, Str, Str);
        AddRow(rows, StringsSet, "string_last_index_of_string_int", "lastIndexOf", true, Int, null, Str, Str, Int);
        AddRow(rows, StringsSet, "string_lower_ascii", "lowerAscii", true, Str, null, Str);
        AddRow(rows, StringsSet, "string_upper_ascii", "upperAscii", true, Str, null, Str);
        AddRow(rows, StringsSet, "string_replace_string_string", "replace", true, Str, null, Str, Str, Str);
        AddRow(rows, StringsSet, "string_replace_string_string_int", "replace", true, Str, null, Str, Str, Str, Int);
        AddRow(rows, StringsSet, "string_split_string", "split", true, Lst, Str, Str, Str);
        AddRow(rows, StringsSet, "string_split_string_int", "split", true, Lst, Str, Str, Str, Int);
        AddRow(rows, StringsSet, "string_substring_int", "substring", true, Str, null, Str, Int);
        AddRow(rows, StringsSet, "string_substring_int_int", "substring", true, Str, null, Str, Int, Int);
        AddRow(rows, StringsSet, "string_trim", "trim", true, Str, null, Str);
        AddRow(rows, StringsSet, "string_reverse", "reverse", true, Str, null, Str);
        AddRow(rows, StringsSet, "list_join", "join", true, Str, Str, Lst);
        AddRow(rows, StringsSet, "list_join_string", "join", true, Str, Str, Lst, Str);
        AddRow(rows, StringsSet, "string_format", "format", true, Str, Dyn, Str, Lst);
        AddRow(rows, StringsSet, "strings_quote", "strings.quote", false, Str, null, Str);
    }

    // math: the macros greatest and least expand to the overloads of math.@max and math.@min.
    private static void AddMathRows(List<ProfileOverload> rows)
    {
        const ProfileType Int = ProfileType.IntType;
        const ProfileType Uint = ProfileType.UintType;
        const ProfileType Dbl = ProfileType.DoubleType;
        const ProfileType Lst = ProfileType.ListType;
        const ProfileType Dyn = ProfileType.DynType;
        ProfileType[] numbers = [Dbl, Int, Uint];
        foreach ((string function, string name) in new[] { ("math.greatest", "max"), ("math.least", "min") })
        {
            foreach (ProfileType kind in numbers)
            {
                AddRow(rows, MathSet, $"math_@{name}_{kind.Name()}", function, false, kind, null, kind);
                AddRow(rows, MathSet, $"math_@{name}_list_{kind.Name()}", function, false, kind, kind, Lst);
                foreach (ProfileType right in numbers)
                {
                    AddRow(rows, MathSet, $"math_@{name}_{kind.Name()}_{right.Name()}", function, false, kind == right ? kind : Dyn, null, kind, right);
                }
            }
        }

        foreach ((string function, string name) in new[] { ("math.ceil", "ceil"), ("math.floor", "floor"), ("math.round", "round"), ("math.trunc", "trunc") })
        {
            AddRow(rows, MathSet, $"math_{name}_double", function, false, Dbl, null, Dbl);
        }

        foreach ((string function, string name) in new[] { ("math.isInf", "isInf"), ("math.isNaN", "isNaN"), ("math.isFinite", "isFinite") })
        {
            AddRow(rows, MathSet, $"math_{name}_double", function, false, ProfileType.BoolType, null, Dbl);
        }

        foreach (ProfileType kind in numbers)
        {
            AddRow(rows, MathSet, $"math_abs_{kind.Name()}", "math.abs", false, kind, null, kind);
            AddRow(rows, MathSet, $"math_sign_{kind.Name()}", "math.sign", false, kind, null, kind);
            AddRow(rows, MathSet, $"math_sqrt_{kind.Name()}", "math.sqrt", false, Dbl, null, kind);
        }

        foreach (ProfileType kind in new[] { Int, Uint })
        {
            foreach (string name in new[] { "bitAnd", "bitOr", "bitXor" })
            {
                AddRow(rows, MathSet, $"math_{name}_{kind.Name()}_{kind.Name()}", "math." + name, false, kind, null, kind, kind);
            }

            AddRow(rows, MathSet, $"math_bitNot_{kind.Name()}_{kind.Name()}", "math.bitNot", false, kind, null, kind);
            foreach (string name in new[] { "bitShiftLeft", "bitShiftRight" })
            {
                AddRow(rows, MathSet, $"math_{name}_{kind.Name()}_int", "math." + name, false, kind, null, kind, Int);
            }
        }
    }

    private static void AddListsRows(List<ProfileOverload> rows)
    {
        const ProfileType Int = ProfileType.IntType;
        const ProfileType Lst = ProfileType.ListType;
        AddRow(rows, ListsSet, "list_slice", "slice", true, Lst, null, Lst, Int, Int);
        AddRow(rows, ListsSet, "list_flatten", "flatten", true, Lst, null, Lst);
        AddRow(rows, ListsSet, "list_flatten_int", "flatten", true, Lst, null, Lst, Int);
        foreach ((string name, ProfileType kind) in new[]
        {
            ("int", Int), ("uint", ProfileType.UintType), ("double", ProfileType.DoubleType), ("bool", ProfileType.BoolType),
            ("google.protobuf.Duration", ProfileType.DurationType), ("google.protobuf.Timestamp", ProfileType.TimestampType),
            ("string", ProfileType.StringType), ("bytes", ProfileType.BytesType),
        })
        {
            AddRow(rows, ListsSet, $"list_{name}_sort", "sort", true, Lst, kind, Lst);
        }

        AddRow(rows, ListsSet, "lists_range", "lists.range", false, Lst, Int, Int);
        AddRow(rows, ListsSet, "list_reverse", "reverse", true, Lst, null, Lst);
        AddRow(rows, ListsSet, "list_distinct", "distinct", true, Lst, null, Lst);
    }

    private static void AddSetsAndEncodersRows(List<ProfileOverload> rows)
    {
        const ProfileType Lst = ProfileType.ListType;
        foreach (string name in new[] { "contains", "intersects", "equivalent" })
        {
            AddRow(rows, SetsSet, $"list_sets_{name}_list", "sets." + name, false, ProfileType.BoolType, null, Lst, Lst);
        }

        AddRow(rows, EncodersSet, "base64_decode_string", "base64.decode", false, ProfileType.BytesType, null, ProfileType.StringType);
        AddRow(rows, EncodersSet, "base64_encode_bytes", "base64.encode", false, ProfileType.StringType, null, ProfileType.BytesType);
        AddRow(rows, EncodersSet, "json_encode_dyn", "json.encode", false, ProfileType.StringType, null, ProfileType.DynType);
    }

    private static void AddNetworkRows(List<ProfileOverload> rows)
    {
        const ProfileType Int = ProfileType.IntType;
        const ProfileType Bool = ProfileType.BoolType;
        const ProfileType Str = ProfileType.StringType;
        const ProfileType Ip = ProfileType.IpType;
        const ProfileType Cidr = ProfileType.CidrType;
        AddRow(rows, NetworkSet, "string_to_ip", "ip", false, Ip, null, Str);
        AddRow(rows, NetworkSet, "string_to_cidr", "cidr", false, Cidr, null, Str);
        AddRow(rows, NetworkSet, "cidr_ip", "ip", true, Ip, null, Cidr);
        AddRow(rows, NetworkSet, "ip_to_string", StringConversion, false, Str, null, Ip);
        AddRow(rows, NetworkSet, "cidr_to_string", StringConversion, false, Str, null, Cidr);
        AddRow(rows, NetworkSet, "is_ip", "isIP", false, Bool, null, Str);
        AddRow(rows, NetworkSet, "is_cidr", "isCIDR", false, Bool, null, Str);
        AddRow(rows, NetworkSet, "ip_is_canonical", "ip.isCanonical", false, Bool, null, Str);
        AddRow(rows, NetworkSet, "ip_family", "family", true, Int, null, Ip);
        foreach ((string name, string id) in new[]
        {
            ("isLoopback", "ip_is_loopback"), ("isUnspecified", "ip_is_unspecified"), ("isLinkLocalUnicast", "ip_is_link_local_unicast"),
            ("isLinkLocalMulticast", "ip_is_link_local_multicast"), ("isGlobalUnicast", "ip_is_global_unicast"),
        })
        {
            AddRow(rows, NetworkSet, id, name, true, Bool, null, Ip);
        }

        AddRow(rows, NetworkSet, "cidr_prefix_length", "prefixLength", true, Int, null, Cidr);
        AddRow(rows, NetworkSet, "cidr_is_mask", "isMask", true, Bool, null, Cidr);
        AddRow(rows, NetworkSet, "cidr_masked", "masked", true, Cidr, null, Cidr);
        AddRow(rows, NetworkSet, "cidr_contains_ip_ip", "containsIP", true, Bool, null, Cidr, Ip);
        AddRow(rows, NetworkSet, "cidr_contains_ip_string", "containsIP", true, Bool, null, Cidr, Str);
        AddRow(rows, NetworkSet, "cidr_contains_cidr", "containsCIDR", true, Bool, null, Cidr, Cidr);
        AddRow(rows, NetworkSet, "cidr_contains_cidr_string", "containsCIDR", true, Bool, null, Cidr, Str);
    }

    // optional: the 18 overloads of cel-go v0.32.0 with OptionalTypesVersion(2), in the order of cel/library.go, and the macros
    // optMap and optFlatMap, which expand to calls of them and are no rows. The parameter A stands for V and K of cel-go; the receiver
    // of the optional index and of the index of an optional is an optional or a list or a map.
    private static void AddOptionalRows(List<ProfileOverload> rows)
    {
        const ProfileType A = ProfileType.ParameterAType;
        const ProfileType Int = ProfileType.IntType;
        const ProfileType Str = ProfileType.StringType;
        const ProfileType Lst = ProfileType.ListType;
        const ProfileType Map = ProfileType.MapType;
        const ProfileType Opt = ProfileType.OptionalType;
        AddRow(rows, OptionalSet, "optional_of", "optional.of", false, Opt, null, A);
        AddRow(rows, OptionalSet, "optional_ofNonZeroValue", "optional.ofNonZeroValue", false, Opt, null, A);
        AddRow(rows, OptionalSet, "optional_none", "optional.none", false, Opt, null);
        AddRow(rows, OptionalSet, "optional_value", "value", true, A, null, Opt);
        AddRow(rows, OptionalSet, "optional_hasValue", "hasValue", true, ProfileType.BoolType, null, Opt);
        // The argument of or is an optional for cel-go; here it is the parameter A, so that a call whose argument is a dyn value that is
        // no optional — optional.none().or(dyn(1)) — is still named by the overload: optional_or_optional(optional_type, int), the
        // signature of the twelve records of the registry that belong to it.
        AddRow(rows, OptionalSet, "optional_or_optional", "or", true, Opt, null, Opt, A);
        AddRow(rows, OptionalSet, "optional_orValue_value", "orValue", true, A, null, Opt, A);
        AddRow(rows, OptionalSet, OptionalSelectOverload, "_?._", false, Opt, null, ProfileType.DynType, Str);
        AddRow(rows, OptionalSet, "list_optindex_optional_int", OptionalIndex, false, Opt, null, Lst, Int);
        AddRow(rows, OptionalSet, "optional_list_optindex_optional_int", OptionalIndex, false, Opt, null, Opt, Int);
        AddRow(rows, OptionalSet, "map_optindex_optional_value", OptionalIndex, false, Opt, null, Map, A);
        AddRow(rows, OptionalSet, "optional_map_optindex_optional_value", OptionalIndex, false, Opt, null, Opt, A);
        AddRow(rows, OptionalSet, "optional_list_index_int", Index, false, Opt, null, Opt, Int);
        AddRow(rows, OptionalSet, "optional_map_index_value", Index, false, Opt, null, Opt, A);
        AddRow(rows, OptionalSet, "list_first", "first", true, Opt, null, Lst);
        AddRow(rows, OptionalSet, "list_last", "last", true, Opt, null, Lst);
        AddRow(rows, OptionalSet, "optional_unwrap", "optional.unwrap", false, Lst, null, Lst);
        AddRow(rows, OptionalSet, "optional_unwrapOpt", "unwrapOpt", true, Lst, null, Lst);
    }
}
