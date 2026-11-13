using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Great.Parts.Cel.Eng.Difftest.Profile;

/// <summary>
/// The forms of the extension sets (docs/design/eng-difftest.md, "Profile of the extension sets"). A case of the extension profile
/// has them among the forms of a target, with the weight <see cref="ExtensionWeight"/>; every form builds its call from the
/// types of its overload, so the expression type checks, except the calls that carry a <see cref="Call.Fault"/>.
/// </summary>
internal sealed partial class ExprBuilder
{
    // The weight of the extension forms of a target among the other forms of the target.
    private const int ExtensionWeight = 10;

    // A prefix length or a precision that the generator takes from the boundaries.
    private static readonly long[] Counts = [-2, -1, 0, 1, 2, 100];

    private static readonly long[] Depths = [-1, 0, 1, 2, 5];

    private static readonly long[] Shifts = [-1, 0, 1, 63, 64, 65, 100];

    private static readonly long[] RangeSizes = [-1, 0, 1, 2, 5, 100, 1000];

    private static readonly long[] Offsets = [-2, -1, 0, 1, 2, 30, long.MinValue, long.MaxValue];

    // The pieces of text between the clauses of a format string.
    private static readonly string[] FormatPieces = [string.Empty, "a", " ", ": ", "x=", "\u00e9", "100%%", "%%", "\U0001f600", "\n"];

    private static readonly string[] FormatPrecisions = ["0", "2", "6", "10", "100"];

    // The clauses of a format string that no function takes, a clause with a width, a lone %, a % with a dot alone and a precision
    // over the limit.
    private static readonly string[] FormatFaults = ["%z", "%5d", "%", "%.", "%.101f"];

    // The numbers that test the rounding and the forms of format.
    private static readonly double[] FormatDoubles = [0.5, 1.5, 2.5, 0.125, 0.375, 1e21, 1e-7, 9007199254740993.0, -0.0, 100000.0, 0.1];

    private static readonly ProfileType[] AllKinds =
    [
        ProfileType.IntType, ProfileType.UintType, ProfileType.DoubleType, ProfileType.BoolType, ProfileType.StringType,
        ProfileType.BytesType, ProfileType.TimestampType, ProfileType.DurationType,
    ];

    private static readonly ProfileType[] Numbers = [ProfileType.IntType, ProfileType.UintType, ProfileType.DoubleType];

    private static readonly ProfileType[] MapKeyKinds = [ProfileType.IntType, ProfileType.StringType, ProfileType.BoolType];

    // The overloads of math.* that are not greatest and least, by result type.
    private static readonly Dictionary<ProfileType, ProfileOverload[]> MathRows = ProfileTables.ExtensionOverloads
        .Where(overload => overload.Set == ProfileTables.MathSet && !overload.Function.StartsWith("math.greatest", StringComparison.Ordinal)
            && !overload.Function.StartsWith("math.least", StringComparison.Ordinal))
        .GroupBy(overload => overload.Result)
        .ToDictionary(group => group.Key, group => group.ToArray());

    private static readonly ProfileOverload[] SortRows = [.. ProfileTables.ExtensionOverloads.Where(overload => overload.Function == "sort")];

    private static readonly string[] NetworkPredicates =
        ["isLoopback", "isUnspecified", "isLinkLocalUnicast", "isLinkLocalMulticast", "isGlobalUnicast"];

    private static readonly Dictionary<ProfileType, ExtForm[]> ExtensionForms = new()
    {
        [ProfileType.StringType] =
        [
            ExtForm.CharAt, ExtForm.StringUnary, ExtForm.StringUnary, ExtForm.Replace, ExtForm.Substring, ExtForm.Quote, ExtForm.Join,
            ExtForm.Format, ExtForm.Format, ExtForm.Base64Encode, ExtForm.JsonEncode, ExtForm.NetString, ExtForm.Bind, ExtForm.TwoVariableIndex,
            ExtForm.OptionalValue, ExtForm.OptionalOrValue,
        ],
        [ProfileType.IntType] =
        [
            ExtForm.IndexOf, ExtForm.IndexOf, ExtForm.LastIndexOf, ExtForm.MathCall, ExtForm.MathExtreme, ExtForm.RangeSize, ExtForm.Family,
            ExtForm.PrefixLength, ExtForm.TwoVariableSize, ExtForm.Bind, ExtForm.TwoVariableIndex, ExtForm.OptionalValue, ExtForm.OptionalOrValue,
        ],
        [ProfileType.UintType] = [ExtForm.MathCall, ExtForm.MathExtreme, ExtForm.Bind, ExtForm.OptionalValue, ExtForm.OptionalOrValue],
        [ProfileType.DoubleType] = [ExtForm.MathCall, ExtForm.MathCall, ExtForm.MathExtreme, ExtForm.Bind, ExtForm.OptionalValue, ExtForm.OptionalOrValue],
        [ProfileType.BoolType] =
        [
            ExtForm.MathCall, ExtForm.Sets, ExtForm.IsText, ExtForm.IpPredicate, ExtForm.Contains, ExtForm.IsMask, ExtForm.NetEquality,
            ExtForm.TypeEquality, ExtForm.TwoVariableQuantifier, ExtForm.TwoVariableQuantifier, ExtForm.TwoVariableIn, ExtForm.TwoVariableEqual,
            ExtForm.Bind, ExtForm.OptionalHasValue, ExtForm.OptionalHasValue, ExtForm.OptionalEquality, ExtForm.OptionalTypeEquality,
            ExtForm.OptionalPresence, ExtForm.OptionalValue, ExtForm.OptionalOrValue,
        ],
        [ProfileType.BytesType] = [ExtForm.Base64Decode, ExtForm.Bind, ExtForm.OptionalValue, ExtForm.OptionalOrValue],
        [ProfileType.ListType] =
        [
            ExtForm.Split, ExtForm.Slice, ExtForm.Flatten, ExtForm.Sort, ExtForm.Sort, ExtForm.ReverseList, ExtForm.Distinct, ExtForm.Range,
            ExtForm.SortBy, ExtForm.TwoVariableTransform, ExtForm.Bind, ExtForm.OptionalUnwrap, ExtForm.OptionalList, ExtForm.OptionalList,
            ExtForm.OptionalValue, ExtForm.OptionalOrValue,
        ],
        [ProfileType.NullType] = [ExtForm.Bind, ExtForm.OptionalValue, ExtForm.OptionalOrValue],
        [ProfileType.MapType] = [ExtForm.Bind, ExtForm.OptionalMap, ExtForm.OptionalMap, ExtForm.OptionalValue, ExtForm.OptionalOrValue],
        [ProfileType.TypeType] = [ExtForm.Bind, ExtForm.OptionalTypeOf, ExtForm.OptionalValue, ExtForm.OptionalOrValue],
        [ProfileType.TimestampType] = [ExtForm.Bind, ExtForm.OptionalValue, ExtForm.OptionalOrValue],
        [ProfileType.DurationType] = [ExtForm.Bind, ExtForm.OptionalValue, ExtForm.OptionalOrValue],
    };

    private enum ExtForm
    {
        CharAt,
        IndexOf,
        LastIndexOf,
        StringUnary,
        Replace,
        Substring,
        Quote,
        Join,
        Format,
        Split,
        Base64Encode,
        Base64Decode,
        JsonEncode,
        NetString,
        MathCall,
        MathExtreme,
        RangeSize,
        Family,
        PrefixLength,
        Sets,
        IsText,
        IpPredicate,
        Contains,
        IsMask,
        NetEquality,
        TypeEquality,
        Slice,
        Flatten,
        Sort,
        ReverseList,
        Distinct,
        Range,
        SortBy,
        TwoVariableQuantifier,
        TwoVariableTransform,
        TwoVariableIn,
        TwoVariableEqual,
        TwoVariableSize,
        TwoVariableIndex,
        Bind,
        OptionalHasValue,
        OptionalValue,
        OptionalOrValue,
        OptionalEquality,
        OptionalTypeEquality,
        OptionalTypeOf,
        OptionalPresence,
        OptionalUnwrap,
        OptionalList,
        OptionalMap,
    }

    // Builds one argument of a call at a depth within a budget of nodes.
    private delegate Built ArgBuilder(int depth, int budget);

    private static StaticType Of(ProfileType type) => StaticType.Of(type);

    // A call of an extension function whose arguments are built in order, each within what is left of the limit.
    private static Built? ExtCall(string function, bool receiver, string? overload, StaticType? type, int depth, int limit, params ArgBuilder[] args)
    {
        if (limit < 1 + args.Length || depth + 1 > ProfileTables.MaxDepth)
        {
            return null;
        }

        List<Expr> built = [];
        int nodes = 1;
        for (int i = 0; i < args.Length; i++)
        {
            Built arg = args[i](depth + 1, limit - nodes - (args.Length - 1 - i));
            built.Add(arg.Expr);
            nodes += arg.Nodes;
        }

        return new Built(new Call(function, built) { Receiver = receiver, Overload = overload }, nodes, type);
    }

    private ArgBuilder Arg(ProfileType type) => (depth, budget) => Make(type, depth, budget);

    // A string argument: a string of the pool of the set strings one time in two.
    private Built ExtString(int depth, int budget) =>
        pcg.OneIn(2) ? PoolString(pcg.Pick(ExtensionStrings.Pool)) : Make(ProfileType.StringType, depth, budget);

    private Built PoolString(string text) => new(StringLiteral(text), 1, Of(ProfileType.StringType));

    // A string that is a part of the receiver, one time in two when the receiver is a literal; otherwise any string argument.
    private Built RelatedString(Expr? receiver, int depth, int budget)
    {
        if (receiver is StringLiteral literal && pcg.OneIn(2))
        {
            List<string> units = [.. literal.Value.EnumerateRunes().Select(rune => rune.ToString())];
            int start = pcg.Below(units.Count + 1);
            return PoolString(string.Concat(units.Skip(start).Take(pcg.Below(units.Count - start + 1))));
        }

        return ExtString(depth, budget);
    }

    private static int? LengthOf(Expr? expr) => expr is StringLiteral literal ? ExtensionStrings.Length(literal.Value) : null;

    // An integer literal from the boundaries, or an int expression one time in six.
    private Built IntFrom(long[] values, int depth, int budget) =>
        pcg.OneIn(6)
            ? Make(ProfileType.IntType, depth, budget)
            : new Built(new IntLiteral(pcg.Pick(values), IntegerForm.DecimalDigits), 1, Of(ProfileType.IntType));

    // An index or an offset: around the length of the receiver when it is known, otherwise from the boundaries.
    private Built IndexArg(int? length, int depth, int budget)
    {
        if (length is { } known && pcg.OneIn(2))
        {
            return new Built(new IntLiteral(pcg.Pick<long>([known - 1, known, known + 1]), IntegerForm.DecimalDigits), 1, Of(ProfileType.IntType));
        }

        return IntFrom(Offsets, depth, budget);
    }

    private Built VariableNamed(string name) => VariableNode(ProfileTables.Variables.First(variable => variable.Name == name));

    // The name of a variable of a macro: the first that no enclosing macro uses.
    private string FreshName(IEnumerable<string> names) => names.First(name => scope.TrueForAll(local => local.Name != name));

    // ---- The forms ----

    // One of the extension forms of a target; null when the form finds no room.
    private Built? Extension(ProfileType target, int depth, int limit) => Builders[pcg.Pick(ExtensionForms[target])](this, target, depth, limit);

    // The builder of each form: the target, the depth and the limit of nodes go in.
    private static readonly Dictionary<ExtForm, Func<ExprBuilder, ProfileType, int, int, Built?>> Builders = new()
    {
        [ExtForm.CharAt] = (b, _, d, l) => b.CharAt(d, l),
        [ExtForm.IndexOf] = (b, _, d, l) => b.IndexOf(last: false, d, l),
        [ExtForm.LastIndexOf] = (b, _, d, l) => b.IndexOf(last: true, d, l),
        [ExtForm.StringUnary] = (b, _, d, l) => b.StringUnary(d, l),
        [ExtForm.Replace] = (b, _, d, l) => b.Replace(d, l),
        [ExtForm.Substring] = (b, _, d, l) => b.Substring(d, l),
        [ExtForm.Quote] = (b, _, d, l) => ExtCall("strings.quote", false, "strings_quote", Of(ProfileType.StringType), d, l, b.ExtString),
        [ExtForm.Join] = (b, _, d, l) => b.Join(d, l),
        [ExtForm.Format] = (b, _, d, l) => b.FormatCall(d, l),
        [ExtForm.Split] = (b, _, d, l) => b.Split(d, l),
        [ExtForm.Base64Encode] = (b, _, d, l) => ExtCall("base64.encode", false, "base64_encode_bytes", Of(ProfileType.StringType), d, l, b.Arg(ProfileType.BytesType)),
        [ExtForm.Base64Decode] = (b, _, d, l) => ExtCall("base64.decode", false, "base64_decode_string", Of(ProfileType.BytesType), d, l, b.Base64Argument),
        [ExtForm.JsonEncode] = (b, _, d, l) => ExtCall("json.encode", false, "json_encode_dyn", Of(ProfileType.StringType), d, l, b.JsonArgument),
        [ExtForm.NetString] = (b, _, d, l) => b.NetString(d, l),
        [ExtForm.MathCall] = (b, target, d, l) => b.MathCall(target, d, l),
        [ExtForm.MathExtreme] = (b, target, d, l) => b.MathExtreme(target, d, l),
        [ExtForm.RangeSize] = (b, _, d, l) => b.RangeSize(d, l),
        [ExtForm.Family] = (b, _, d, l) => NetworkRoom(d, l, 1) ? ExtCall("family", true, "ip_family", Of(ProfileType.IntType), d, l, b.IpNode) : null,
        [ExtForm.PrefixLength] = (b, _, d, l) => NetworkRoom(d, l, 1) ? ExtCall("prefixLength", true, "cidr_prefix_length", Of(ProfileType.IntType), d, l, b.CidrNode) : null,
        [ExtForm.Sets] = (b, _, d, l) => b.Sets(d, l),
        [ExtForm.IsText] = (b, _, d, l) => b.IsText(d, l),
        [ExtForm.IpPredicate] = (b, _, d, l) => b.IpPredicate(d, l),
        [ExtForm.Contains] = (b, _, d, l) => b.ContainsCall(d, l),
        [ExtForm.IsMask] = (b, _, d, l) => NetworkRoom(d, l, 1) ? ExtCall("isMask", true, "cidr_is_mask", Of(ProfileType.BoolType), d, l, b.CidrNode) : null,
        [ExtForm.NetEquality] = (b, _, d, l) => b.NetEquality(d, l),
        [ExtForm.TypeEquality] = (b, _, d, l) => b.TypeEquality(d, l),
        [ExtForm.Slice] = (b, _, d, l) => b.Slice(d, l),
        [ExtForm.Flatten] = (b, _, d, l) => b.Flatten(d, l),
        [ExtForm.Sort] = (b, _, d, l) => b.Sort(d, l),
        [ExtForm.ReverseList] = (b, _, d, l) => b.SameListCall("reverse", "list_reverse", d, l),
        [ExtForm.Distinct] = (b, _, d, l) => b.SameListCall("distinct", "list_distinct", d, l),
        [ExtForm.Range] = (b, _, d, l) => ExtCall("lists.range", false, "lists_range", StaticType.ListOf(Of(ProfileType.IntType)), d, l, (_, _) => b.RangeLiteral()),
        [ExtForm.SortBy] = (b, _, d, l) => b.SortBy(d, l),
        [ExtForm.TwoVariableQuantifier] = (b, _, d, l) => b.TwoVariableQuantifier(d, l),
        [ExtForm.TwoVariableTransform] = (b, _, d, l) => b.TwoVariableMacro("transformList", d, l, null),
        [ExtForm.TwoVariableIn] = (b, _, d, l) => b.TwoVariableIn(d, l),
        [ExtForm.TwoVariableEqual] = (b, _, d, l) => b.TwoVariableEqual(d, l),
        [ExtForm.TwoVariableSize] = (b, _, d, l) => b.TwoVariableSize(d, l),
        [ExtForm.TwoVariableIndex] = (b, target, d, l) => b.TwoVariableIndex(target, d, l),
        [ExtForm.Bind] = (b, target, d, l) => b.Bind(target, d, l),
        [ExtForm.OptionalHasValue] = (b, _, d, l) => b.OptHasValue(d, l),
        [ExtForm.OptionalValue] = (b, target, d, l) => b.OptValue(target, d, l),
        [ExtForm.OptionalOrValue] = (b, target, d, l) => b.OptOrValue(target, d, l),
        [ExtForm.OptionalEquality] = (b, _, d, l) => b.OptEquality(d, l),
        [ExtForm.OptionalTypeEquality] = (b, _, d, l) => b.OptTypeEquality(d, l),
        [ExtForm.OptionalTypeOf] = (b, _, d, l) => b.OptTypeOf(d, l),
        [ExtForm.OptionalPresence] = (b, _, d, l) => b.OptPresence(d, l),
        [ExtForm.OptionalUnwrap] = (b, _, d, l) => b.OptUnwrap(d, l),
        [ExtForm.OptionalList] = (b, _, d, l) => b.OptList(d, l),
        [ExtForm.OptionalMap] = (b, _, d, l) => b.OptMap(d, l),
    };

    // ---- strings ----

    private Built? CharAt(int depth, int limit)
    {
        int? length = null;
        return ExtCall(
            "charAt", true, "string_char_at_int", Of(ProfileType.StringType), depth, limit,
            (d, b) =>
            {
                Built receiver = ExtString(d, b);
                length = LengthOf(receiver.Expr);
                return receiver;
            },
            (d, b) => IndexArg(length, d, b));
    }

    // indexOf and lastIndexOf with a needle and, one time in two, an offset.
    private Built? IndexOf(bool last, int depth, int limit)
    {
        Expr? receiver = null;
        bool offset = pcg.OneIn(2);
        List<ArgBuilder> args =
        [
            (d, b) =>
            {
                Built built = ExtString(d, b);
                receiver = built.Expr;
                return built;
            },
            (d, b) => RelatedString(receiver, d, b),
        ];
        if (offset)
        {
            args.Add((d, b) => IndexArg(LengthOf(receiver), d, b));
        }

        string id = (last ? "string_last_index_of_string" : "string_index_of_string") + (offset ? "_int" : string.Empty);
        return ExtCall(last ? "lastIndexOf" : "indexOf", true, id, Of(ProfileType.IntType), depth, limit, [.. args]);
    }

    private Built? StringUnary(int depth, int limit)
    {
        (string function, string id) = pcg.Pick<(string, string)>(
            [("lowerAscii", "string_lower_ascii"), ("upperAscii", "string_upper_ascii"), ("trim", "string_trim"), ("reverse", "string_reverse")]);
        return ExtCall(function, true, id, Of(ProfileType.StringType), depth, limit, ExtString);
    }

    private Built? Replace(int depth, int limit)
    {
        Expr? receiver = null;
        bool count = pcg.OneIn(2);
        List<ArgBuilder> args =
        [
            (d, b) =>
            {
                Built built = ExtString(d, b);
                receiver = built.Expr;
                return built;
            },
            (d, b) => RelatedString(receiver, d, b),
            ExtString,
        ];
        if (count)
        {
            args.Add((d, b) => IntFrom(Counts, d, b));
        }

        return ExtCall("replace", true, "string_replace_string_string" + (count ? "_int" : string.Empty), Of(ProfileType.StringType), depth, limit, [.. args]);
    }

    private Built? Split(int depth, int limit)
    {
        Expr? receiver = null;
        bool count = pcg.OneIn(2);
        List<ArgBuilder> args =
        [
            (d, b) =>
            {
                Built built = ExtString(d, b);
                receiver = built.Expr;
                return built;
            },
            (d, b) => RelatedString(receiver, d, b),
        ];
        if (count)
        {
            args.Add((d, b) => IntFrom(Counts, d, b));
        }

        return ExtCall("split", true, "string_split_string" + (count ? "_int" : string.Empty), StaticType.ListOf(Of(ProfileType.StringType)), depth, limit, [.. args]);
    }

    private Built? Substring(int depth, int limit)
    {
        int? length = null;
        bool end = pcg.OneIn(2);
        List<ArgBuilder> args =
        [
            (d, b) =>
            {
                Built built = ExtString(d, b);
                length = LengthOf(built.Expr);
                return built;
            },
            (d, b) => IndexArg(length, d, b),
        ];
        if (end)
        {
            args.Add((d, b) => IndexArg(length, d, b));
        }

        return ExtCall("substring", true, "string_substring_int" + (end ? "_int" : string.Empty), Of(ProfileType.StringType), depth, limit, [.. args]);
    }

    // join on a list of strings: a literal, the variable ls, the variable lx of dyn, or the result of split.
    private Built? Join(int depth, int limit)
    {
        bool separator = pcg.OneIn(2);
        List<ArgBuilder> args = [StringList];
        if (separator)
        {
            args.Add(ExtString);
        }

        return ExtCall("join", true, separator ? "list_join_string" : "list_join", Of(ProfileType.StringType), depth, limit, [.. args]);
    }

    private Built StringList(int depth, int budget)
    {
        switch (pcg.Below(5))
        {
            case 0:
                return VariableNamed("ls");
            case 1:
                return VariableNamed("lx");
            case 2 when budget >= 3 && depth + 2 <= ProfileTables.MaxDepth:
                if (Split(depth, budget) is { } split)
                {
                    return split;
                }

                return VariableNamed("ls");
            default:
                return budget >= 2 && depth < ProfileTables.MaxDepth ? ElementList(ProfileType.StringType, depth, budget).List : VariableNamed("ls");
        }
    }

    // format: a format string made of text and clauses, with a fault one time in twelve, and its arguments: a list literal with an
    // element for each clause, or, one time in six, the variable lx. A format string that is a literal with a list literal is
    // checked at compilation on both sides, so a fault of the string or an element that does not fit makes a call that carries a
    // fault; dyn() around the string, or the variable lx, keeps the error for the evaluation.
    private Built? FormatCall(int depth, int limit)
    {
        if (limit < 3 || depth + 2 > ProfileTables.MaxDepth)
        {
            return null;
        }

        (string text, List<char> letters, bool faulty) = FormatString();
        int mode = pcg.Below(6);
        bool variableList = mode == 4;
        bool dynamicString = mode == 5;
        (Built arguments, bool mismatch) = variableList ? (VariableNamed("lx"), false) : FormatArguments(letters, limit, depth);
        StringLiteral literal = StringLiteral(text);
        Expr formatString = dynamicString ? new Call(ProfileTables.Dyn, [literal]) : literal;
        int total = 1 + (dynamicString ? 2 : 1) + arguments.Nodes;
        if (total > limit)
        {
            return null;
        }

        FormatsLx |= variableList;
        bool checkedAtCompilation = !dynamicString && !variableList;
        string? fault = checkedAtCompilation && (faulty || mismatch) ? "format" : null;
        return new Built(
            new Call("format", [formatString, arguments.Expr]) { Receiver = true, Overload = "string_format", Fault = fault },
            total,
            Of(ProfileType.StringType));
    }

    // The format string: text between 0 to 3 clauses, and, one time in twelve, a fault after them.
    private (string Text, List<char> Letters, bool Faulty) FormatString()
    {
        int clauses = pcg.Below(ProfileTables.MaxElements + 1);
        StringBuilder text = new();
        List<char> letters = [];
        for (int i = 0; i < clauses; i++)
        {
            char letter = "sdfebxXo"[pcg.Below(8)];
            text.Append(pcg.Pick(FormatPieces)).Append('%');
            if (pcg.OneIn(3))
            {
                text.Append('.').Append(pcg.Pick(FormatPrecisions));
            }

            text.Append(letter);
            letters.Add(letter);
        }

        text.Append(pcg.Pick(FormatPieces));
        bool faulty = pcg.OneIn(12);
        if (faulty)
        {
            text.Append(pcg.Pick(FormatFaults));
        }

        return (text.ToString(), letters, faulty);
    }

    // The list literal of a call of format: an element for each clause, and whether one of them does not fit its clause. An element with
    // a rich form needs room: a list or a map has elements of its own, dyn() wraps one more node.
    private (Built Arguments, bool Mismatch) FormatArguments(List<char> letters, int limit, int depth)
    {
        bool rich = limit >= 12 && depth + 3 <= ProfileTables.MaxDepth;
        bool mismatch = false;
        List<Expr> elements = [];
        int nodes = 3;
        for (int i = 0; i < letters.Count; i++)
        {
            (Built element, bool fits) = FormatElement(letters[i], rich ? Math.Min(5, limit - nodes - (letters.Count - 1 - i)) : 1);
            mismatch |= !fits;
            elements.Add(element.Expr);
            nodes += element.Nodes;
        }

        return (new Built(new ListLiteral(elements, pcg.OneIn(4)), 1 + elements.Sum(CountNodes), StaticType.ListOf(null)), mismatch);
    }

    private static int CountNodes(Expr expr) => 1 + expr switch
    {
        ListLiteral list => list.Elements.Sum(CountNodes),
        MapLiteral map => map.Entries.Sum(entry => CountNodes(entry.Key) + CountNodes(entry.Value)),
        Call call => call.Args.Sum(CountNodes),
        _ => 0,
    };

    // The element of a format list for a clause, of a kind the clause takes, or, one time in five, of another kind; and whether the
    // element fits: with room it is sometimes a list or a map, and dyn() around an element, which fits every clause.
    private (Built Element, bool Fits) FormatElement(char clause, int budget)
    {
        ProfileType[] takes = clause switch
        {
            'd' or 'f' or 'e' => Numbers,
            'b' => [ProfileType.BoolType, ProfileType.IntType, ProfileType.UintType],
            'o' => [ProfileType.IntType, ProfileType.UintType],
            'x' or 'X' => [ProfileType.IntType, ProfileType.UintType, ProfileType.StringType, ProfileType.BytesType],
            _ => [ProfileType.StringType, ProfileType.BytesType, ProfileType.BoolType, ProfileType.IntType, ProfileType.UintType, ProfileType.DoubleType, ProfileType.NullType],
        };

        // Every kind fits %s, so it has no element that does not.
        ProfileType[] other = [.. FormatKinds.Where(kind => !takes.Contains(kind))];
        bool fits = other.Length == 0 || !pcg.OneIn(5);
        if (clause == 's' && budget >= 3 && pcg.OneIn(3))
        {
            return (pcg.OneIn(2) && budget >= 5 ? FormatMap() : FormatList(budget), true);
        }

        Built element = FormatScalar(fits ? pcg.Pick(takes) : pcg.Pick(other));
        return !fits && budget >= 2 && pcg.OneIn(8) ? (Wrap(element), true) : (element, fits);
    }

    private static readonly ProfileType[] FormatKinds =
        [ProfileType.IntType, ProfileType.UintType, ProfileType.DoubleType, ProfileType.BoolType, ProfileType.StringType, ProfileType.BytesType, ProfileType.NullType];

    // A scalar of a kind in a format list: a literal, or a variable that holds a value the oracle reads as the library does.
    private Built FormatScalar(ProfileType kind)
    {
        switch (kind)
        {
            case ProfileType.DoubleType:
                if (pcg.OneIn(4))
                {
                    return VariableNamed("d1");
                }

                return pcg.OneIn(2)
                    ? new Built(new DoubleLiteral(pcg.Pick(FormatDoubles), pcg.Pick(Enum.GetValues<DoubleForm>())), 1, Of(kind))
                    : new Built(Literal(kind), 1, Of(kind));
            case ProfileType.StringType:
                return pcg.OneIn(5) ? VariableNamed("s1") : PoolString(pcg.Pick(ExtensionStrings.Pool));
            case ProfileType.BytesType:
                // Bytes that are valid UTF-8: a Go string keeps invalid bytes and a .NET string cannot.
                return new Built(BytesLiteral(Encoding.UTF8.GetBytes(pcg.Pick(ExtensionStrings.Pool))), 1, Of(kind));
            case ProfileType.NullType:
                return new Built(new NullLiteral(), 1, Of(kind));
            default:
                return new Built(Literal(kind), 1, Of(kind));
        }
    }

    // A list of up to two scalars that fits in the budget.
    private Built FormatList(int budget)
    {
        int count = budget >= 3 ? pcg.Below(3) : 0;
        List<Expr> elements = [];
        for (int i = 0; i < count; i++)
        {
            elements.Add(FormatScalar(pcg.Pick<ProfileType>([ProfileType.IntType, ProfileType.StringType, ProfileType.BoolType])).Expr);
        }

        return new Built(new ListLiteral(elements, TrailingComma: false), 1 + count, StaticType.ListOf(null));
    }

    // A map of two entries with string keys of the pool that differ, so that the order of the pairs by code points is tested.
    private Built FormatMap()
    {
        string first = pcg.Pick(ExtensionStrings.Pool);
        string second = pcg.Pick(ExtensionStrings.Pool);
        if (first == second)
        {
            second += "z";
        }

        MapLiteral map = new(
            [
                new MapEntry(StringLiteral(first), Literal(ProfileType.IntType)),
                new MapEntry(StringLiteral(second), Literal(ProfileType.BoolType)),
            ],
            TrailingComma: false);
        return new Built(map, 5, StaticType.MapOf(null, null));
    }

    // ---- math ----

    // greatest and least with 1 to 4 numeric arguments, and one time in twelve a call that the macro refuses.
    private Built? MathExtreme(ProfileType target, int depth, int limit)
    {
        bool greatest = pcg.OneIn(2);
        string function = greatest ? "math.greatest" : "math.least";
        string name = greatest ? "max" : "min";
        if (target == ProfileType.IntType && limit >= 4 && depth + 2 <= ProfileTables.MaxDepth && pcg.OneIn(12))
        {
            return RefusedExtreme(function);
        }

        int count = 1 + pcg.Below(4);
        ProfileType[] kinds = new ProfileType[count];
        bool mixed = count >= 2 && pcg.OneIn(3);
        for (int i = 0; i < count; i++)
        {
            kinds[i] = target;
        }

        if (mixed)
        {
            // A mixed pair of the table, then the other arguments of any number kind.
            ProfileType other = pcg.Pick(Numbers.Where(kind => kind != target).ToArray());
            kinds[pcg.Below(count)] = other;
        }

        string id = count switch
        {
            1 => $"math_@{name}_{kinds[0].Name()}",
            2 => $"math_@{name}_{kinds[0].Name()}_{kinds[1].Name()}",
            _ => $"math_@{name}_list_{kinds[0].Name()}",
        };
        // The result of numbers of different kinds has the type dyn: the call stands in dyn(), one level deeper.
        Built? call = ExtCall(function, false, id, Of(target), mixed ? depth + 1 : depth, mixed ? limit - 1 : limit, [.. kinds.Select(Arg)]);
        return mixed && call is { } built ? Wrap(built) : call;
    }

    // A call that the macros of math refuse at parsing: it type checks nowhere, so it carries a fault.
    private Built RefusedExtreme(string function)
    {
        List<Expr> args = [];
        switch (pcg.Below(7))
        {
            case 1:
                args.Add(StringLiteral(pcg.Pick(ExtensionStrings.Pool)));
                break;
            case 2:
                args.Add(new ListLiteral([], TrailingComma: false));
                break;
            case 3:
                args.Add(new MapLiteral([], TrailingComma: false));
                break;
            case 4:
                args.Add(Literal(ProfileType.IntType));
                args.Add(new ListLiteral([Literal(ProfileType.IntType)], TrailingComma: false));
                break;
            case 5:
                args.Add(new BoolLiteral(pcg.OneIn(2)));
                break;
            case 6:
                args.Add(new NullLiteral());
                break;
        }

        return new Built(new Call(function, args) { Fault = "math-refused" }, 1 + args.Sum(CountNodes), null);
    }

    // One overload of math.* that gives the target type: rounding, tests, abs, sign, sqrt, bit operations, shifts.
    private Built? MathCall(ProfileType target, int depth, int limit)
    {
        if (!MathRows.TryGetValue(target, out ProfileOverload[]? rows))
        {
            return null;
        }

        ProfileOverload row = pcg.Pick(rows);
        List<ArgBuilder> args = [];
        for (int i = 0; i < row.Parameters.Count; i++)
        {
            ProfileType parameter = row.Parameters[i];
            bool shiftCount = i == 1 && row.Function.StartsWith("math.bitShift", StringComparison.Ordinal);
            args.Add(shiftCount ? (d, b) => IntFrom(Shifts, d, b) : Arg(parameter));
        }

        return ExtCall(row.Function, false, row.Id, Of(row.Result), depth, limit, [.. args]);
    }

    // ---- lists ----

    private Built ListOperand(int depth, int budget) => Make(ProfileType.ListType, depth, budget);

    // A list that sort and sortBy order: a literal of 1 to 3 elements of a kind, or a typed variable: at most 12 elements, so that
    // the order of equal keys and of NaN is that of cel-go.
    private Built SortableList(ProfileType kind, int depth, int budget)
    {
        if (kind == ProfileType.IntType && pcg.OneIn(5))
        {
            return VariableNamed("li");
        }

        if (kind == ProfileType.StringType && pcg.OneIn(5))
        {
            return VariableNamed("ls");
        }

        return budget >= 2 && depth < ProfileTables.MaxDepth ? ElementList(kind, depth, budget).List : VariableNamed(kind == ProfileType.StringType ? "ls" : "li");
    }

    private Built? Slice(int depth, int limit)
    {
        StaticType? type = null;
        Built? call = ExtCall(
            "slice", true, "list_slice", null, depth, limit,
            (d, b) =>
            {
                Built receiver = ListOperand(d, b);
                type = receiver.Type;
                return receiver;
            },
            (d, b) => IntFrom(Offsets, d, b),
            (d, b) => IntFrom(Offsets, d, b));
        return call is { } built ? built with { Type = type } : null;
    }

    private Built? Flatten(int depth, int limit)
    {
        // flatten() takes a list of lists, which a list of another element type is not: its receiver is a dyn list or a list of dyn.
        // The form with a depth takes any list.
        bool levels = pcg.OneIn(2);
        List<ArgBuilder> args =
        [
            levels
                ? ListOperand
                : (d, b) => Operand(ProfileType.ListType, d, b, type => type is { IsDyn: true } or { Element.IsDyn: true }),
        ];
        if (levels)
        {
            args.Add((d, b) => IntFrom(Depths, d, b));
        }

        return ExtCall("flatten", true, levels ? "list_flatten_int" : "list_flatten", StaticType.ListOf(null), depth, limit, [.. args]);
    }

    // reverse and distinct: the list of the receiver.
    private Built? SameListCall(string function, string id, int depth, int limit)
    {
        StaticType? type = null;
        Built? call = ExtCall(
            function, true, id, null, depth, limit,
            (d, b) =>
            {
                Built receiver = ListOperand(d, b);
                type = receiver.Type;
                return receiver;
            });
        return call is { } built ? built with { Type = type } : null;
    }

    private Built? Sort(int depth, int limit)
    {
        ProfileOverload row = pcg.Pick(SortRows);
        StaticType? type = null;
        Built? call = ExtCall(
            "sort", true, row.Id, null, depth, limit,
            (d, b) =>
            {
                Built receiver = SortableList(row.Element!.Value, d, b);
                type = receiver.Type;
                return receiver;
            });
        return call is { } built ? built with { Type = type } : null;
    }

    private Built RangeLiteral() => new(new IntLiteral(pcg.Pick(RangeSizes), IntegerForm.DecimalDigits), 1, Of(ProfileType.IntType));

    // size(lists.range(n)): the list stays inside size(), so a large n costs the oracle no value to send.
    private Built? RangeSize(int depth, int limit)
    {
        if (limit < 3 || depth + 2 > ProfileTables.MaxDepth)
        {
            return null;
        }

        long size = pcg.OneIn(100) ? 1_000_000 + pcg.Below(2) : pcg.Pick(RangeSizes);
        Expr range = new Call("lists.range", [new IntLiteral(size, IntegerForm.DecimalDigits)]) { Overload = "lists_range" };
        return new Built(new Call(ProfileTables.Size, [range]), 3, Of(ProfileType.IntType));
    }

    // list.sortBy(v, key): the key has the kind of the elements half the time, so that it reads the variable.
    private Built? SortBy(int depth, int limit)
    {
        if (limit < 4 || macros >= ProfileTables.MaxMacroNesting || depth + 2 > ProfileTables.MaxDepth)
        {
            return null;
        }

        macros++;
        ProfileType elementKind = pcg.Pick(AllKinds);
        Built range = SortableList(elementKind, depth + 1, limit - 2);
        StaticType? element = range.Type?.Element;
        if (element is { Kind: ProfileType.TypeType })
        {
            element = null;
        }

        string variable = FreshName(ProfileTables.IterationVariables);
        scope.Add((variable, element));
        Built key = Make(pcg.OneIn(2) ? elementKind : pcg.Pick(AllKinds), depth + 1, limit - 1 - range.Nodes);
        scope.RemoveAt(scope.Count - 1);
        macros--;
        return new Built(new Comprehension(ProfileTables.SortByMacro, range.Expr, variable, [key.Expr]), 1 + range.Nodes + key.Nodes, range.Type);
    }

    private Built? Sets(int depth, int limit)
    {
        string name = pcg.Pick<string>(["contains", "intersects", "equivalent"]);
        StaticType? first = null;
        return ExtCall(
            "sets." + name, false, $"list_sets_{name}_list", Of(ProfileType.BoolType), depth, limit,
            (d, b) =>
            {
                Built left = pcg.OneIn(2) && b >= 2 && d < ProfileTables.MaxDepth
                    ? ElementList(pcg.Pick(AllKinds), d, b).List
                    : Make(ProfileType.ListType, d, b);
                first = left.Type;
                return left;
            },
            (d, b) => CompatibleOperand(ProfileType.ListType, first, d, b));
    }

    // ---- encoders ----

    private Built Base64Argument(int depth, int budget) =>
        pcg.OneIn(4) ? Make(ProfileType.StringType, depth, budget) : PoolString(ExtensionStrings.Base64Text(pcg));

    // The value of json.encode: a string that it escapes one time in four, otherwise a value of any type of the profile, among them
    // the values it refuses: NaN, an infinity, a map with a key that is not a string, a type.
    private Built JsonArgument(int depth, int budget) =>
        pcg.OneIn(4) ? PoolString(ExtensionStrings.JsonText(pcg)) : Make(Target(), depth, budget);

    // ---- network ----

    // The text argument of ip, cidr and the other functions of the set: a literal, one time in five dyn(literal), and one time in
    // seven the variable s1, which the case binds to the text.
    private Built NetworkText(int depth, int budget, Func<CsCheck.PCG, string> source)
    {
        string text = source(pcg);
        if (S1Binding is null && pcg.OneIn(7))
        {
            S1Binding = text;
            return VariableNamed("s1");
        }

        Built literal = PoolString(text);
        return budget >= 2 && depth + 1 <= ProfileTables.MaxDepth && pcg.OneIn(5) ? Wrap(literal) : literal;
    }

    // A node of the type net.IP: ip(text), or the address of a prefix. The caller leaves room for its text, at least 2 nodes.
    private Built IpNode(int depth, int budget)
    {
        if (budget >= 4 && depth + 3 <= ProfileTables.MaxDepth && pcg.OneIn(4))
        {
            Built cidr = CidrNode(depth + 1, budget - 1);
            return new Built(new Call("ip", [cidr.Expr]) { Receiver = true, Overload = "cidr_ip" }, 1 + cidr.Nodes, Of(ProfileType.IpType));
        }

        Built text = NetworkText(depth + 1, budget - 1, ExtensionStrings.IpText);
        return new Built(new Call("ip", [text.Expr]) { Overload = "string_to_ip" }, 1 + text.Nodes, Of(ProfileType.IpType));
    }

    // A node of the type net.CIDR: cidr(text), or a masked prefix.
    private Built CidrNode(int depth, int budget)
    {
        if (budget >= 4 && depth + 3 <= ProfileTables.MaxDepth && pcg.OneIn(5))
        {
            Built inner = CidrNode(depth + 1, budget - 1);
            return new Built(new Call("masked", [inner.Expr]) { Receiver = true, Overload = "cidr_masked" }, 1 + inner.Nodes, Of(ProfileType.CidrType));
        }

        Built text = NetworkText(depth + 1, budget - 1, ExtensionStrings.CidrText);
        return new Built(new Call("cidr", [text.Expr]) { Overload = "string_to_cidr" }, 1 + text.Nodes, Of(ProfileType.CidrType));
    }

    // The forms with an abstract argument need two levels, and two nodes for each such argument and one for each other.
    private static bool NetworkRoom(int depth, int limit, int abstractArguments, int otherArguments = 0) =>
        depth + 2 <= ProfileTables.MaxDepth && limit >= 1 + (2 * abstractArguments) + otherArguments;

    private Built? NetString(int depth, int limit)
    {
        if (!NetworkRoom(depth, limit, 1))
        {
            return null;
        }

        bool ip = pcg.OneIn(2);
        ArgBuilder operand = ip ? IpNode : CidrNode;
        return ExtCall(ProfileTables.StringConversion, false, ip ? "ip_to_string" : "cidr_to_string", Of(ProfileType.StringType), depth, limit, operand);
    }

    private Built? IsText(int depth, int limit)
    {
        (string function, string id) = pcg.Pick<(string, string)>([("isIP", "is_ip"), ("isCIDR", "is_cidr"), ("ip.isCanonical", "ip_is_canonical")]);
        return ExtCall(function, false, id, Of(ProfileType.BoolType), depth, limit, (d, b) => NetworkText(d, b, ExtensionStrings.AddressOrPrefix));
    }

    private Built? IpPredicate(int depth, int limit)
    {
        if (!NetworkRoom(depth, limit, 1))
        {
            return null;
        }

        string function = pcg.Pick(NetworkPredicates);
        string id = ProfileTables.ExtensionOverloads.First(overload => overload.Function == function).Id;
        return ExtCall(function, true, id, Of(ProfileType.BoolType), depth, limit, IpNode);
    }

    // containsIP and containsCIDR with an abstract argument or a string.
    private Built? ContainsCall(int depth, int limit)
    {
        bool ip = pcg.OneIn(2);
        bool text = pcg.OneIn(2);
        if (!NetworkRoom(depth, limit, text ? 1 : 2, text ? 1 : 0))
        {
            return null;
        }

        ArgBuilder argument = ip ? IpNode : CidrNode;
        if (text)
        {
            argument = (d, b) => NetworkText(d, b, ip ? ExtensionStrings.IpText : ExtensionStrings.CidrText);
        }

        // The receiver leaves one node more for an abstract argument, which needs two.
        int spare = text ? 0 : 1;
        string id = (ip ? "cidr_contains_ip_" : "cidr_contains_cidr") + (ip ? (text ? "string" : "ip") : (text ? "_string" : string.Empty));
        return ExtCall(ip ? "containsIP" : "containsCIDR", true, id, Of(ProfileType.BoolType), depth, limit, (d, b) => CidrNode(d, b - spare), argument);
    }

    private Built? NetEquality(int depth, int limit)
    {
        if (!NetworkRoom(depth, limit, 2))
        {
            return null;
        }

        ArgBuilder operand = pcg.OneIn(2) ? IpNode : CidrNode;
        string function = pcg.OneIn(2) ? ProfileTables.Equal : ProfileTables.NotEqual;
        return ExtCall(function, false, null, Of(ProfileType.BoolType), depth, limit, (d, b) => operand(d, b - 1), operand);
    }

    // type(x) == net.IP, or the other type.
    private Built? TypeEquality(int depth, int limit)
    {
        if (limit < 5 || depth + 3 > ProfileTables.MaxDepth)
        {
            return null;
        }

        bool ip = pcg.OneIn(2);
        bool matching = !pcg.OneIn(4);
        string identifier = ProfileTables.AbstractTypeIdentifiers[ip == matching ? 0 : 1];
        ArgBuilder operand = ip ? IpNode : CidrNode;
        Built? typeOf = ExtCall(ProfileTables.TypeOf, false, null, Of(ProfileType.TypeType), depth + 1, limit - 2, operand);
        if (typeOf is not { } type)
        {
            return null;
        }

        Built name = new(new Ident(identifier), 1, Of(ProfileType.TypeType));
        List<Expr> operands = pcg.OneIn(2) ? [type.Expr, name.Expr] : [name.Expr, type.Expr];
        return new Built(new Call(ProfileTables.Equal, operands), 2 + type.Nodes, Of(ProfileType.BoolType));
    }

    // ---- two-variable macros and cel.bind ----

    // The range of a two-variable macro and the types of its two variables: a list or a map of a fixed order. A range whose
    // variables have no static type, or the type type, is wrapped in dyn(), and both variables are then dyn.
    private (Built Range, StaticType? First, StaticType? Second) TwoVariableRange(int depth, int limit)
    {
        switch (pcg.Below(6))
        {
            case 0:
                {
                    ProfileVariable variable = pcg.Pick(ProfileTables.Variables.Where(candidate => candidate.Type == ProfileType.ListType).ToList());
                    return (VariableNode(variable), Of(ProfileType.IntType), variable.Declared.Element);
                }

            case 1 or 2:
                {
                    Built map = SmallMap(depth + 1, limit - 1);
                    StaticType? key = map.Type?.Key;
                    StaticType? value = map.Type?.Value;
                    return key is null || value is null || value.Kind == ProfileType.TypeType
                        ? (Wrap(map), StaticType.Dyn, StaticType.Dyn)
                        : (map, key, value);
                }

            case 3:
                return (Wrap(ListOfAny(depth + 1, limit - 1)), StaticType.Dyn, StaticType.Dyn);
            default:
                {
                    Built list = ListOfAny(depth + 1, limit - 1);
                    StaticType? element = list.Type?.Element;
                    return element is null or { Kind: ProfileType.TypeType }
                        ? (Wrap(list), StaticType.Dyn, StaticType.Dyn)
                        : (list, Of(ProfileType.IntType), element);
                }
        }
    }

    // A map literal of 0 to 1 entries with a key of the kind int, string or bool, and a value of any type: the range of a macro,
    // and the map that transformMapEntry takes.
    private Built SmallMap(int depth, int limit)
    {
        if (limit < 3 || depth >= ProfileTables.MaxDepth || pcg.OneIn(4))
        {
            return new Built(new MapLiteral([], TrailingComma()), 1, StaticType.MapOf(null, null));
        }

        ProfileType kind = pcg.Pick(MapKeyKinds);
        Built value = Make(Target(), depth + 1, limit - 2);
        return new Built(
            new MapLiteral([new MapEntry(Literal(kind), value.Expr)], TrailingComma()),
            2 + value.Nodes,
            StaticType.MapOf(Of(kind), value.Type));
    }

    // A two-variable macro. The value of the transformations has the type valueTarget when it is given.
    private Built? TwoVariableMacro(string macro, int depth, int limit, ProfileType? valueTarget)
    {
        if (limit < 4 || macros >= ProfileTables.MaxMacroNesting || depth + 2 > ProfileTables.MaxDepth)
        {
            return null;
        }

        macros++;
        (Built range, StaticType? first, StaticType? second) = TwoVariableRange(depth + 1, limit - 2);
        (string firstName, string secondName) = ProfileTables.TwoVariableNames
            .First(pair => scope.TrueForAll(local => local.Name != pair.First && local.Name != pair.Second));
        scope.Add((firstName, first));
        scope.Add((secondName, second));
        List<Expr> arguments = [];
        int nodes = 1 + range.Nodes;
        bool quantifier = ProfileTables.TwoVariableQuantifiers.Contains(macro);
        StaticType? result;
        if (quantifier)
        {
            Built predicate = Make(ProfileType.BoolType, depth + 1, limit - nodes);
            arguments.Add(predicate.Expr);
            nodes += predicate.Nodes;
            result = Of(ProfileType.BoolType);
        }
        else
        {
            if (limit - nodes >= 3 && pcg.OneIn(2))
            {
                Built filter = Make(ProfileType.BoolType, depth + 1, limit - nodes - 1);
                arguments.Add(filter.Expr);
                nodes += filter.Nodes;
            }

            Built transform = macro == "transformMapEntry"
                ? SmallMapOf(valueTarget, depth + 1, limit - nodes)
                : Make(valueTarget ?? Target(), depth + 1, limit - nodes);
            arguments.Add(transform.Expr);
            nodes += transform.Nodes;
            result = macro == "transformList" ? StaticType.ListOf(transform.Type) : StaticType.MapOf(null, null);
        }

        scope.RemoveRange(scope.Count - 2, 2);
        macros--;
        return new Built(new Comprehension(macro, range.Expr, firstName, arguments) { SecondVariable = secondName }, nodes, result);
    }

    // The map of transformMapEntry: an entry whose value has the type when it is given.
    private Built SmallMapOf(ProfileType? valueTarget, int depth, int limit)
    {
        if (valueTarget is not { } kind || limit < 3 || depth >= ProfileTables.MaxDepth)
        {
            return SmallMap(depth, limit);
        }

        Built value = Make(kind, depth + 1, limit - 2);
        return new Built(
            new MapLiteral([new MapEntry(Literal(pcg.Pick(MapKeyKinds)), value.Expr)], TrailingComma()),
            2 + value.Nodes,
            StaticType.MapOf(null, value.Type));
    }

    private Built? TwoVariableQuantifier(int depth, int limit) =>
        TwoVariableMacro(pcg.Pick(ProfileTables.TwoVariableQuantifiers), depth, limit, null);

    // transformMap or transformMapEntry: the map they make is read only by size, an index, in and ==, never the range of a macro,
    // since cel-go iterates it in no fixed order.
    private Built? MapMacro(int depth, int limit, ProfileType? valueTarget) =>
        TwoVariableMacro(pcg.OneIn(2) ? "transformMap" : "transformMapEntry", depth, limit, valueTarget);

    private Built? TwoVariableSize(int depth, int limit)
    {
        Built? map = MapMacro(depth + 1, limit - 1, null);
        return map is { } built ? new Built(new Call(ProfileTables.Size, [built.Expr]), 1 + built.Nodes, Of(ProfileType.IntType)) : null;
    }

    private Built? TwoVariableIndex(ProfileType target, int depth, int limit)
    {
        // The key is a literal in dyn(), whatever the key type of the map is.
        if (limit < 6 || depth + 3 > ProfileTables.MaxDepth)
        {
            return null;
        }

        Built? map = MapMacro(depth + 1, limit - 3, target);
        if (map is not { } built)
        {
            return null;
        }

        Built key = Wrap(new Built(Literal(pcg.Pick(MapKeyKinds)), 1, null));
        return new Built(new Call(ProfileTables.Index, [built.Expr, key.Expr]), 1 + built.Nodes + key.Nodes, Of(target));
    }

    private Built? TwoVariableIn(int depth, int limit)
    {
        if (limit < 6 || depth + 3 > ProfileTables.MaxDepth)
        {
            return null;
        }

        Built? map = MapMacro(depth + 1, limit - 3, null);
        if (map is not { } built)
        {
            return null;
        }

        Built key = Wrap(new Built(Literal(pcg.Pick(MapKeyKinds)), 1, null));
        return new Built(new Call(ProfileTables.In, [key.Expr, built.Expr]), 1 + built.Nodes + key.Nodes, Of(ProfileType.BoolType));
    }

    private Built? TwoVariableEqual(int depth, int limit)
    {
        if (limit < 4)
        {
            return null;
        }

        Built? map = MapMacro(depth + 1, limit - 2, null);
        if (map is not { } built)
        {
            return null;
        }

        Built other = CompatibleOperand(ProfileType.MapType, built.Type, depth + 1, limit - 1 - built.Nodes);
        return new Built(new Call(ProfileTables.Equal, [built.Expr, other.Expr]), 1 + built.Nodes + other.Nodes, Of(ProfileType.BoolType));
    }

    // cel.bind(name, value, result): the name hides a scalar variable of the manifest one time in four, with a value of its type. A
    // value of the static type type is in dyn(): the name is then dyn.
    private Built? Bind(ProfileType target, int depth, int limit)
    {
        if (limit < 3 || macros >= ProfileTables.MaxMacroNesting || depth + 1 > ProfileTables.MaxDepth)
        {
            return null;
        }

        macros++;
        string name;
        StaticType? type;
        Built init;
        if (pcg.OneIn(4))
        {
            string hiddenName = pcg.Pick(ProfileTables.HidableVariables);
            ProfileVariable hidden = ProfileTables.Variables.First(variable => variable.Name == hiddenName);
            name = hidden.Name;
            type = hidden.Declared;
            // The name has the static type of its value, so the value of a name that hides a dyn variable is a dyn node: an int value
            // would make the name an int in the body, where the generator takes it for dyn.
            init = Operand(hidden.Type, depth + 1, limit - 2, valueType => !hidden.Declared.IsDyn || valueType is { IsDyn: true });
        }
        else
        {
            name = FreshName(ProfileTables.BindingNames);
            init = Make(Target(), depth + 1, limit - 2);
            type = init.Type;
            if (type is { Kind: ProfileType.TypeType })
            {
                // cel-go plans a name of the static type type as a type name, as it does the variable of a macro over such a range
                // (reference to undefined type: bn1): a value of that type is built again a level deeper and a node smaller, so that
                // dyn() fits around it, or is a dyn variable where there is no room for dyn().
                init = depth + 2 <= ProfileTables.MaxDepth && limit >= 4 ? Wrap(Make(ProfileType.TypeType, depth + 2, limit - 3)) : DynVariable();
                type = StaticType.Dyn;
            }
        }

        scope.Add((name, type));
        Built body = Make(target, depth + 1, limit - 1 - init.Nodes);
        scope.RemoveAt(scope.Count - 1);
        macros--;

        // The minimization takes the body with the value in place of the name, once for each read of the name. A value that holds a
        // macro, copied by two reads, is no smaller case than the macro (Minimizer.IsSmaller counts the macros of the copies), so the
        // instance is dropped, and a divergence of the body stays the signature comprehension(...) even when it is a known one (the
        // run of seed 2211168051, case 520214): such a body reads the name once at most.
        if (HoldsMacro(init.Expr) && ReadsOf(body.Expr, name) > 1)
        {
            return null;
        }

        return new Built(new Comprehension(ProfileTables.BindMacro, init.Expr, name, [body.Expr]), 1 + init.Nodes + body.Nodes, body.Type);
    }

    /// <summary>Whether a node holds a macro: a comprehension anywhere in it.</summary>
    /// <param name="node">The node.</param>
    internal static bool HoldsMacro(Expr node) => node switch
    {
        Comprehension => true,
        Call call => call.Args.Any(HoldsMacro),
        ListLiteral list => list.Elements.Any(HoldsMacro),
        MapLiteral map => map.Entries.Any(entry => HoldsMacro(entry.Key) || HoldsMacro(entry.Value)),
        Select selection => HoldsMacro(selection.Operand),
        PresenceTest test => HoldsMacro(test.Operand),
        _ => false,
    };

    /// <summary>The number of reads of a name in a node: those that no macro on the way hides by binding the same name.</summary>
    /// <param name="node">The node.</param>
    /// <param name="name">The name.</param>
    internal static int ReadsOf(Expr node, string name) => node switch
    {
        Ident ident => ident.Name == name ? 1 : 0,
        Call call => call.Args.Sum(argument => ReadsOf(argument, name)),
        ListLiteral list => list.Elements.Sum(element => ReadsOf(element, name)),
        MapLiteral map => map.Entries.Sum(entry => ReadsOf(entry.Key, name) + ReadsOf(entry.Value, name)),
        Select selection => ReadsOf(selection.Operand, name),
        PresenceTest test => ReadsOf(test.Operand, name),
        Comprehension macro => ReadsOf(macro.Range, name)
            + (macro.Variable == name || macro.SecondVariable == name ? 0 : macro.Arguments.Sum(argument => ReadsOf(argument, name))),
        _ => 0,
    };
}
