using System;
using System.Collections.Generic;
using System.Linq;

namespace Great.Parts.Cel.Eng.Difftest.Profile;

/// <summary>
/// The forms of the set <c>optional</c> (docs/design/eng-difftest.md, "Profile of the set optional"). The oracle returns a value as
/// <c>cel.expr.Value</c>, which has no kind for an optional, so an optional node is confined: it stands only as the receiver or the
/// argument of <c>hasValue</c>, <c>value</c>, <c>orValue</c>, <c>or</c>, <c>optMap</c> and <c>optFlatMap</c>, as an operand of <c>==</c>,
/// <c>!=</c> and <c>type</c>, as an optional element or value of <c>[?e]</c> and <c>{?k: v}</c>, as an element of the literal that
/// <c>optional.unwrap</c> and <c>unwrapOpt</c> take, and as the operand of a selection, an index or a presence test that is itself so
/// consumed. The forms that give a target type — <c>.value()</c>, <c>.orValue(y)</c>, <c>.hasValue()</c> — are among the extension forms
/// of the targets; an optional node is built only inside them, by <see cref="OptionalNode"/>, which tracks the static type of the held
/// value, so that two optionals that meet, in <c>or</c>, <c>?:</c>, <c>==</c> and a literal, have assignable held types.
/// </summary>
internal sealed partial class ExprBuilder
{
    // The shapes of an optional node, each with a weight in the draw.
    private enum OptionalShape
    {
        None,
        Of,
        NonZero,
        FirstLast,
        Select,
        IndexList,
        IndexMap,
        Or,
        Conditional,
        Map,
        FlatMap,
        ChainSelect,
        ChainIndex,
    }

    // The probability that the value() of an empty optional is built: one in this.
    private const int EmptyValueOneIn = 10;

    private static Built OptionalBuilt(Expr expr, int nodes, StaticType? held) => new(expr, nodes, StaticType.OptionalOf(held));

    // optional.none(): the optional of a free type parameter, which is assignable wherever an optional is.
    private static Built OptionalNone() => OptionalBuilt(new Call("optional.none", []) { Overload = "optional_none" }, 1, null);

    private static bool IsNone(Built optional) => optional.Expr is Call { Overload: "optional_none" };

    // Whether an optional node may stand where its held type has to be assignable to a given type. optional.none() always may.
    private static bool Fits(Built optional, bool constrained, StaticType? with) =>
        !constrained || IsNone(optional) || with is { IsDyn: true } || StaticType.Compatible(optional.Type?.Element, with) == Compatibility.Yes;

    // The kind of the held value: the given one; else, when it has to be assignable to a known type, that kind half the time; else any.
    private ProfileType OptionalKind(ProfileType? kind, bool constrained, StaticType? with)
    {
        ProfileType drawn = kind ?? (constrained && with is { IsDyn: false, Kind: var known } && ProfileTables.TargetTypes.Contains(known) && pcg.OneIn(2) ? known : Target());

        // An optional of the type type is not built: its type parameter meets the variable of a macro and makes cel-go type it as type.
        return drawn == ProfileType.TypeType ? ProfileType.IntType : drawn;
    }

    // An optional node at a depth within a budget of nodes, never null: optional.none() where nothing else finds room. The kind is the
    // kind of the held value when it is given; the held type has to be assignable to the type with when constrained is true.
    private Built OptionalOperand(int depth, int budget, ProfileType? kind, bool constrained, StaticType? with) =>
        OptionalNode(depth, budget, kind, constrained, with) ?? OptionalNone();

    // The number of optional nodes being built, which makes Target draw no type of the type type.
    private int optionalScope;

    // The kind of the elements of a literal with optional positions: any kind but the type type.
    private ProfileType OptionalTarget()
    {
        ProfileType drawn = Target();
        return drawn == ProfileType.TypeType ? ProfileType.IntType : drawn;
    }

    private Built? OptionalNode(int depth, int limit, ProfileType? kind, bool constrained, StaticType? with)
    {
        optionalScope++;
        try
        {
            return BuildOptionalNode(depth, limit, kind, constrained, with);
        }
        finally
        {
            optionalScope--;
        }
    }

    private Built? BuildOptionalNode(int depth, int limit, ProfileType? kind, bool constrained, StaticType? with)
    {
        if (limit < 1)
        {
            return null;
        }

        List<(OptionalShape Shape, int Weight)> shapes = [(OptionalShape.None, 1)];
        if (limit >= 2 && depth < ProfileTables.MaxDepth)
        {
            shapes.AddRange(
            [
                (OptionalShape.Of, 6), (OptionalShape.NonZero, 2), (OptionalShape.FirstLast, 2), (OptionalShape.Select, 3),
                (OptionalShape.IndexList, 2), (OptionalShape.IndexMap, 2), (OptionalShape.Or, 2), (OptionalShape.Conditional, 1),
                (OptionalShape.Map, 2), (OptionalShape.FlatMap, 1),
            ]);
            if (kind is null)
            {
                // The chains hold the value of a map or a list, so their held kind is not the caller's to choose.
                shapes.AddRange([(OptionalShape.ChainSelect, 3), (OptionalShape.ChainIndex, 4)]);
            }
        }

        OptionalShape shape = PickShape(shapes);
        if (shape == OptionalShape.None)
        {
            return MaybeParenthesized(OptionalNone());
        }

        ProfileType resolved = OptionalKind(kind, constrained, with);
        Built? built = shape switch
        {
            OptionalShape.Of => OfShape(depth, limit, resolved, constrained, with),
            OptionalShape.NonZero => NonZeroShape(depth, limit, resolved, constrained, with),
            OptionalShape.FirstLast => FirstLastShape(depth, limit, resolved),
            OptionalShape.Select => SelectShape(depth, limit, resolved),
            OptionalShape.IndexList => IndexListShape(depth, limit, resolved),
            OptionalShape.IndexMap => IndexMapShape(depth, limit, resolved),
            OptionalShape.Or => OrShape(depth, limit, resolved, constrained, with),
            OptionalShape.Conditional => ConditionalShape(depth, limit, resolved, constrained, with),
            OptionalShape.Map => MacroShape(depth, limit, resolved, constrained, with, flat: false),
            OptionalShape.FlatMap => MacroShape(depth, limit, resolved, constrained, with, flat: true),
            OptionalShape.ChainSelect => ChainSelectShape(depth, limit),
            OptionalShape.ChainIndex => ChainIndexShape(depth, limit),
            _ => null,
        };
        // optional.of(x) fits whatever the held type has to be assignable to: x is built as an operand of that type.
        Built result = built is { } chosen && Fits(chosen, constrained, with)
            ? chosen
            : OfShape(depth, limit, resolved, constrained, with) ?? OptionalNone();
        return MaybeParenthesized(result);
    }

    private Built MaybeParenthesized(Built built) => built with { Expr = Maybe(built.Expr) };

    private OptionalShape PickShape(List<(OptionalShape Shape, int Weight)> shapes)
    {
        int draw = pcg.Below(shapes.Sum(entry => entry.Weight));
        foreach ((OptionalShape shape, int weight) in shapes)
        {
            if (draw < weight)
            {
                return shape;
            }

            draw -= weight;
        }

        return OptionalShape.None;
    }

    // ---- The shapes of an optional node ----

    // optional.of(x): x is any node of the kind, assignable to the given type when constrained.
    private Built? OfShape(int depth, int limit, ProfileType kind, bool constrained, StaticType? with)
    {
        if (limit < 2 || depth >= ProfileTables.MaxDepth)
        {
            return null;
        }

        Built element = constrained ? CompatibleOperand(kind, with, depth + 1, limit - 1) : Make(kind, depth + 1, limit - 1);
        return OptionalBuilt(new Call("optional.of", [element.Expr]) { Overload = "optional_of" }, 1 + element.Nodes, element.Type);
    }

    // optional.ofNonZeroValue(x): x is, three times in four, a zero or a non-zero value from the pool of the kind.
    private Built? NonZeroShape(int depth, int limit, ProfileType kind, bool constrained, StaticType? with)
    {
        if (limit < 2 || depth >= ProfileTables.MaxDepth)
        {
            return null;
        }

        Built? pooled = pcg.OneIn(4) ? null : PoolValue(kind, zero: pcg.OneIn(2));
        Built element = pooled is { } value && PoolFits(value, depth, limit - 1) && (!constrained || StaticType.Compatible(value.Type, with) == Compatibility.Yes)
            ? value
            : constrained ? CompatibleOperand(kind, with, depth + 1, limit - 1) : Make(kind, depth + 1, limit - 1);
        return OptionalBuilt(
            new Call("optional.ofNonZeroValue", [element.Expr]) { Overload = "optional_ofNonZeroValue" }, 1 + element.Nodes, element.Type);
    }

    // A value of the pool stands at depth + 1 and, when it has nodes below it, needs one level more.
    private static bool PoolFits(Built value, int depth, int budget) =>
        value.Nodes <= budget && depth + (value.Nodes == 1 ? 1 : 2) <= ProfileTables.MaxDepth;

    // A zero or a non-zero value of a kind that optional.ofNonZeroValue tests: null, false and true, 0 and 1, 0u and 1u, 0.0, -0.0, a
    // number of d1, which holds a NaN now and then, and 1.5, the empty string and 'a', empty bytes and b'a', [] and [0], {} and
    // {'a': 0}, duration('0s') and duration('1s'), timestamp('0001-01-01T00:00:00Z') and timestamp(0): the zero values of the page of
    // the set, and the year 1 of the zero timestamp. A type has no literal.
    private Built? PoolValue(ProfileType kind, bool zero) => kind switch
    {
        ProfileType.IntType or ProfileType.UintType or ProfileType.DoubleType or ProfileType.BoolType or ProfileType.StringType
            or ProfileType.BytesType or ProfileType.NullType => ScalarPoolValue(kind, zero),
        ProfileType.ListType or ProfileType.MapType => CollectionPoolValue(kind, zero),
        ProfileType.DurationType or ProfileType.TimestampType => TimePoolValue(kind, zero),
        _ => null,
    };

    private Built? ScalarPoolValue(ProfileType kind, bool zero)
    {
        Expr? literal = kind switch
        {
            ProfileType.IntType => new IntLiteral(zero ? 0 : 1, IntegerForm.DecimalDigits),
            ProfileType.UintType => new UintLiteral(zero ? 0UL : 1UL, IntegerForm.DecimalDigits, 'u'),
            ProfileType.DoubleType when !zero && pcg.OneIn(3) => null,
            ProfileType.DoubleType => new DoubleLiteral(zero ? (pcg.OneIn(2) ? 0.0 : -0.0) : 1.5, DoubleForm.Plain),
            ProfileType.BoolType => new BoolLiteral(!zero),
            ProfileType.StringType => StringLiteral(zero ? string.Empty : "a"),
            ProfileType.BytesType => BytesLiteral(zero ? Array.Empty<byte>() : new[] { (byte)'a' }),
            _ => new NullLiteral(),
        };

        // The variable d1 stands for a double that is a NaN now and then.
        return literal is null ? VariableNamed("d1") : new Built(literal, 1, Of(kind));
    }

    private Built CollectionPoolValue(ProfileType kind, bool zero)
    {
        if (kind == ProfileType.ListType)
        {
            return zero
                ? new Built(new ListLiteral([], TrailingComma: false), 1, StaticType.ListOf(null))
                : new Built(new ListLiteral([new IntLiteral(0, IntegerForm.DecimalDigits)], TrailingComma: false), 2, StaticType.ListOf(Of(ProfileType.IntType)));
        }

        return zero
            ? new Built(new MapLiteral([], TrailingComma: false), 1, StaticType.MapOf(null, null))
            : new Built(
                new MapLiteral([new MapEntry(StringLiteral("a"), new IntLiteral(0, IntegerForm.DecimalDigits))], TrailingComma: false),
                3,
                StaticType.MapOf(Of(ProfileType.StringType), Of(ProfileType.IntType)));
    }

    private Built TimePoolValue(ProfileType kind, bool zero) => kind == ProfileType.DurationType
        ? new Built(new Call(ProfileTables.DurationConversion, [StringLiteral(zero ? "0s" : "1s")]), 2, Of(kind))
        : new Built(
            new Call(
                ProfileTables.TimestampConversion,
                [zero ? (Expr)StringLiteral("0001-01-01T00:00:00Z") : new IntLiteral(0, IntegerForm.DecimalDigits)]),
            2,
            Of(kind));

    // l.first() and l.last() on a list literal or a list variable of the kind.
    private Built? FirstLastShape(int depth, int limit, ProfileType kind)
    {
        if (limit < 2 || depth >= ProfileTables.MaxDepth)
        {
            return null;
        }

        Built list;
        if (limit >= 3 && depth + 2 <= ProfileTables.MaxDepth && pcg.OneIn(2))
        {
            list = ElementList(kind, depth + 1, limit - 1).List;
        }
        else if (CollectionVariableFor(ProfileType.ListType, kind) is { } variable)
        {
            list = VariableNode(variable);
        }
        else
        {
            return null;
        }

        bool first = pcg.OneIn(2);
        StaticType? held = list.Type is { IsDyn: true } ? StaticType.Dyn : list.Type?.Element;
        return OptionalBuilt(
            new Call(first ? "first" : "last", [list.Expr]) { Receiver = true, Overload = first ? "list_first" : "list_last" },
            1 + list.Nodes,
            held);
    }

    // e.?f on a map literal that often has the field as a key, a map variable of strings, or a node of dyn.
    private Built? SelectShape(int depth, int limit, ProfileType kind)
    {
        if (limit < 2 || depth >= ProfileTables.MaxDepth)
        {
            return null;
        }

        string field = pcg.Pick(ProfileTables.FieldNames);
        Built operand;
        StaticType? held;
        switch (pcg.Below(4))
        {
            case 0 when StringMapVariable() is { } variable && StaticType.Fits(ElementOf(variable), kind):
                operand = VariableNode(variable);
                held = ElementOf(variable);
                break;
            case 1:
                operand = DynExpr(depth + 1, limit - 1);
                held = StaticType.Dyn;
                break;
            default:
                if (limit < 4 || depth + 2 > ProfileTables.MaxDepth)
                {
                    return null;
                }

                (operand, held) = ValueMap(kind, field, depth + 1, limit - 1);
                break;
        }

        return OptionalBuilt(new Select(operand.Expr, field) { Optional = true }, 1 + operand.Nodes, held);
    }

    // l[?i] on a list literal or a list variable of the kind: an index of -1, 0, 1, the size, the size + 1 and 100, now and then an int node.
    private Built? IndexListShape(int depth, int limit, ProfileType kind)
    {
        if (limit < 3 || depth >= ProfileTables.MaxDepth)
        {
            return null;
        }

        Built list;
        StaticType? held;
        if (limit >= 4 && depth + 2 <= ProfileTables.MaxDepth && pcg.OneIn(2))
        {
            (list, held) = ElementList(kind, depth + 1, limit - 2);
        }
        else if (CollectionVariableFor(ProfileType.ListType, kind) is { } variable)
        {
            list = VariableNode(variable);
            held = variable.Declared.Element;
        }
        else
        {
            return null;
        }

        Built index = OptionalIndexValue((list.Expr as ListLiteral)?.Elements.Count, depth + 1, limit - 1 - list.Nodes);
        return OptionalBuilt(
            new Call(ProfileTables.OptionalIndex, [list.Expr, index.Expr]) { Overload = "list_optindex_optional_int" },
            1 + list.Nodes + index.Nodes,
            held);
    }

    // m[?k] on a map literal or a map variable whose values are of the kind: the key is one of the map half the time, otherwise of any
    // key type, in dyn() when it is not of the type of the keys.
    private Built? IndexMapShape(int depth, int limit, ProfileType kind)
    {
        if (limit < 3 || depth >= ProfileTables.MaxDepth)
        {
            return null;
        }

        Built map;
        StaticType? held;
        Built key;
        if (limit >= 5 && depth + 2 <= ProfileTables.MaxDepth && pcg.OneIn(2))
        {
            (map, held) = ValueMap(kind, null, depth + 1, limit - 2);
            key = Key((MapLiteral)map.Expr, map.Type, depth + 1, limit - 1 - map.Nodes);
        }
        else if (CollectionVariableFor(ProfileType.MapType, kind) is { } variable)
        {
            map = VariableNode(variable);
            held = ElementOf(variable);
            key = Key(null, variable.Declared, depth + 1, limit - 1 - map.Nodes);
        }
        else
        {
            return null;
        }

        return OptionalBuilt(
            new Call(ProfileTables.OptionalIndex, [map.Expr, key.Expr]) { Overload = "map_optindex_optional_value" },
            1 + map.Nodes + key.Nodes,
            held);
    }

    // a.or(b): the second optional holds a type assignable to that of the first, unless the first is optional.none(), whose type is free.
    private Built? OrShape(int depth, int limit, ProfileType kind, bool constrained, StaticType? with)
    {
        if (limit < 3 || depth >= ProfileTables.MaxDepth)
        {
            return null;
        }

        Built first = OptionalOperand(depth + 1, limit - 2, kind, constrained, with);
        bool free = IsNone(first);
        Built second = OptionalOperand(depth + 1, limit - 1 - first.Nodes, kind, free ? constrained : true, free ? with : first.Type?.Element);
        return OptionalBuilt(
            new Call("or", [first.Expr, second.Expr]) { Receiver = true, Overload = "optional_or_optional" },
            1 + first.Nodes + second.Nodes,
            StaticType.Meet(first.Type?.Element, second.Type?.Element));
    }

    // c ? a : b with two optional nodes, the second of a type assignable to that of the first.
    private Built? ConditionalShape(int depth, int limit, ProfileType kind, bool constrained, StaticType? with)
    {
        if (limit < 4 || depth >= ProfileTables.MaxDepth)
        {
            return null;
        }

        Built condition = Make(ProfileType.BoolType, depth + 1, limit - 3);
        Built first = OptionalOperand(depth + 1, limit - 2 - condition.Nodes, kind, constrained, with);
        bool free = IsNone(first);
        Built second = OptionalOperand(
            depth + 1, limit - 1 - condition.Nodes - first.Nodes, kind, free ? constrained : true, free ? with : first.Type?.Element);
        return OptionalBuilt(
            new Call(ProfileTables.Conditional, [condition.Expr, first.Expr, second.Expr]),
            1 + condition.Nodes + first.Nodes + second.Nodes,
            StaticType.Meet(first.Type?.Element, second.Type?.Element));
    }

    // o.optMap(v, e) and o.optFlatMap(v, e): the receiver is an optional node whose held type is known and is not type, so that v has a
    // static type; v is the held value, and the body reads it. e is a node of the kind, and for optFlatMap an optional node.
    private Built? MacroShape(int depth, int limit, ProfileType kind, bool constrained, StaticType? with, bool flat)
    {
        if (limit < 4 || macros >= ProfileTables.MaxMacroNesting || depth + 2 > ProfileTables.MaxDepth)
        {
            return null;
        }

        macros++;
        Built receiver = KnownOptional(depth + 1, limit - 2);
        string variable = FreshName(ProfileTables.IterationVariables);
        scope.Add((variable, receiver.Type?.Element));
        int budget = limit - 1 - receiver.Nodes;
        Built body = flat
            ? OptionalOperand(depth + 1, budget, kind, constrained, with)
            : constrained ? CompatibleOperand(kind, with, depth + 1, budget) : Make(kind, depth + 1, budget);
        scope.RemoveAt(scope.Count - 1);
        macros--;
        return new Built(
            new Comprehension(flat ? ProfileTables.OptFlatMapMacro : ProfileTables.OptMapMacro, receiver.Expr, variable, [body.Expr]),
            1 + receiver.Nodes + body.Nodes,
            flat ? body.Type : StaticType.OptionalOf(body.Type));
    }

    // An optional node whose held type is known and is not type: v of optMap is bound to it. After a few tries, optional.of of an int.
    private Built KnownOptional(int depth, int limit)
    {
        for (int attempt = 0; attempt < 3; attempt++)
        {
            if (OptionalNode(depth, limit, null, false, null) is { } built && built.Type?.Element is { } held && held.Kind != ProfileType.TypeType)
            {
                return built;
            }
        }

        return OfShape(depth, limit, ProfileType.IntType, false, null) ?? OptionalNone();
    }

    // e.?a.b and e.?a.?b, e.a on an optional: the operand is an optional of a map, so the field selects in it.
    private Built? ChainSelectShape(int depth, int limit)
    {
        if (limit < 3 || depth >= ProfileTables.MaxDepth)
        {
            return null;
        }

        Built inner = OptionalOperand(depth + 1, limit - 1, ProfileType.MapType, false, null);
        string field = pcg.Pick(ProfileTables.FieldNames);
        bool optional = pcg.OneIn(2);
        StaticType? held = inner.Type?.Element is { IsDyn: true } ? StaticType.Dyn : inner.Type?.Element?.Value;
        return OptionalBuilt(new Select(inner.Expr, field) { Optional = optional }, 1 + inner.Nodes, held);
    }

    // o[i], o[?i], o[k] and o[?k] on an optional of a list or of a map. The operand may hold, by its value, another kind than its type
    // names — {'a': x1}.?a is an optional of a map by its type and holds whatever x1 holds: the library reads the held value by its
    // kind as cel-go does (docs/design/extension-optional.md, "Indexes"), so the operand is not restricted.
    private Built? ChainIndexShape(int depth, int limit)
    {
        if (limit < 4 || depth >= ProfileTables.MaxDepth)
        {
            return null;
        }

        bool list = pcg.OneIn(2);
        bool optionalIndex = pcg.OneIn(2);
        Built inner = OptionalOperand(depth + 1, limit - 2, list ? ProfileType.ListType : ProfileType.MapType, false, null);
        StaticType? container = inner.Type?.Element;
        Built index = list
            ? OptionalIndexValue(null, depth + 1, limit - 1 - inner.Nodes)
            : Key(null, container, depth + 1, limit - 1 - inner.Nodes);
        StaticType? held = container is { IsDyn: true } ? StaticType.Dyn : list ? container?.Element : container?.Value;
        string id = list
            ? (optionalIndex ? "optional_list_optindex_optional_int" : "optional_list_index_int")
            : (optionalIndex ? "optional_map_optindex_optional_value" : "optional_map_index_value");
        return OptionalBuilt(
            new Call(optionalIndex ? ProfileTables.OptionalIndex : ProfileTables.Index, [inner.Expr, index.Expr]) { Overload = id, Container = container },
            1 + inner.Nodes + index.Nodes,
            held);
    }

    // An index: -1, 0, 1, the size, the size + 1 or 100, where the size of a literal is its number of elements and otherwise 2; one time
    // in six a node of the type int, and one time in six a node of the type dyn, whose value the library reads by its kind as cel-go
    // reads the qualifier of an index: an element for an int, a uint or an integral double, none outside the list, an error for the
    // rest.
    private Built OptionalIndexValue(int? length, int depth, int budget)
    {
        switch (pcg.Below(6))
        {
            case 0:
                return Make(ProfileType.IntType, depth, budget);
            case 1:
                return OptionalDynIndex(depth, budget);
            default:
                long size = length ?? 2;
                return new Built(new IntLiteral(pcg.Pick<long>([-1, 0, 1, size, size + 1, 100]), IntegerForm.DecimalDigits), 1, Of(ProfileType.IntType));
        }
    }

    // A node of the type dyn that is an index: dyn() of a literal of a uint, a double — integral or not — or an int, or a dyn variable
    // of the manifest, whose value is a scalar, null, a list or a map.
    private Built OptionalDynIndex(int depth, int budget)
    {
        if (depth < ProfileTables.MaxDepth && budget >= 2 && !pcg.OneIn(3))
        {
            ProfileType kind = pcg.Pick<ProfileType>([ProfileType.UintType, ProfileType.DoubleType, ProfileType.IntType]);
            return Wrap(new Built(Literal(kind), 1, Of(kind)));
        }

        return DynVariable();
    }

    // A list or a map variable of the manifest whose elements or values fit the kind, or null when there is none.
    private ProfileVariable? CollectionVariableFor(ProfileType collection, ProfileType kind)
    {
        List<ProfileVariable> fitting =
        [
            .. ProfileTables.Variables.Where(variable => variable.Type == collection && StaticType.Fits(ElementOf(variable), kind)),
        ];
        return fitting.Count == 0 ? null : pcg.Pick(fitting);
    }

    // An optional that holds no value and whose held type is the kind: optional.ofNonZeroValue of a zero value, or optional.none().
    private Built EmptyOptional(ProfileType kind, int depth, int budget)
    {
        if (budget >= 2 && depth < ProfileTables.MaxDepth && PoolValue(kind, zero: true) is { } zero && PoolFits(zero, depth, budget - 1))
        {
            return OptionalBuilt(new Call("optional.ofNonZeroValue", [zero.Expr]) { Overload = "optional_ofNonZeroValue" }, 1 + zero.Nodes, zero.Type);
        }

        return OptionalNone();
    }

    // ---- The forms of a target ----

    // o.hasValue()
    private Built? OptHasValue(int depth, int limit) =>
        ExtCall("hasValue", true, "optional_hasValue", Of(ProfileType.BoolType), depth, limit, (d, b) => OptionalOperand(d, b, null, false, null));

    // o.value() of the kind of the target; one time in ten the optional is empty, and the call carries the fault that tells the
    // expression fails on both sides at evaluation.
    private Built? OptValue(ProfileType target, int depth, int limit)
    {
        if (target == ProfileType.TypeType)
        {
            return null;
        }

        bool empty = pcg.OneIn(EmptyValueOneIn);
        bool freeType = false;
        StaticType? held = null;
        Built? call = ExtCall(
            "value", true, "optional_value", null, depth, limit,
            (d, b) =>
            {
                Built optional = empty ? EmptyOptional(target, d, b) : OptionalOperand(d, b, target, false, null);
                held = optional.Type?.Element;
                freeType = IsNone(optional);
                return optional;
            });

        // optional.none().value() has a free type parameter as its type, which the checker of cel-go binds to the first overload that a
        // method on it has, so a case with one is no case of the profile.
        if (freeType || call is not { } built)
        {
            return null;
        }

        Call value = (Call)built.Expr;
        return built with { Expr = empty ? value with { Fault = "optional-empty" } : value, Type = held };
    }

    // o.orValue(y) of the kind of the target: y is a node of that kind assignable to the held type.
    private Built? OptOrValue(ProfileType target, int depth, int limit)
    {
        // A node of the type type as the argument gives the type parameter of the optional to the checker of cel-go, which then types the
        // variable of a macro in the receiver as type and fails to plan it; the profile builds no such node.
        if (target == ProfileType.TypeType)
        {
            return null;
        }

        StaticType? held = null;
        Built? call = ExtCall(
            "orValue", true, "optional_orValue_value", null, depth, limit,
            (d, b) =>
            {
                Built optional = OptionalOperand(d, b, target, false, null);
                held = optional.Type?.Element;
                return optional;
            },
            (d, b) => CompatibleOperand(target, held, d, b));
        return call is { } built ? built with { Type = held } : null;
    }

    // a == b and a != b on two optionals whose held types are assignable.
    private Built? OptEquality(int depth, int limit)
    {
        StaticType? held = null;
        bool free = false;
        string function = pcg.OneIn(2) ? ProfileTables.Equal : ProfileTables.NotEqual;
        return ExtCall(
            function, false, null, Of(ProfileType.BoolType), depth, limit,
            (d, b) =>
            {
                Built first = OptionalOperand(d, b, null, false, null);
                held = first.Type?.Element;
                free = IsNone(first);
                return first;
            },
            (d, b) => OptionalOperand(d, b, null, !free, held));
    }

    // type(o) == optional_type, and now and then another type identifier, in either order.
    private Built? OptTypeEquality(int depth, int limit)
    {
        if (limit < 4 || depth + 2 > ProfileTables.MaxDepth)
        {
            return null;
        }

        Built? typeOf = ExtCall(ProfileTables.TypeOf, false, null, Of(ProfileType.TypeType), depth + 1, limit - 2, (d, b) => OptionalOperand(d, b, null, false, null));
        if (typeOf is not { } type)
        {
            return null;
        }

        string identifier = pcg.OneIn(4) ? pcg.Pick(ProfileTables.TypeIdentifiers) : ProfileTables.OptionalTypeIdentifier;
        Built name = new(new Ident(identifier), 1, Of(ProfileType.TypeType));
        List<Expr> operands = pcg.OneIn(2) ? [type.Expr, name.Expr] : [name.Expr, type.Expr];
        return new Built(new Call(pcg.OneIn(4) ? ProfileTables.NotEqual : ProfileTables.Equal, operands), 2 + type.Nodes, Of(ProfileType.BoolType));
    }

    // type(o)
    private Built? OptTypeOf(int depth, int limit) =>
        ExtCall(ProfileTables.TypeOf, false, null, Of(ProfileType.TypeType), depth, limit, (d, b) => OptionalOperand(d, b, null, false, null));

    // has({'k': o}.k.f): the operand of the presence test is the optional that is the value of an entry of a map literal, the form of the
    // known signature of the registry. has(e.?a.f) is not built, on a variable or on any other optional node: cel-go gives an optional
    // there, none or with a value, where its checker says bool, which the oracle cannot return (CI, the runs on the push of the set
    // optional), and has(e.?a) is not built either: the parser reads e.?a as a call, not a selection.
    private Built? OptPresence(int depth, int limit)
    {
        if (limit < 5 || depth + 3 > ProfileTables.MaxDepth)
        {
            return null;
        }

        string field = pcg.Pick(ProfileTables.FieldNames);
        string key = pcg.Pick(ProfileTables.FieldNames);
        Built value = OptionalOperand(depth + 3, limit - 4, ProfileType.MapType, false, null);
        MapLiteral map = new([new MapEntry(StringLiteral(key), value.Expr)], TrailingComma());
        return new Built(new PresenceTest(new Select(map, key), field), 4 + value.Nodes, Of(ProfileType.BoolType));
    }

    // optional.unwrap([…]) and [...].unwrapOpt() on a literal of 0 to 3 optional nodes.
    private Built? OptUnwrap(int depth, int limit)
    {
        if (limit < 2 || depth >= ProfileTables.MaxDepth)
        {
            return null;
        }

        bool receiver = pcg.OneIn(2);
        int room = depth + 2 <= ProfileTables.MaxDepth ? Math.Min(ProfileTables.MaxElements, limit - 2) : 0;
        int count = pcg.Below(room + 1);
        List<Expr> elements = [];
        StaticType? join = null;
        int nodes = 2;
        for (int i = 0; i < count; i++)
        {
            Built element = OptionalOperand(depth + 2, limit - nodes - (count - 1 - i), null, false, null);
            StaticType? held = element.Type?.Element;
            join = i == 0 ? held : StaticType.Join(join, held);
            elements.Add(element.Expr);
            nodes += element.Nodes;
        }

        ListLiteral list = new(elements, TrailingComma());
        Call call = receiver
            ? new Call("unwrapOpt", [list]) { Receiver = true, Overload = "optional_unwrapOpt" }
            : new Call("optional.unwrap", [list]) { Overload = "optional_unwrap" };
        return new Built(call, nodes, StaticType.ListOf(join));
    }

    // [?a, b, ?c]: 1 to 3 elements of one kind, some of them optional nodes, at least one.
    private Built? OptList(int depth, int limit)
    {
        if (limit < 2 || depth >= ProfileTables.MaxDepth)
        {
            return null;
        }

        ProfileType kind = OptionalTarget();
        int count = 1 + pcg.Below(Math.Min(ProfileTables.MaxElements, limit - 1));
        int firstOptional = pcg.Below(count);
        List<Expr> elements = [];
        List<int> positions = [];
        StaticType? join = null;
        int nodes = 1;
        for (int i = 0; i < count; i++)
        {
            int budget = limit - nodes - (count - 1 - i);
            Built element;
            StaticType? type;
            if (i == firstOptional || pcg.OneIn(2))
            {
                element = OptionalOperand(depth + 1, budget, kind, i > 0, join);
                type = element.Type?.Element;
                positions.Add(i);
            }
            else
            {
                element = i == 0 ? Make(kind, depth + 1, budget) : CompatibleOperand(kind, join, depth + 1, budget);
                type = element.Type;
            }

            join = i == 0 ? type : StaticType.Join(join, type);
            elements.Add(element.Expr);
            nodes += element.Nodes;
        }

        return new Built(new ListLiteral(elements, TrailingComma()) { OptionalPositions = positions }, nodes, StaticType.ListOf(join));
    }

    // {?k: a, k2: b}: 1 to 3 entries with values of one kind, some of them optional nodes, at least one; the keys differ as in every map.
    private Built? OptMap(int depth, int limit)
    {
        if (limit < 3 || depth >= ProfileTables.MaxDepth)
        {
            return null;
        }

        ProfileType kind = OptionalTarget();
        int count = 1 + pcg.Below(Math.Min(ProfileTables.MaxElements, (limit - 1) / 2));
        int firstOptional = pcg.Below(count);
        HashSet<string> keys = new(StringComparer.Ordinal);
        List<MapEntry> entries = [];
        StaticType? join = null;
        int nodes = 1;
        for (int i = 0; i < count; i++)
        {
            Expr? key = DistinctKey(keys);
            if (key is null)
            {
                continue;
            }

            int budget = limit - nodes - 1 - (2 * (count - 1 - i));
            bool optional = i == firstOptional || pcg.OneIn(2);
            Built value;
            StaticType? type;
            if (optional)
            {
                value = OptionalOperand(depth + 1, budget, kind, entries.Count > 0, join);
                type = value.Type?.Element;
            }
            else
            {
                value = entries.Count == 0 ? Make(kind, depth + 1, budget) : CompatibleOperand(kind, join, depth + 1, budget);
                type = value.Type;
            }

            join = entries.Count == 0 ? type : StaticType.Join(join, type);
            entries.Add(new MapEntry(key, value.Expr) { Optional = optional });
            nodes += 1 + value.Nodes;
        }

        return new Built(new MapLiteral(entries, TrailingComma()), nodes, StaticType.MapOf(KeyType(entries), entries.Count == 0 ? null : join));
    }
}
