using System;
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
    /// Finds the macro of a call.
    /// </summary>
    /// <param name="function">The name of the function of the call.</param>
    /// <param name="argumentCount">The number of arguments of the call.</param>
    /// <param name="isReceiver">Whether the call has a target, <c>target.f(args)</c>.</param>
    /// <returns>The macro, or <see langword="null"/> when the call is not a macro call.</returns>
    internal Macro? Find(string function, int argumentCount, bool isReceiver)
    {
        foreach (Macro macro in _macros)
        {
            if (macro.ArgumentCount == argumentCount && macro.IsReceiver == isReceiver
                && string.Equals(macro.Name, function, StringComparison.Ordinal))
            {
                return macro;
            }
        }

        return null;
    }
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
    /// The name of the accumulator of a comprehension that a macro creates, <c>@result</c>: no text can write it
    /// (<c>EnableHiddenAccumulatorName</c> of cel-go).
    /// </summary>
    internal const string AccumulatorName = "@result";

    // The accumulator of cel-go without the hidden one; an iteration variable of this name is still an error.
    private const string LegacyAccumulatorName = "__result__";

    private readonly MacroKind _kind;

    internal Macro(string name, int argumentCount, bool isReceiver, MacroKind kind)
    {
        Name = name;
        ArgumentCount = argumentCount;
        IsReceiver = isReceiver;
        _kind = kind;
    }

    /// <summary>
    /// The name of the function of the call.
    /// </summary>
    internal string Name { get; }

    /// <summary>
    /// The number of arguments of the call.
    /// </summary>
    internal int ArgumentCount { get; }

    /// <summary>
    /// Whether the call has a target, <c>target.f(args)</c>.
    /// </summary>
    internal bool IsReceiver { get; }

    /// <summary>
    /// Checks the arguments of a call of the macro.
    /// </summary>
    /// <param name="arguments">The arguments, as many as <see cref="ArgumentCount"/>.</param>
    /// <returns>
    /// <see langword="null"/> when the call can be expanded; otherwise the detail of
    /// <see cref="CelErrorCode.InvalidMacroCall"/>.
    /// </returns>
    internal string? Check(ImmutableArray<ExprNode> arguments)
    {
        if (_kind == MacroKind.Has)
        {
            return arguments[0] is SelectNode ? null : "argument must be a field selection";
        }

        return arguments[0] switch
        {
            IdentNode { Name: LegacyAccumulatorName } => "iteration variable overwrites accumulator variable",
            IdentNode => null,
            _ => "argument must be a simple name",
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
    internal ExprNode Expand(NodeFactory nodes, ExprNode? target, ImmutableArray<ExprNode> arguments, int offset)
    {
        if (_kind == MacroKind.Has)
        {
            var select = (SelectNode)arguments[0];
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
    // each after its children and the arguments of a call from the left, then the comprehension, as in cel-go.
    private readonly struct Expansion(NodeFactory nodes, int offset)
    {
        internal ComprehensionNode All(ExprNode range, ImmutableArray<ExprNode> arguments)
        {
            ExprNode init = Constant(CelValue.FromBoolean(true));
            ExprNode condition = Call("@not_strictly_false", Accumulator());
            ExprNode step = Call("_&&_", Accumulator(), arguments[1]);
            return Comprehension(range, arguments, init, condition, step, Accumulator());
        }

        internal ComprehensionNode Exists(ExprNode range, ImmutableArray<ExprNode> arguments)
        {
            ExprNode init = Constant(CelValue.FromBoolean(false));
            ExprNode condition = Call("@not_strictly_false", Call("!_", Accumulator()));
            ExprNode step = Call("_||_", Accumulator(), arguments[1]);
            return Comprehension(range, arguments, init, condition, step, Accumulator());
        }

        internal ComprehensionNode ExistsOne(ExprNode range, ImmutableArray<ExprNode> arguments)
        {
            ExprNode init = Constant(CelValue.FromInt64(0));
            ExprNode condition = Constant(CelValue.FromBoolean(true));
            ExprNode step = Call("_?_:_", arguments[1], Call("_+_", Accumulator(), Constant(CelValue.FromInt64(1))), Accumulator());
            ExprNode result = Call("_==_", Accumulator(), Constant(CelValue.FromInt64(1)));
            return Comprehension(range, arguments, init, condition, step, result);
        }

        // r.map(v, f) and r.map(v, p, f).
        internal ComprehensionNode Map(ExprNode range, ImmutableArray<ExprNode> arguments)
        {
            ExprNode init = nodes.List([], [], offset);
            ExprNode condition = Constant(CelValue.FromBoolean(true));
            ExprNode step = Append(arguments[^1]);
            if (arguments.Length == 3)
            {
                step = Call("_?_:_", arguments[1], step, Accumulator());
            }

            return Comprehension(range, arguments, init, condition, step, Accumulator());
        }

        // The step keeps the identifier node of the first argument.
        internal ComprehensionNode Filter(ExprNode range, ImmutableArray<ExprNode> arguments)
        {
            ExprNode init = nodes.List([], [], offset);
            ExprNode condition = Constant(CelValue.FromBoolean(true));
            ExprNode step = Call("_?_:_", arguments[1], Append(arguments[0]), Accumulator());
            return Comprehension(range, arguments, init, condition, step, Accumulator());
        }

        // @result + [element]
        private CallNode Append(ExprNode element) => Call("_+_", Accumulator(), nodes.List([element], [], offset));

        private ComprehensionNode Comprehension(
            ExprNode range,
            ImmutableArray<ExprNode> arguments,
            ExprNode init,
            ExprNode condition,
            ExprNode step,
            ExprNode result) =>
            nodes.Comprehension(((IdentNode)arguments[0]).Name, range, AccumulatorName, init, condition, step, result, offset);

        private IdentNode Accumulator() => nodes.Ident(AccumulatorName, offset);

        private ConstantNode Constant(CelValue value) => nodes.Constant(value, offset);

        private CallNode Call(string function, params ReadOnlySpan<ExprNode> arguments) =>
            nodes.Call(function, null, [.. arguments], offset);
    }
}
