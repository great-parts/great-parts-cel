using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Text;
using CsCheck;

namespace Great.Parts.Cel.Eng.Difftest.Profile;

/// <summary>
/// Builds an expression of the profile for a target type (docs/design/eng-difftest.md, "Profile"), within the limits of
/// depth, nodes and nested macros, so that every expression type checks. The builder knows the static type of each node it
/// builds as a bound from below (<see cref="StaticType"/>): where two operands must have assignable types — the operands of
/// <c>==</c>, <c>!=</c>, <c>_?_:_</c> and <c>+</c> on lists, the element of <c>in</c>, the key of an index — and the known
/// types do not guarantee it, the second operand is wrapped in <c>dyn()</c>. A <c>dyn</c> operand may stand wherever a
/// function takes one; an ordering across numeric kinds always has one.
/// </summary>
internal sealed partial class ExprBuilder
{
    // A node gets parentheses that precedence does not need with a chance of one in this.
    private const int ParenthesesOneIn = 10;

    // An operand of a concrete target is a dyn variable with a chance of one in this.
    private const int DynOperandOneIn = 6;

    // An atom is an iteration variable in scope that fits the target with a chance of one in this.
    private const int IterationVariableOneIn = 3;

    // A list or map literal ends with a comma with a chance of one in this.
    private const int TrailingCommaOneIn = 4;

    // The zone argument of a receiver is the variable s1 with a chance of one in this.
    private const int ZoneVariableOneIn = 10;

    // The target types of the root, list elements and map values: every type of the profile, int and bool more often, since
    // most functions take or give them.
    private static readonly ProfileType[] WeightedTargets =
    [
        .. ProfileTables.TargetTypes,
        ProfileType.IntType,
        ProfileType.BoolType,
        ProfileType.BoolType,
    ];

    // The target types without dyn.
    private static readonly ProfileType[] ConcreteTargets = [.. ProfileTables.TargetTypes.Where(type => type != ProfileType.DynType)];

    // The concrete target types of an atom of type dyn: without time, whose atom is a call of two nodes that needs room.
    private static readonly ProfileType[] AtomTargets = [.. ConcreteTargets.Where(type => !type.IsTime())];

    // The functions whose overloads take and give types without parameters, by result type; the other functions have builders of their own.
    private static readonly Dictionary<ProfileType, ProfileOverload[][]> SimpleOverloads = ProfileTables.Overloads
        .Where(overload => overload.Set is null && !SpecialFunctions().Contains(overload.Function) && overload.Id != "add_list"
            && !overload.Parameters.Any(ProfileTypeNames.IsParameter) && !overload.Result.IsParameter())
        .GroupBy(overload => overload.Result)
        .ToDictionary(byResult => byResult.Key, byResult => byResult.GroupBy(overload => overload.Function).Select(byFunction => byFunction.ToArray()).ToArray());

    // The overloads of matches: the global form and the form from a receiver.
    private static readonly ProfileOverload[] MatchesOverloads = [.. ProfileTables.Overloads.Where(overload => overload.Function == ProfileTables.Matches)];

    // The conversions whose string argument is, half the time, a number or a bool in the text forms of the standard library page.
    private static readonly HashSet<string> TextConversions = new(StringComparer.Ordinal)
    {
        ProfileTables.IntConversion, ProfileTables.UintConversion, ProfileTables.DoubleConversion, ProfileTables.BoolConversion,
    };

    private readonly PCG pcg;

    // Whether the forms of the extension sets are among the forms of a target: a case of the extension profile has them.
    private readonly bool extensions;

    // The iteration variables in scope, the innermost last, with their static types.
    private readonly List<(string Name, StaticType? Type)> scope = [];

    // The number of macros around the node being built, through their ranges or their other arguments.
    private int macros;

    /// <summary>Creates a builder that draws every choice from a random source.</summary>
    /// <param name="pcg">The random source.</param>
    /// <param name="extensions">Whether the forms of the extension sets are among the forms of a target.</param>
    internal ExprBuilder(PCG pcg, bool extensions = false)
    {
        this.pcg = pcg;
        this.extensions = extensions;
    }

    /// <summary>
    /// The text that the variable <c>s1</c> is bound to when a call of <c>matches</c> of the expression takes it as its subject or as
    /// its pattern, so the binding is a string that the call matches or is; <see langword="null"/> when no call does.
    /// </summary>
    internal string? S1Binding { get; private set; }

    /// <summary>
    /// Whether a call of <c>format</c> of the expression takes the variable <c>lx</c> as its list of arguments, so that the bytes of
    /// the binding of <c>lx</c> must be valid UTF-8 (<see cref="ProfileValues.WithValidUtf8Bytes"/>): <c>%s</c> of other bytes is
    /// the known difference <c>string_format</c>, and cel-go gives a string that no <c>cel.expr.Value</c> can hold.
    /// </summary>
    internal bool FormatsLx { get; private set; }

    private enum Form
    {
        Conditional,
        IndexList,
        IndexMap,
        SelectMap,
        SelectDyn,
        DynWrap,
        Simple,
        Chain,
        Equality,
        In,
        Has,
        Quantifier,
        List,
        AddList,
        Transform,
        Map,
        TypeOf,
        DynIndex,
        IndexVariable,
        SelectVariable,
        Extension,
    }

    /// <summary>Builds an expression.</summary>
    /// <param name="type">The target type: a member of <see cref="ProfileTables.TargetTypes"/>.</param>
    /// <param name="depth">The depth of the node, 1 for the root.</param>
    /// <param name="limit">The largest number of nodes of the expression, at least 1.</param>
    /// <returns>The expression and its number of nodes.</returns>
    internal (Expr Expr, int Nodes) Build(ProfileType type, int depth, int limit)
    {
        Built built = Make(type, depth, limit);
        return (built.Expr, built.Nodes);
    }

    /// <summary>A target type for the root, a list element or a map value: any type of the profile, <c>dyn</c> included.</summary>
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

    private static HashSet<string> SpecialFunctions() => new(StringComparer.Ordinal)
    {
        ProfileTables.Conditional, ProfileTables.LogicalAnd, ProfileTables.LogicalOr, ProfileTables.Equal, ProfileTables.NotEqual,
        ProfileTables.Index, ProfileTables.In, ProfileTables.TypeOf, ProfileTables.Dyn,
    };

    // The static type of a key literal.
    private static ProfileType KeyKind(Expr key) => key switch
    {
        IntLiteral => ProfileType.IntType,
        UintLiteral => ProfileType.UintType,
        BoolLiteral => ProfileType.BoolType,
        _ => ProfileType.StringType,
    };

    // The type of a map literal's keys: literals have exact types, so keys of one kind give that kind and mixed keys dyn.
    private static StaticType? KeyType(List<MapEntry> entries) =>
        entries.Count == 0 ? null
        : entries.All(entry => KeyKind(entry.Key) == KeyKind(entries[0].Key)) ? StaticType.Of(KeyKind(entries[0].Key))
        : StaticType.Dyn;

    // ---- Nodes of a target type ----

    // A node of a target type: an atom near the limits and more often deeper, otherwise a compound form. A node of time has no
    // atom of one node — it is a constructor and its argument, two nodes and two levels — so where there is no room for it, a dyn
    // variable stands, which fits wherever a node of time is expected.
    private Built Make(ProfileType target, int depth, int limit)
    {
        Built built = target.IsTime() && (limit < 2 || depth >= ProfileTables.MaxDepth)
            ? DynVariable()
            : depth >= ProfileTables.MaxDepth || limit < 2 || pcg.Below(ProfileTables.MaxDepth + 1) < depth
                ? Atom(target)
                : Compound(target, depth, limit) ?? Atom(target);
        return built with { Expr = Maybe(built.Expr) };
    }

    // A value of time made in the expression, by a constructor from a string or, for a timestamp, from an int: two nodes. The
    // string is a string of time of the grammar of the profile, valid or mutated.
    private Built TimeValue(ProfileType target)
    {
        Expr argument;
        string function;
        if (target == ProfileType.DurationType)
        {
            function = ProfileTables.DurationConversion;
            argument = StringLiteral(TimeStrings.Duration(pcg));
        }
        else
        {
            function = ProfileTables.TimestampConversion;
            argument = pcg.OneIn(3)
                ? new IntLiteral(TimeStrings.TimestampInteger(pcg), pcg.Pick(Enum.GetValues<IntegerForm>()))
                : StringLiteral(TimeStrings.Timestamp(pcg));
        }

        return new Built(new Call(function, [argument]), 2, StaticType.Of(target));
    }

    // A node of one node.
    private Built Atom(ProfileType target)
    {
        if (target != ProfileType.DynType && pcg.OneIn(DynOperandOneIn))
        {
            return DynVariable();
        }

        if (pcg.OneIn(IterationVariableOneIn) && IterationVariable(target) is { } local)
        {
            return local;
        }

        switch (target)
        {
            case ProfileType.DynType:
                return pcg.OneIn(3) ? DynVariable() : Atom(pcg.Pick(AtomTargets));
            case ProfileType.TypeType:
                return new Built(new Ident(pcg.Pick(ProfileTables.TypeIdentifiers)), 1, StaticType.Of(ProfileType.TypeType));
            case ProfileType.NullType:
                return new Built(new NullLiteral(), 1, StaticType.Of(ProfileType.NullType));
            case ProfileType.TimestampType or ProfileType.DurationType:
                return TimeValue(target);
            default:
                return pcg.OneIn(2) && VariableOf(target) is { } variable ? variable : EmptyOrLiteral(target);
        }
    }

    // A literal of a scalar type, or an empty list or map literal.
    private Built EmptyOrLiteral(ProfileType target) => target switch
    {
        ProfileType.ListType => new Built(new ListLiteral([], TrailingComma()), 1, StaticType.ListOf(null)),
        ProfileType.MapType => new Built(new MapLiteral([], TrailingComma()), 1, StaticType.MapOf(null, null)),
        _ => new Built(Literal(target), 1, StaticType.Of(target)),
    };

    // A variable of the manifest of a kind — a typed one included — written in one of its spellings, or null when there is none.
    private Built? VariableOf(ProfileType kind)
    {
        List<ProfileVariable> variables = [.. ProfileTables.Variables.Where(variable => variable.Type == kind)];
        return variables.Count == 0 ? null : VariableNode(pcg.Pick(variables));
    }

    // The node of a variable: its declared name, or a name through the container, with the static type of its declaration.
    private Built VariableNode(ProfileVariable variable)
    {
        string spelling = pcg.Pick(variable.Spellings);
        return new Built(new Ident(variable.Name) { Spelling = spelling == variable.Name ? null : spelling }, 1, variable.Declared);
    }

    // The type of the element of a list variable or of the value of a map variable.
    private static StaticType ElementOf(ProfileVariable variable) =>
        (variable.Type == ProfileType.ListType ? variable.Declared.Element : variable.Declared.Value)!;

    // v[i] on a typed list variable, v[k] on a typed map variable, whose element or value fits the target.
    private Built? IndexVariable(ProfileType target, int depth, int limit)
    {
        List<ProfileVariable> fitting =
        [
            .. ProfileTables.Variables.Where(candidate => candidate.Type is ProfileType.ListType or ProfileType.MapType
                && StaticType.Fits(ElementOf(candidate), target)),
        ];
        if (limit < 3 || fitting.Count == 0)
        {
            return null;
        }

        ProfileVariable variable = pcg.Pick(fitting);
        Built container = VariableNode(variable);
        Built index = variable.Type == ProfileType.ListType
            ? pcg.OneIn(2)
                ? new Built(new IntLiteral(pcg.Below(5) - 1, IntegerForm.DecimalDigits), 1, StaticType.Of(ProfileType.IntType))
                : Make(ProfileType.IntType, depth + 1, limit - 2)
            : Key(null, variable.Declared, depth + 1, limit - 2);
        return new Built(new Call(ProfileTables.Index, [container.Expr, index.Expr]), 1 + container.Nodes + index.Nodes, ElementOf(variable));
    }

    // m.f on a map variable with string keys, whose value type fits the target.
    private Built? SelectVariable(ProfileType target, int limit)
    {
        ProfileVariable? variable = StringMapVariable();
        if (limit < 2 || variable is null || !StaticType.Fits(ElementOf(variable), target))
        {
            return null;
        }

        Built container = VariableNode(variable);
        return new Built(new Select(container.Expr, pcg.Pick(ProfileTables.FieldNames)), 1 + container.Nodes, ElementOf(variable));
    }

    private static ProfileVariable? StringMapVariable() =>
        ProfileTables.Variables.FirstOrDefault(variable => variable.Type == ProfileType.MapType && variable.Declared.Key is { Kind: ProfileType.StringType });

    private Built DynVariable() => new(new Ident(pcg.OneIn(2) ? "x1" : "x2"), 1, StaticType.Dyn);

    // An iteration variable in scope whose type fits the target; any of them for dyn.
    private Built? IterationVariable(ProfileType target)
    {
        List<(string Name, StaticType? Type)> fitting = [.. scope.Where(local => StaticType.Fits(local.Type, target))];
        if (fitting.Count == 0)
        {
            return null;
        }

        (string name, StaticType? type) = pcg.Pick(fitting);
        return new Built(new Ident(name), 1, type);
    }

    private Built? Compound(ProfileType target, int depth, int limit)
    {
        if (target == ProfileType.DynType)
        {
            return pcg.Below(8) switch
            {
                0 => DynIndex(depth, limit),
                1 => DynWrap(ProfileType.DynType, depth, limit),
                _ => Compound(pcg.Pick(ConcreteTargets), depth, limit),
            };
        }

        List<(Form Form, int Weight)> forms =
        [
            (Form.Conditional, 1), (Form.IndexList, 1), (Form.IndexMap, 1), (Form.SelectMap, 1), (Form.SelectDyn, 1), (Form.DynWrap, 1),
            (Form.IndexVariable, 1), (Form.SelectVariable, 1),
        ];
        forms.AddRange(target switch
        {
            ProfileType.BoolType =>
            [
                (Form.Simple, 4), (Form.Chain, 3), (Form.Equality, 3), (Form.In, 2), (Form.Has, 2), (Form.Quantifier, 2),
            ],
            ProfileType.ListType => [(Form.List, 4), (Form.AddList, 2), (Form.Transform, 2)],
            ProfileType.MapType => [(Form.Map, 5)],
            ProfileType.TypeType => [(Form.TypeOf, 4)],
            ProfileType.NullType => [],
            _ => [(Form.Simple, 6)],
        });
        if (extensions && ExtensionForms.ContainsKey(target))
        {
            forms.Add((Form.Extension, ExtensionWeight));
        }

        int total = forms.Sum(form => form.Weight);
        int draw = pcg.Below(total);
        Form chosen = forms[0].Form;
        foreach ((Form form, int weight) in forms)
        {
            if (draw < weight)
            {
                chosen = form;
                break;
            }

            draw -= weight;
        }

        return chosen switch
        {
            Form.Conditional => Conditional(target, depth, limit),
            Form.IndexList => IndexList(target, depth, limit),
            Form.IndexMap => IndexMap(target, depth, limit),
            Form.SelectMap => SelectMap(target, depth, limit),
            Form.SelectDyn => SelectDyn(depth, limit),
            Form.DynWrap => DynWrap(target, depth, limit),
            Form.IndexVariable => IndexVariable(target, depth, limit),
            Form.SelectVariable => SelectVariable(target, limit),
            Form.Simple => Simple(target, depth, limit),
            Form.Chain => Chain(depth, limit),
            Form.Equality => Equality(depth, limit),
            Form.In => In(depth, limit),
            Form.Has => Has(depth, limit),
            Form.Quantifier => Quantifier(depth, limit),
            Form.List => ListOfAny(depth, limit),
            Form.AddList => AddList(depth, limit),
            Form.Transform => Transform(depth, limit),
            Form.Map => MapOfAny(depth, limit, ProfileTables.MaxElements),
            Form.TypeOf => TypeOf(depth, limit),
            Form.Extension => Extension(target, depth, limit),
            _ => null,
        };
    }

    // ---- Operands ----

    // An operand at a depth whose type the caller accepts; one that is not accepted is wrapped in dyn(), so it is built a level
    // deeper and one node smaller than the operand may be. Near the limits it is an atom, or a dyn variable.
    private Built Operand(ProfileType target, int depth, int limit, Func<StaticType?, bool> accept)
    {
        if (depth >= ProfileTables.MaxDepth || limit < 2)
        {
            Built atom = target.IsTime() ? DynVariable() : Atom(target);
            return accept(atom.Type) ? atom : DynVariable();
        }

        Built inner = Make(target, depth + 1, limit - 1);
        return accept(inner.Type) ? inner : Wrap(inner);
    }

    private static Built Wrap(Built inner) => new(new Call(ProfileTables.Dyn, [inner.Expr]), inner.Nodes + 1, StaticType.Dyn);

    // An operand whose type is assignable to a given type under every type the checkers may give.
    private Built CompatibleOperand(ProfileType target, StaticType? with, int depth, int limit) =>
        Operand(target, depth, limit, type => StaticType.Compatible(type, with) == Compatibility.Yes);

    // A node of the static type dyn: a dyn variable, a dyn iteration variable, dyn() of any node, or a selection on such a node.
    private Built DynExpr(int depth, int limit)
    {
        int choice = depth >= ProfileTables.MaxDepth || limit < 2 ? pcg.Below(2) : pcg.Below(5);
        switch (choice)
        {
            case 1 when scope.Exists(local => local.Type is { IsDyn: true }):
                (string name, _) = pcg.Pick(scope.Where(local => local.Type is { IsDyn: true }).ToList());
                return new Built(new Ident(name), 1, StaticType.Dyn);
            case 2 or 3:
                return Wrap(Make(Target(), depth + 1, limit - 1));
            case 4:
                Built operand = DynExpr(depth + 1, limit - 1);
                return new Built(new Select(operand.Expr, pcg.Pick(ProfileTables.FieldNames)), operand.Nodes + 1, StaticType.Dyn);
            default:
                return DynVariable();
        }
    }

    // ---- Forms of any target ----

    private Built? Conditional(ProfileType target, int depth, int limit)
    {
        if (limit < 4)
        {
            return null;
        }

        Built condition = Make(ProfileType.BoolType, depth + 1, limit - 3);
        Built first = Make(target, depth + 1, limit - 2 - condition.Nodes);
        Built second = CompatibleOperand(target, first.Type, depth + 1, limit - 1 - condition.Nodes - first.Nodes);
        return new Built(
            new Call(ProfileTables.Conditional, [condition.Expr, first.Expr, second.Expr]),
            1 + condition.Nodes + first.Nodes + second.Nodes,
            StaticType.Meet(first.Type, second.Type));
    }

    // e[i] on a list literal whose elements have the target type.
    private Built? IndexList(ProfileType target, int depth, int limit)
    {
        if (limit < 4 || depth + 2 > ProfileTables.MaxDepth)
        {
            return null;
        }

        (Built list, StaticType? element) = ElementList(target, depth + 1, limit - 2);
        Built index = pcg.OneIn(2)
            ? new Built(new IntLiteral(pcg.Below(5) - 1, IntegerForm.DecimalDigits), 1, StaticType.Of(ProfileType.IntType))
            : Make(ProfileType.IntType, depth + 1, limit - 1 - list.Nodes);
        return new Built(new Call(ProfileTables.Index, [list.Expr, index.Expr]), 1 + list.Nodes + index.Nodes, element);
    }

    // e[k] on a map literal whose values have the target type.
    private Built? IndexMap(ProfileType target, int depth, int limit)
    {
        if (limit < 5 || depth + 2 > ProfileTables.MaxDepth)
        {
            return null;
        }

        (Built map, StaticType? value) = ValueMap(target, null, depth + 1, limit - 2);
        Built key = Key((MapLiteral)map.Expr, map.Type, depth + 1, limit - 1 - map.Nodes);
        return new Built(new Call(ProfileTables.Index, [map.Expr, key.Expr]), 1 + map.Nodes + key.Nodes, value);
    }

    // m.f on a map literal whose values have the target type; one key is often the field.
    private Built? SelectMap(ProfileType target, int depth, int limit)
    {
        if (limit < 4 || depth + 2 > ProfileTables.MaxDepth)
        {
            return null;
        }

        string field = pcg.Pick(ProfileTables.FieldNames);
        (Built map, StaticType? value) = ValueMap(target, field, depth + 1, limit - 1);
        return new Built(new Select(map.Expr, field), 1 + map.Nodes, value);
    }

    private Built? SelectDyn(int depth, int limit)
    {
        Built operand = DynExpr(depth + 1, limit - 1);
        return new Built(new Select(operand.Expr, pcg.Pick(ProfileTables.FieldNames)), 1 + operand.Nodes, StaticType.Dyn);
    }

    private Built? DynWrap(ProfileType target, int depth, int limit) => Wrap(Make(target, depth + 1, limit - 1));

    // A list literal of 1–3 elements of the target type, each assignable to the join of those before it.
    private (Built List, StaticType? Element) ElementList(ProfileType target, int depth, int limit)
    {
        int count = 1 + pcg.Below(Math.Min(ProfileTables.MaxElements, limit - 1));
        Built list = ListLiteralOf(count, limit, (i, join, budget) =>
            i == 0 ? Make(target, depth + 1, budget) : CompatibleOperand(target, join, depth + 1, budget));
        return (list, list.Type?.Element);
    }

    // A map literal of 1–3 entries whose values have the target type, each assignable to the join of those before it; with a
    // field, one key is that field two times in three.
    private (Built Map, StaticType? Value) ValueMap(ProfileType target, string? field, int depth, int limit)
    {
        int count = 1 + pcg.Below(Math.Min(ProfileTables.MaxElements, (limit - 1) / 2));
        int fieldAt = field is not null && pcg.Below(3) < 2 ? pcg.Below(count) : -1;
        Built map = MapLiteralOf(count, fieldAt, field, limit, (i, join, budget) =>
            i == 0 ? Make(target, depth + 1, budget) : CompatibleOperand(target, join, depth + 1, budget));
        return (map, map.Type?.Value);
    }

    // A list literal of count elements, each built by element from its index, the join of the elements before it and its
    // budget of nodes; the type is the list of the join.
    private Built ListLiteralOf(int count, int limit, Func<int, StaticType?, int, Built> element)
    {
        List<Expr> elements = [];
        StaticType? join = null;
        int nodes = 1;
        for (int i = 0; i < count; i++)
        {
            Built built = element(i, join, limit - nodes - (count - 1 - i));
            join = i == 0 ? built.Type : StaticType.Join(join, built.Type);
            elements.Add(built.Expr);
            nodes += built.Nodes;
        }

        return new Built(new ListLiteral(elements, TrailingComma()), nodes, StaticType.ListOf(join));
    }

    // A map literal of at most count entries, the key of entry fieldAt the field. Map keys are literals of int, uint, bool and
    // string, pairwise unequal by CEL equality; a key equal to an earlier one is drawn again, and after a few draws the entry
    // is left out. Each value is built by value from the number of entries before it, their join and its budget of nodes.
    private Built MapLiteralOf(int count, int fieldAt, string? field, int limit, Func<int, StaticType?, int, Built> value)
    {
        List<MapEntry> entries = [];
        HashSet<string> keys = new(StringComparer.Ordinal);
        StaticType? join = null;
        int nodes = 1;
        for (int i = 0; i < count; i++)
        {
            Expr? key = i == fieldAt ? FieldKey(field!, keys) : DistinctKey(keys);
            if (key is null)
            {
                continue;
            }

            Built built = value(entries.Count, join, limit - nodes - 1 - (2 * (count - 1 - i)));
            join = entries.Count == 0 ? built.Type : StaticType.Join(join, built.Type);
            entries.Add(new MapEntry(key, built.Expr));
            nodes += 1 + built.Nodes;
        }

        return new Built(new MapLiteral(entries, TrailingComma()), nodes, StaticType.MapOf(KeyType(entries), entries.Count == 0 ? null : join));
    }

    // The key of an index or of in on a map: half the time an equal copy of a key of the map literal, otherwise a key of any
    // key type, wrapped in dyn() when its type is not that of the keys.
    private Built Key(MapLiteral? map, StaticType? mapType, int depth, int limit)
    {
        if (map is { Entries.Count: > 0 } && pcg.OneIn(2))
        {
            Expr copy = pcg.Pick(map.Entries).Key with { Parenthesized = false };
            if (StaticType.Compatible(StaticType.Of(KeyKind(copy)), mapType?.Key) == Compatibility.Yes || mapType is { IsDyn: true })
            {
                return new Built(copy, 1, StaticType.Of(KeyKind(copy)));
            }
        }

        StaticType? keyType = mapType is { IsDyn: true } ? StaticType.Dyn : mapType?.Key;
        ProfileType target = keyType is { IsDyn: false } known && ProfileTables.KeyTypes.Contains(known.Kind) && pcg.OneIn(2)
            ? known.Kind
            : pcg.Pick(ProfileTables.KeyTypes);
        return CompatibleOperand(target, keyType, depth, limit);
    }

    // ---- Forms of one target ----

    // A call of an overload whose parameters and result have no type parameters.
    private Built? Simple(ProfileType target, int depth, int limit)
    {
        if (!SimpleOverloads.TryGetValue(target, out ProfileOverload[][]? functions))
        {
            return null;
        }

        ProfileOverload overload = pcg.Pick(pcg.Pick(functions));
        int count = overload.Parameters.Count;
        if (limit < 1 + count)
        {
            return null;
        }

        if (overload.Function == ProfileTables.Matches)
        {
            return MatchesCall(overload, null, depth, limit);
        }

        int dynAt = overload.DynOnly ? pcg.Below(count) : -1;
        List<Expr> args = [];
        int nodes = 1;
        for (int i = 0; i < count; i++)
        {
            ProfileType parameter = overload.Parameters[i];
            int budget = limit - nodes - (count - 1 - i);
            Built arg = i == dynAt
                ? Operand(parameter, depth + 1, budget, type => type is { IsDyn: true })
                : ConstantArgument(overload, i) ?? Make(parameter, depth + 1, budget);
            args.Add(arg.Expr);
            nodes += arg.Nodes;
        }

        return new Built(new Call(overload.Function, args) { Receiver = overload.Receiver }, nodes, StaticType.Of(target));
    }

    // The argument of one node that some overloads take from a table instead of a generated expression: a number or a bool in the
    // text forms of the conversions, half the time; a string of time for a constructor of time, half the time, and an int near the
    // limits of the range for timestamp(int); the zone argument of a receiver of a timestamp — a literal, or, one case in ten, the
    // variable s1, which a binding also gives the name of a zone. Null for any other argument.
    private Built? ConstantArgument(ProfileOverload overload, int index)
    {
        ProfileType parameter = overload.Parameters[index];
        if (parameter == ProfileType.StringType)
        {
            string? text = null;
            if (TextConversions.Contains(overload.Function) && pcg.OneIn(2))
            {
                text = ProfileValues.NumberText(pcg, overload.Result);
            }
            else if (overload.Function == ProfileTables.TimestampConversion && pcg.OneIn(2))
            {
                text = TimeStrings.Timestamp(pcg);
            }
            else if (overload.Function == ProfileTables.DurationConversion && pcg.OneIn(2))
            {
                text = TimeStrings.Duration(pcg);
            }
            else if (index == 1 && overload.Parameters[0] == ProfileType.TimestampType)
            {
                return pcg.OneIn(ZoneVariableOneIn)
                    ? VariableNode(ProfileTables.Variables.First(variable => variable.Name == ProfileTables.ZoneVariable))
                    : new Built(StringLiteral(TimeStrings.Zone(pcg)), 1, StaticType.Of(ProfileType.StringType));
            }

            return text is null ? null : new Built(StringLiteral(text), 1, StaticType.Of(ProfileType.StringType));
        }

        return parameter == ProfileType.IntType && overload.Function == ProfileTables.TimestampConversion && pcg.OneIn(2)
            ? new Built(new IntLiteral(TimeStrings.TimestampInteger(pcg), pcg.Pick(Enum.GetValues<IntegerForm>())), 1, StaticType.Of(ProfileType.IntType))
            : null;
    }

    // A chain of && or || with 2–4 operands, nested to the left as the parser reads it.
    private Built? Chain(int depth, int limit)
    {
        int longest = Math.Min(Math.Min(ProfileTables.MaxChain, ProfileTables.MaxDepth - depth), (limit + 1) / 2);
        if (longest < 2)
        {
            return null;
        }

        string function = pcg.OneIn(2) ? ProfileTables.LogicalAnd : ProfileTables.LogicalOr;
        int operands = 2 + pcg.Below(longest - 1);
        int nodes = operands - 1;
        Built first = Make(ProfileType.BoolType, depth + operands - 1, limit - nodes - (operands - 1));
        Expr chain = first.Expr;
        nodes += first.Nodes;
        for (int i = 1; i < operands; i++)
        {
            Built next = Make(ProfileType.BoolType, depth + operands - i, limit - nodes - (operands - 1 - i));
            nodes += next.Nodes;
            chain = new Call(function, [chain, next.Expr]);
            if (i < operands - 1)
            {
                chain = Maybe(chain);
            }
        }

        return new Built(chain, nodes, StaticType.Of(ProfileType.BoolType));
    }

    // == or != on two operands of one target type, the second assignable to the first.
    private Built? Equality(int depth, int limit)
    {
        if (limit < 3)
        {
            return null;
        }

        ProfileType target = Target();
        Built left = Make(target, depth + 1, limit - 2);
        Built right = CompatibleOperand(target, left.Type, depth + 1, limit - 1 - left.Nodes);
        string function = pcg.OneIn(2) ? ProfileTables.Equal : ProfileTables.NotEqual;
        return new Built(new Call(function, [left.Expr, right.Expr]), 1 + left.Nodes + right.Nodes, StaticType.Of(ProfileType.BoolType));
    }

    // e in a list or a map; the element is assignable to the elements of the list or to the keys of the map.
    private Built? In(int depth, int limit)
    {
        if (limit < 3)
        {
            return null;
        }

        Built container;
        Built element;
        if (pcg.OneIn(2))
        {
            container = Make(ProfileType.ListType, depth + 1, limit - 2);
            StaticType? with = container.Type is { IsDyn: true } ? StaticType.Dyn : container.Type?.Element;
            ProfileType target = with is { IsDyn: false } known && pcg.OneIn(2) ? known.Kind : Target();
            element = CompatibleOperand(target, with, depth + 1, limit - 1 - container.Nodes);
        }
        else
        {
            container = Make(ProfileType.MapType, depth + 1, limit - 2);
            element = Key(container.Expr as MapLiteral, container.Type, depth + 1, limit - 1 - container.Nodes);
        }

        return new Built(new Call(ProfileTables.In, [element.Expr, container.Expr]), 1 + container.Nodes + element.Nodes, StaticType.Of(ProfileType.BoolType));
    }

    // has(e.f) on a map literal that often has the field as a key, on any node of the type map, on a map variable with string
    // keys, or on a node of the type dyn.
    private Built? Has(int depth, int limit)
    {
        string field = pcg.Pick(ProfileTables.FieldNames);
        Built operand = pcg.Below(4) switch
        {
            0 when limit >= 4 && depth + 2 <= ProfileTables.MaxDepth => ValueMap(Target(), field, depth + 1, limit - 1).Map,
            1 => Make(ProfileType.MapType, depth + 1, limit - 1),
            2 when StringMapVariable() is { } variable => VariableNode(variable),
            _ => DynExpr(depth + 1, limit - 1),
        };
        return new Built(new PresenceTest(operand.Expr, field), 1 + operand.Nodes, StaticType.Of(ProfileType.BoolType));
    }

    // all, exists or exists_one.
    private Built? Quantifier(int depth, int limit)
    {
        // The range is built a level deeper and two nodes smaller, so that dyn() always fits around it.
        if (limit < 4 || depth + 1 >= ProfileTables.MaxDepth || macros >= ProfileTables.MaxMacroNesting)
        {
            return null;
        }

        string macro = pcg.Pick<string>([ProfileTables.All, ProfileTables.Exists, ProfileTables.ExistsOne]);
        macros++;
        (Built range, StaticType? variableType) = Range(depth + 1, limit - 2);
        string variable = FreshName(ProfileTables.IterationVariables);
        scope.Add((variable, variableType));
        Built predicate = Predicate(variable, variableType, depth + 1, limit - 1 - range.Nodes);
        scope.RemoveAt(scope.Count - 1);
        macros--;
        return new Built(new Comprehension(macro, range.Expr, variable, [predicate.Expr]), 1 + range.Nodes + predicate.Nodes, StaticType.Of(ProfileType.BoolType));
    }

    // map with 2 or 3 arguments, or filter.
    private Built? Transform(int depth, int limit)
    {
        if (limit < 4 || depth + 1 >= ProfileTables.MaxDepth || macros >= ProfileTables.MaxMacroNesting)
        {
            return null;
        }

        string macro = pcg.OneIn(2) ? ProfileTables.MapMacro : ProfileTables.Filter;
        bool predicateAndTransform = macro == ProfileTables.MapMacro && limit >= 5 && pcg.OneIn(2);
        macros++;
        (Built range, StaticType? variableType) = TransformRange(depth + 1, limit - (predicateAndTransform ? 3 : 2));
        string variable = FreshName(ProfileTables.IterationVariables);
        scope.Add((variable, variableType));
        List<Expr> arguments = [];
        int nodes = 1 + range.Nodes;
        StaticType? element = variableType;
        if (macro == ProfileTables.Filter || predicateAndTransform)
        {
            Built predicate = Predicate(variable, variableType, depth + 1, limit - nodes - (predicateAndTransform ? 1 : 0));
            arguments.Add(predicate.Expr);
            nodes += predicate.Nodes;
        }

        if (macro == ProfileTables.MapMacro)
        {
            Built transform = Make(Target(), depth + 1, limit - nodes);
            arguments.Add(transform.Expr);
            nodes += transform.Nodes;
            element = transform.Type;
        }

        scope.RemoveAt(scope.Count - 1);
        macros--;
        return new Built(new Comprehension(macro, range.Expr, variable, arguments), nodes, StaticType.ListOf(element));
    }

    // The range of map and filter, whose result lists the elements in the order of the range: a list literal, a list literal
    // in dyn(), a typed list variable, or a map literal of at most one entry. cel-go iterates a map in no fixed order, so a map of more entries, and a
    // dyn variable, which may be bound to one, would give results that differ from run to run. The range is built a level deeper
    // and a node smaller, as an operand is, so that dyn() always fits around it (the caller checks the room). A range whose
    // iteration variable has no static type — an empty literal, whose element is a free type variable that the body may bind to
    // the type type — or the type type is wrapped in dyn(): the variable is then dyn.
    private (Built Range, StaticType? Variable) TransformRange(int depth, int limit)
    {
        switch (pcg.Below(7))
        {
            case 0:
                return (Wrap(ListOfAny(depth + 1, limit - 1)), StaticType.Dyn);
            case 1:
                Built map = MapOfAny(depth + 1, limit - 1, most: 1);
                return map.Type?.Key is null or { Kind: ProfileType.TypeType } ? (Wrap(map), StaticType.Dyn) : (map, map.Type.Key);
            case 2 when pcg.OneIn(2) && VariableOf(ProfileType.ListType) is { } listVariable:
                // A list variable has a fixed order too: li.map(v, …), lx.filter(v, …); a map variable has none.
                return (listVariable, listVariable.Type!.Element);
            case 6 when limit >= 3 && depth + 1 < ProfileTables.MaxDepth:
                // A list of strings: the iteration variable is a string, the subject of matches.
                Built strings = ElementList(ProfileType.StringType, depth + 1, limit - 1).List;
                return (strings, strings.Type?.Element);
            default:
                Built list = ListOfAny(depth + 1, limit - 1);
                return list.Type?.Element is null or { Kind: ProfileType.TypeType }
                    ? (Wrap(list), StaticType.Dyn)
                    : (list, list.Type.Element);
        }
    }

    // The range of a macro — a list, a map or a node of the type dyn — and the type of its iteration variable: the element of a
    // list, the key of a map, dyn for dyn. A range whose iteration variable has no static type (an empty literal, or an iteration
    // variable of an outer macro of that kind) or the static type type is wrapped in dyn(): the variable is then dyn. The range is
    // built a level deeper and a node smaller, so that dyn() always fits around it (the caller checks the room).
    private (Built Range, StaticType? Variable) Range(int depth, int limit)
    {
        Built range = pcg.Below(5) switch
        {
            0 or 1 => Make(ProfileType.ListType, depth + 1, limit - 1),
            2 => Make(ProfileType.MapType, depth + 1, limit - 1),
            4 when limit >= 3 && depth + 1 < ProfileTables.MaxDepth => ElementList(ProfileType.StringType, depth + 1, limit - 1).List,
            _ => DynExpr(depth, limit),
        };
        StaticType? variable = range.Type switch
        {
            { IsDyn: true } => StaticType.Dyn,
            { Kind: ProfileType.ListType } list => list.Element,
            { Kind: ProfileType.MapType } map => map.Key,
            _ => null,
        };
        return variable is null or { Kind: ProfileType.TypeType } ? (Wrap(range), StaticType.Dyn) : (range, variable);
    }

    // A list literal of 0–3 elements of any types.
    private Built ListOfAny(int depth, int limit)
    {
        int count = depth < ProfileTables.MaxDepth ? pcg.Below(Math.Min(ProfileTables.MaxElements, limit - 1) + 1) : 0;
        return ListLiteralOf(count, limit, (_, _, budget) => Make(Target(), depth + 1, budget));
    }

    // + on two lists, the second assignable to the first.
    private Built? AddList(int depth, int limit)
    {
        if (limit < 3)
        {
            return null;
        }

        Built left = Make(ProfileType.ListType, depth + 1, limit - 2);
        Built right = CompatibleOperand(ProfileType.ListType, left.Type, depth + 1, limit - 1 - left.Nodes);
        StaticType? type = StaticType.Meet(left.Type, right.Type);
        return new Built(
            new Call(ProfileTables.Add, [left.Expr, right.Expr]),
            1 + left.Nodes + right.Nodes,
            type is { IsDyn: true } ? StaticType.ListOf(null) : type);
    }

    // A map literal of 0 to most entries with values of any types.
    private Built MapOfAny(int depth, int limit, int most)
    {
        int count = depth < ProfileTables.MaxDepth ? pcg.Below(Math.Min(most, (limit - 1) / 2) + 1) : 0;
        return MapLiteralOf(count, -1, null, limit, (_, _, budget) => Make(Target(), depth + 1, budget));
    }

    private Built? TypeOf(int depth, int limit)
    {
        Built operand = Make(Target(), depth + 1, limit - 1);
        return new Built(new Call(ProfileTables.TypeOf, [operand.Expr]), 1 + operand.Nodes, StaticType.Of(ProfileType.TypeType));
    }

    // e[k] on a node of the type dyn: both index overloads are candidates, so the type is not known.
    private Built? DynIndex(int depth, int limit)
    {
        if (limit < 3)
        {
            return null;
        }

        Built container = DynExpr(depth + 1, limit - 2);
        Built key = pcg.OneIn(2)
            ? new Built(new IntLiteral(pcg.Below(4), IntegerForm.DecimalDigits), 1, StaticType.Of(ProfileType.IntType))
            : Make(pcg.Pick(ProfileTables.KeyTypes), depth + 1, limit - 1 - container.Nodes);
        return new Built(new Call(ProfileTables.Index, [container.Expr, key.Expr]), 1 + container.Nodes + key.Nodes, null);
    }

    // ---- matches ----

    // The predicate of a macro: a call of matches whose subject is the iteration variable when the variable is a string, one time in
    // three; otherwise a node of the type bool. The node is at depth.
    private Built Predicate(string variable, StaticType? variableType, int depth, int limit)
    {
        if (variableType is { Kind: ProfileType.StringType } && limit >= 3 && pcg.OneIn(3))
        {
            Built subject = new(new Ident(variable), 1, variableType);
            if (MatchesCall(pcg.Pick(MatchesOverloads), subject, depth, limit) is { } call)
            {
                return call with { Expr = Maybe(call.Expr) };
            }
        }

        return Make(ProfileType.BoolType, depth, limit);
    }

    // A call of matches at a depth, global or from a receiver, with its subject — the given one or any string expression — and a
    // pattern of one of the forms of the profile; null when there is no room for the three nodes.
    private Built? MatchesCall(ProfileOverload overload, Built? given, int depth, int limit)
    {
        int subjectNodes = given?.Nodes ?? 1;
        if (limit < subjectNodes + 2)
        {
            return null;
        }

        (Built pattern, GeneratedPattern generated) = MatchesPattern(depth, limit - 1 - subjectNodes);
        Built subject = given ?? MatchesSubject(depth, limit - 1 - pattern.Nodes);
        return new Built(
            new Call(ProfileTables.Matches, [subject.Expr, pattern.Expr]) { Receiver = overload.Receiver, Pattern = generated },
            1 + subject.Nodes + pattern.Nodes,
            StaticType.Of(ProfileType.BoolType));
    }

    // The pattern: a string literal, 60 times in 100, otherwise one of the forms that are not a literal: dyn('…'), a concatenation
    // of two literals cut between two code points, string('…'), a conditional on true, the variable s1 bound to the text. The forms
    // with literals below the call need two levels and 2 to 4 nodes of room, else the pattern is a literal.
    private (Built Node, GeneratedPattern Pattern) MatchesPattern(int depth, int budget)
    {
        GeneratedPattern generated = PatternGenerator.Generate(pcg);
        Built literal = new(PatternLiteral(generated.Text), 1, StaticType.Of(ProfileType.StringType));
        if (pcg.Below(100) < 60)
        {
            return (literal, generated);
        }

        switch (pcg.Below(5))
        {
            case 0 when S1Binding is null:
                S1Binding = generated.Text;
                return (VariableNode(ProfileTables.Variables.Single(variable => variable.Name == "s1")), generated);
            case 1 when budget >= 2 && depth + 2 <= ProfileTables.MaxDepth:
                return (Wrap(literal), generated);
            case 2 when budget >= 3 && depth + 2 <= ProfileTables.MaxDepth:
                return (Concatenated(generated.Text), generated);
            case 3 when budget >= 2 && depth + 2 <= ProfileTables.MaxDepth:
                return (new Built(new Call(ProfileTables.StringConversion, [literal.Expr]), 2, StaticType.Of(ProfileType.StringType)), generated);
            case 4 when budget >= 4 && depth + 2 <= ProfileTables.MaxDepth:
                // The branch that is not taken may be refused: it is never read.
                Built other = new(PatternLiteral(PatternGenerator.Generate(pcg).Text), 1, StaticType.Of(ProfileType.StringType));
                return (
                    new Built(
                        new Call(ProfileTables.Conditional, [new BoolLiteral(true), literal.Expr, other.Expr]),
                        4,
                        StaticType.Of(ProfileType.StringType)),
                    generated);
            default:
                return (literal, generated);
        }
    }

    // text1 + text2 with the text cut between two code points.
    private Built Concatenated(string text)
    {
        List<int> cuts = [0];
        foreach (Rune rune in text.EnumerateRunes())
        {
            cuts.Add(cuts[^1] + rune.Utf16SequenceLength);
        }

        int cut = pcg.Pick(cuts);
        Expr first = StringLiteral(text[..cut]);
        Expr second = StringLiteral(text[cut..]);
        return new Built(new Call(ProfileTables.Add, [first, second]), 3, StaticType.Of(ProfileType.StringType));
    }

    // The subject: a string literal of the alphabet of subjects, a string variable — s1 when no pattern is bound to it, bound to a
    // subject — or any expression of the type string, the iteration variable of a macro included.
    private Built MatchesSubject(int depth, int budget)
    {
        int choice = pcg.Below(10);
        if (choice < 3)
        {
            return new Built(StringLiteral(PatternGenerator.Subject(pcg)), 1, StaticType.Of(ProfileType.StringType));
        }

        if (choice < 5)
        {
            if (S1Binding is null && pcg.OneIn(2))
            {
                S1Binding = PatternGenerator.Subject(pcg);
                return VariableNode(ProfileTables.Variables.Single(variable => variable.Name == "s1"));
            }

            return VariableOf(ProfileType.StringType) ?? Atom(ProfileType.StringType);
        }

        return Make(ProfileType.StringType, depth + 1, budget);
    }

    // The literal of a pattern: raw when its text can stand in raw quotes, two times in three; otherwise with the escapes of the
    // string forms that a pattern needs: for a backslash, for a quote, for a line feed, and the long Unicode form for the rest.
    private StringLiteral PatternLiteral(string text)
    {
        List<int> codePoints = [.. text.EnumerateRunes().Select(rune => rune.Value)];
        Quote[] rawQuotes = [.. Enum.GetValues<Quote>().Where(quote => codePoints.TrueForAll(codePoint => TextUnits.CanBeLiteral(codePoint, quote, raw: true)))];
        if (rawQuotes.Length > 0 && pcg.Below(3) < 2)
        {
            return new StringLiteral(text, new TextForm(pcg.Pick(rawQuotes), RawPrefix() ?? 'r', [.. codePoints.Select(_ => Escape.Literal)]));
        }

        Quote quote = pcg.Pick(Enum.GetValues<Quote>());
        List<Escape> units = [];
        foreach (int codePoint in codePoints)
        {
            List<Escape> forms = TextUnits.StringForms(codePoint, quote, raw: false);
            units.Add(forms.Contains(Escape.Literal) ? Escape.Literal : forms.Contains(Escape.Character) ? Escape.Character : Escape.UnicodeLong);
        }

        return new StringLiteral(text, new TextForm(quote, null, units));
    }

    // ---- Literals ----

    // Parentheses around any node at random.
    private Expr Maybe(Expr expr) => pcg.OneIn(ParenthesesOneIn) ? expr with { Parenthesized = true } : expr;

    // A list or map literal sometimes ends with a comma, also without elements: [,] and {,}.
    private bool TrailingComma() => pcg.OneIn(TrailingCommaOneIn);

    // A key literal unequal to the keys before it; a string key is a field name half the time.
    private Expr? DistinctKey(HashSet<string> keys)
    {
        for (int attempt = 0; attempt < 4; attempt++)
        {
            ProfileType type = pcg.Pick(ProfileTables.KeyTypes);
            Expr key = type == ProfileType.StringType && pcg.OneIn(2) ? StringLiteral(pcg.Pick(ProfileTables.FieldNames)) : Literal(type);
            if (keys.Add(KeyIdentity(key)))
            {
                return Maybe(key);
            }
        }

        return null;
    }

    // The field as a string key, unless an equal key is there already.
    private StringLiteral? FieldKey(string field, HashSet<string> keys)
    {
        StringLiteral key = StringLiteral(field);
        return keys.Add(KeyIdentity(key)) ? key : null;
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

    // A built node: the expression, its number of nodes and its static type.
    private readonly record struct Built(Expr Expr, int Nodes, StaticType? Type);
}
