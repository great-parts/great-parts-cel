using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Syntax;

/// <summary>
/// The macros of a parser by name, argument count and call style. Immutable.
/// </summary>
internal sealed class MacroTable
{
    private readonly Macro[] _macros;

    private MacroTable(Macro[] macros)
    {
        _macros = macros;
    }

    /// <summary>
    /// The macros in the order they were given.
    /// </summary>
    internal ReadOnlySpan<Macro> Macros => _macros;

    /// <summary>
    /// Creates a table from macros with distinct keys: the name, the call style and the number of arguments, or any number.
    /// </summary>
    /// <param name="macros">The macros, the standard ones and those of the extension sets of an environment.</param>
    /// <returns>The table.</returns>
    /// <exception cref="ArgumentException">Two macros have one key.</exception>
    internal static MacroTable Create(IEnumerable<Macro> macros)
    {
        List<Macro> table = [];
        foreach (Macro macro in macros)
        {
            foreach (Macro kept in table)
            {
                if (kept.HasKeyOf(macro))
                {
                    throw new ArgumentException(
                        "Two macros have the key " + macro.Name + "/" + macro.ArgumentCount + ", receiver " + macro.IsReceiver + ".",
                        nameof(macros));
                }
            }

            table.Add(macro);
        }

        return new MacroTable([.. table]);
    }

    /// <summary>
    /// The standard macros of CEL, <c>StandardMacros</c> of cel-go: <c>has(e.f)</c>, and <c>r.all(v, p)</c>,
    /// <c>r.exists(v, p)</c>, <c>r.exists_one(v, p)</c>, <c>r.map(v, f)</c>, <c>r.map(v, p, f)</c> and
    /// <c>r.filter(v, p)</c>.
    /// </summary>
    internal static MacroTable Standard { get; } = new(
    [
        new("has", 1, isReceiver: false, MacroKind.Has),
        new("all", 2, isReceiver: true, MacroKind.All),
        new("exists", 2, isReceiver: true, MacroKind.Exists),
        new("exists_one", 2, isReceiver: true, MacroKind.ExistsOne),
        new("map", 2, isReceiver: true, MacroKind.Map),
        new("map", 3, isReceiver: true, MacroKind.Map),
        new("filter", 2, isReceiver: true, MacroKind.Filter),
    ]);

    /// <summary>
    /// No macros: every call is a call.
    /// </summary>
    internal static MacroTable Empty { get; } = new([]);

    /// <summary>
    /// Every macro of the library: the standard ones and those of every extension set. The expansion of a macro does not depend on
    /// the environment, so a tree can be built for a macro that an environment brings.
    /// </summary>
    internal static MacroTable All { get; } = Create(
    [
        .. Standard.Macros.ToArray(),
        .. ExtensionMacros.Math,
        .. ExtensionMacros.Lists,
        .. ExtensionMacros.Bindings,
        .. ExtensionMacros.Protos,
        .. ExtensionMacros.Optional,
        .. ExtensionMacros.Block,
        .. ExtensionMacros.TwoVariableComprehensions,
    ]);

    /// <summary>
    /// Finds a macro by the name that a text calls it by, such as <c>exists</c> or <c>cel.bind</c>: the entry with exactly that
    /// number of arguments, otherwise the entry for any number. A macro of a namespace is called with the namespace in the name and
    /// without a target; any other macro is called with a target when it takes a receiver.
    /// </summary>
    /// <param name="name">The name, qualified by the namespace for a macro of a namespace.</param>
    /// <param name="argumentCount">The number of arguments.</param>
    /// <param name="hasTarget">Whether the call has a target other than the namespace.</param>
    /// <returns>The macro, or <see langword="null"/> when no macro has the name, the number of arguments and the style.</returns>
    internal Macro? FindQualified(string name, int argumentCount, bool hasTarget)
    {
        Macro? forAnyNumber = null;
        foreach (Macro macro in _macros)
        {
            bool styleMatches = macro.Namespace is null ? macro.IsReceiver == hasTarget : !hasTarget;
            if (!styleMatches || !string.Equals(macro.QualifiedName, name, StringComparison.Ordinal))
            {
                continue;
            }

            if (macro.ArgumentCount == argumentCount)
            {
                return macro;
            }

            if (macro.ArgumentCount == Macro.AnyArgumentCount)
            {
                forAnyNumber = macro;
            }
        }

        return forAnyNumber;
    }

    /// <summary>
    /// Finds the macro of a call: the entry with exactly that number of arguments, otherwise the entry for any number.
    /// </summary>
    /// <param name="function">The name of the function of the call.</param>
    /// <param name="argumentCount">The number of arguments of the call.</param>
    /// <param name="isReceiver">Whether the call has a target, <c>target.f(args)</c>.</param>
    /// <returns>The macro, or <see langword="null"/> when the call is not a macro call.</returns>
    internal Macro? Find(string function, int argumentCount, bool isReceiver)
    {
        Macro? forAnyNumber = null;
        foreach (Macro macro in _macros)
        {
            if (macro.IsReceiver != isReceiver || !string.Equals(macro.Name, function, StringComparison.Ordinal))
            {
                continue;
            }

            if (macro.ArgumentCount == argumentCount)
            {
                return macro;
            }

            if (macro.ArgumentCount == Macro.AnyArgumentCount)
            {
                forAnyNumber = macro;
            }
        }

        return forAnyNumber;
    }
}

/// <summary>
/// Creates the nodes of the expansion of a macro call that the macro accepted.
/// </summary>
/// <param name="nodes">The factory of the tree.</param>
/// <param name="target">The target of the call; <see langword="null"/> for a global macro.</param>
/// <param name="arguments">The arguments.</param>
/// <param name="offset">The offset of the <c>(</c> of the call, which every node created has.</param>
/// <returns>The expansion.</returns>
internal delegate CelExprNode MacroExpansion(NodeFactory nodes, CelExprNode? target, ImmutableArray<CelExprNode> arguments, int offset);

/// <summary>
/// What a macro says about a call: it does not apply, the arguments are wrong, or the call is expanded.
/// </summary>
internal readonly struct MacroCheck
{
    private readonly byte _answer;

    private MacroCheck(byte answer, string? detail, CelExprNode? place)
    {
        _answer = answer;
        Detail = detail;
        Place = place;
    }

    /// <summary>
    /// The call stays a call: the target is not the identifier that the macro needs.
    /// </summary>
    internal static MacroCheck NotApplicable => new(1, null, null);

    /// <summary>
    /// The call is expanded.
    /// </summary>
    internal static MacroCheck Expand => new(0, null, null);

    /// <summary>
    /// Whether the call stays a call.
    /// </summary>
    internal bool IsNotApplicable => _answer == 1;

    /// <summary>
    /// Whether the arguments are wrong.
    /// </summary>
    internal bool IsError => _answer == 2;

    /// <summary>
    /// The detail of <see cref="CelErrorCode.InvalidMacroCall"/> of an error.
    /// </summary>
    internal string? Detail { get; }

    /// <summary>
    /// The node at which an error is reported; <see langword="null"/> for the <c>(</c> of the call.
    /// </summary>
    internal CelExprNode? Place { get; }

    /// <summary>
    /// The arguments are wrong.
    /// </summary>
    /// <param name="detail">The detail of the error.</param>
    /// <param name="place">The node of its place, or <see langword="null"/> for the call.</param>
    /// <returns>The answer.</returns>
    internal static MacroCheck Error(string detail, CelExprNode? place) => new(2, detail, place);
}

/// <summary>
/// The macros the parser can expand.
/// </summary>
internal enum MacroKind
{
    /// <summary>
    /// <c>has(e.f)</c>: a presence test.
    /// </summary>
    Has,

    /// <summary>
    /// <c>r.all(v, p)</c>.
    /// </summary>
    All,

    /// <summary>
    /// <c>r.exists(v, p)</c>.
    /// </summary>
    Exists,

    /// <summary>
    /// <c>r.exists_one(v, p)</c>.
    /// </summary>
    ExistsOne,

    /// <summary>
    /// <c>r.map(v, f)</c> and <c>r.map(v, p, f)</c>.
    /// </summary>
    Map,

    /// <summary>
    /// <c>r.filter(v, p)</c>.
    /// </summary>
    Filter,
}

/// <summary>
/// A macro: a call that the parser replaces by its expansion, in the shape of <c>parser/macro.go</c> of cel-go with
/// the hidden accumulator.
/// </summary>
internal sealed class Macro
{
    /// <summary>
    /// The number of arguments of a macro that takes any number, such as <c>math.greatest</c>.
    /// </summary>
    internal const int AnyArgumentCount = -1;

    /// <summary>
    /// The name of the accumulator of a comprehension that a macro creates, <c>@result</c>: no text can write it
    /// (<c>EnableHiddenAccumulatorName</c> of cel-go).
    /// </summary>
    internal const string AccumulatorName = "@result";

    // The accumulator of cel-go without the hidden one; an iteration variable of this name is still an error.
    private const string LegacyAccumulatorName = "__result__";

    private readonly MacroKind _kind;
    private readonly Func<CelExprNode?, ImmutableArray<CelExprNode>, MacroCheck>? _check;
    private readonly MacroExpansion? _expand;

    internal Macro(string name, int argumentCount, bool isReceiver, MacroKind kind)
    {
        Name = name;
        QualifiedName = name;
        ArgumentCount = argumentCount;
        IsReceiver = isReceiver;
        _kind = kind;
    }

    /// <summary>
    /// Creates a macro of an extension set.
    /// </summary>
    /// <param name="name">The name of the called function, without the target.</param>
    /// <param name="namespace">
    /// The identifier the target must be, such as <c>math</c> for <c>math.greatest</c>; <see langword="null"/> for a macro of
    /// any target.
    /// </param>
    /// <param name="argumentCount">The number of arguments, or <see cref="AnyArgumentCount"/>.</param>
    /// <param name="isReceiver">Whether the call has a target.</param>
    /// <param name="check">Tells for a call with the target and the arguments whether the macro applies, fails or expands.</param>
    /// <param name="expand">Creates the nodes of an expansion that <paramref name="check"/> accepted.</param>
    internal Macro(
        string name,
        string? @namespace,
        int argumentCount,
        bool isReceiver,
        Func<CelExprNode?, ImmutableArray<CelExprNode>, MacroCheck> check,
        MacroExpansion expand)
    {
        Name = name;
        QualifiedName = @namespace is null ? name : @namespace + "." + name;
        Namespace = @namespace;
        ArgumentCount = argumentCount;
        IsReceiver = isReceiver;
        _check = check;
        _expand = expand;
    }

    /// <summary>
    /// The identifier that the target of a call must be for the macro to apply; <see langword="null"/> for a macro of any
    /// target.
    /// </summary>
    internal string? Namespace { get; }

    /// <summary>
    /// The name of the function of the call.
    /// </summary>
    internal string Name { get; }

    /// <summary>
    /// The name that the text calls the macro by: <see cref="Name"/>, and for a macro of a namespace, such as
    /// <c>math.greatest</c>, the namespace and the name joined by a dot.
    /// </summary>
    internal string QualifiedName { get; }

    /// <summary>
    /// The record of a call of this macro as the text writes it: the receiver of a macro of a namespace, which is the
    /// identifier of the namespace, is part of the name.
    /// </summary>
    /// <param name="target">The target of the call; <see langword="null"/> for a global macro.</param>
    /// <param name="arguments">The arguments.</param>
    /// <returns>The record.</returns>
    internal CelMacroCall Recorded(CelExprNode? target, ImmutableArray<CelExprNode> arguments) =>
        new(QualifiedName, Namespace is null ? target : null, arguments);

    /// <summary>
    /// The number of arguments of the call, or <see cref="AnyArgumentCount"/>.
    /// </summary>
    internal int ArgumentCount { get; }

    /// <summary>
    /// Whether the call has a target, <c>target.f(args)</c>.
    /// </summary>
    internal bool IsReceiver { get; }

    /// <summary>
    /// Whether an iteration variable of this name would overwrite the accumulator: <see cref="AccumulatorName"/> or the
    /// accumulator of cel-go without the hidden name.
    /// </summary>
    /// <param name="name">The name of the iteration variable.</param>
    /// <returns><see langword="true"/> for the name of an accumulator.</returns>
    internal static bool IsAccumulatorName(string name) =>
        string.Equals(name, AccumulatorName, StringComparison.Ordinal) || string.Equals(name, LegacyAccumulatorName, StringComparison.Ordinal);

    /// <summary>
    /// Whether another macro has the same key: the name, the call style and the number of arguments.
    /// </summary>
    /// <param name="other">The other macro.</param>
    /// <returns><see langword="true"/> when a call could not tell the two apart.</returns>
    internal bool HasKeyOf(Macro other) =>
        IsReceiver == other.IsReceiver
        && ArgumentCount == other.ArgumentCount
        && string.Equals(Name, other.Name, StringComparison.Ordinal);

    /// <summary>
    /// Checks a call of the macro.
    /// </summary>
    /// <param name="target">The target of the call; <see langword="null"/> for a global one.</param>
    /// <param name="arguments">The arguments, as many as <see cref="ArgumentCount"/>, or any number for a macro of any number.</param>
    /// <returns>
    /// Not applicable when the call stays a call; an error with the detail of <see cref="CelErrorCode.InvalidMacroCall"/> and
    /// the node of its place, the call itself for a standard macro; otherwise that the call is expanded.
    /// </returns>
    internal MacroCheck Check(CelExprNode? target, ImmutableArray<CelExprNode> arguments)
    {
        if (_check is not null)
        {
            // A macro with a namespace applies only to a call on the identifier of that name, as in cel-go.
            if (Namespace is not null && !(target is CelIdentNode identifier && string.Equals(identifier.Name, Namespace, StringComparison.Ordinal)))
            {
                return MacroCheck.NotApplicable;
            }

            return _check(target, arguments);
        }

        if (_kind == MacroKind.Has)
        {
            return arguments[0] is CelSelectNode ? MacroCheck.Expand : MacroCheck.Error("argument must be a field selection", null);
        }

        return arguments[0] switch
        {
            CelIdentNode { Name: LegacyAccumulatorName } => MacroCheck.Error("iteration variable overwrites accumulator variable", null),
            CelIdentNode => MacroCheck.Expand,
            _ => MacroCheck.Error("argument must be a simple name", null),
        };
    }

    /// <summary>
    /// Expands a call of the macro whose arguments passed <see cref="Check"/>. Every node it creates is at the offset
    /// of the call.
    /// </summary>
    /// <param name="nodes">The factory of the tree.</param>
    /// <param name="target">The target of the call; <see langword="null"/> only for a global macro.</param>
    /// <param name="arguments">The arguments.</param>
    /// <param name="offset">The offset of the <c>(</c> of the call.</param>
    /// <returns>The expansion: a presence test for <c>has</c>, otherwise a comprehension.</returns>
    internal CelExprNode Expand(NodeFactory nodes, CelExprNode? target, ImmutableArray<CelExprNode> arguments, int offset)
    {
        if (_expand is not null)
        {
            return _expand(nodes, target, arguments, offset);
        }

        if (_kind == MacroKind.Has)
        {
            var select = (CelSelectNode)arguments[0];
            return nodes.Select(select.Operand, select.Field, isPresenceTest: true, offset);
        }

        var expansion = new Expansion(nodes, offset);
        return _kind switch
        {
            MacroKind.All => expansion.All(target!, arguments),
            MacroKind.Exists => expansion.Exists(target!, arguments),
            MacroKind.ExistsOne => expansion.ExistsOne(target!, arguments),
            MacroKind.Map => expansion.Map(target!, arguments),
            _ => expansion.Filter(target!, arguments),
        };
    }

    // The comprehensions of the macros. The parts are created in the order AccuInit, LoopCondition, LoopStep, Result,
    // each after its children and the arguments of a call from the left, then the comprehension, as in cel-go. A macro of two
    // variables has two names before its other arguments, so the predicate or the transformation is the third argument.
    internal readonly struct Expansion(NodeFactory nodes, int offset, bool twoVariables = false)
    {
        // The position of the first argument after the names of the iteration variables.
        private int Body => twoVariables ? 2 : 1;

        internal CelComprehensionNode All(CelExprNode range, ImmutableArray<CelExprNode> arguments)
        {
            CelExprNode init = Constant(CelValue.FromBoolean(true));
            CelExprNode condition = Call("@not_strictly_false", Accumulator());
            CelExprNode step = Call("_&&_", Accumulator(), arguments[Body]);
            return Comprehension(range, arguments, init, condition, step, Accumulator());
        }

        internal CelComprehensionNode Exists(CelExprNode range, ImmutableArray<CelExprNode> arguments)
        {
            CelExprNode init = Constant(CelValue.FromBoolean(false));
            CelExprNode condition = Call("@not_strictly_false", Call("!_", Accumulator()));
            CelExprNode step = Call("_||_", Accumulator(), arguments[Body]);
            return Comprehension(range, arguments, init, condition, step, Accumulator());
        }

        internal CelComprehensionNode ExistsOne(CelExprNode range, ImmutableArray<CelExprNode> arguments)
        {
            CelExprNode init = Constant(CelValue.FromInt64(0));
            CelExprNode condition = Constant(CelValue.FromBoolean(true));
            CelExprNode step = Call("_?_:_", arguments[Body], Call("_+_", Accumulator(), Constant(CelValue.FromInt64(1))), Accumulator());
            CelExprNode result = Call("_==_", Accumulator(), Constant(CelValue.FromInt64(1)));
            return Comprehension(range, arguments, init, condition, step, result);
        }

        // r.map(v, f) and r.map(v, p, f); with two variables, r.transformList(i, v, t) and r.transformList(i, v, p, t).
        internal CelComprehensionNode Map(CelExprNode range, ImmutableArray<CelExprNode> arguments)
        {
            CelExprNode init = nodes.List([], [], offset);
            CelExprNode condition = Constant(CelValue.FromBoolean(true));
            CelExprNode step = Filtered(arguments, Append(arguments[^1]));
            return Comprehension(range, arguments, init, condition, step, Accumulator());
        }

        // r.transformMap(i, v, t) and r.transformMap(i, v, p, t): the accumulator is a map, and the step inserts the pair of the
        // first variable and the transformation. The read of the first variable is an identifier of its own.
        internal CelComprehensionNode TransformMap(CelExprNode range, ImmutableArray<CelExprNode> arguments)
        {
            CelExprNode init = nodes.Map([], offset);
            CelExprNode condition = Constant(CelValue.FromBoolean(true));
            CelExprNode step = Call(
                ExtensionMacros.MapInsertFunction,
                Accumulator(),
                nodes.Ident(((CelIdentNode)arguments[0]).Name, offset),
                arguments[^1]);
            return Comprehension(range, arguments, init, condition, Filtered(arguments, step), Accumulator());
        }

        // r.transformMapEntry(i, v, t) and r.transformMapEntry(i, v, p, t): the step inserts the pairs of the map that the
        // transformation gives.
        internal CelComprehensionNode TransformMapEntry(CelExprNode range, ImmutableArray<CelExprNode> arguments)
        {
            CelExprNode init = nodes.Map([], offset);
            CelExprNode condition = Constant(CelValue.FromBoolean(true));
            CelExprNode step = Call(ExtensionMacros.MapInsertFunction, Accumulator(), arguments[^1]);
            return Comprehension(range, arguments, init, condition, Filtered(arguments, step), Accumulator());
        }

        // The step keeps the identifier node of the first argument.
        internal CelComprehensionNode Filter(CelExprNode range, ImmutableArray<CelExprNode> arguments)
        {
            CelExprNode init = nodes.List([], [], offset);
            CelExprNode condition = Constant(CelValue.FromBoolean(true));
            CelExprNode step = Call("_?_:_", arguments[1], Append(arguments[0]), Accumulator());
            return Comprehension(range, arguments, init, condition, step, Accumulator());
        }

        // @result + [element]
        private CelCallNode Append(CelExprNode element) => Call("_+_", Accumulator(), nodes.List([element], [], offset));

        // With a filter, the third argument of a macro of one variable and the fourth of two, the step runs only when it is true.
        private CelExprNode Filtered(ImmutableArray<CelExprNode> arguments, CelExprNode step) =>
            arguments.Length == Body + 2 ? Call("_?_:_", arguments[Body], step, Accumulator()) : step;

        private CelComprehensionNode Comprehension(
            CelExprNode range,
            ImmutableArray<CelExprNode> arguments,
            CelExprNode init,
            CelExprNode condition,
            CelExprNode step,
            CelExprNode result)
        {
            string first = ((CelIdentNode)arguments[0]).Name;
            return twoVariables
                ? nodes.Comprehension(first, ((CelIdentNode)arguments[1]).Name, range, AccumulatorName, init, condition, step, result, offset)
                : nodes.Comprehension(first, range, AccumulatorName, init, condition, step, result, offset);
        }

        private CelIdentNode Accumulator() => nodes.Ident(AccumulatorName, offset);

        private CelConstantNode Constant(CelValue value) => nodes.Constant(value, offset);

        private CelCallNode Call(string function, params ReadOnlySpan<CelExprNode> arguments) =>
            nodes.Call(function, null, [.. arguments], offset);
    }
}
