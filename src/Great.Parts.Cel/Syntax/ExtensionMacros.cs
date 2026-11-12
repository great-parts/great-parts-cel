using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Syntax;

/// <summary>
/// The macros of the extension sets as <see cref="Macro"/> objects, grouped by set: <c>math.greatest</c> and <c>math.least</c>
/// of <c>math</c>, <c>sortBy</c> of <c>lists</c>, <c>cel.bind</c> of <c>bindings</c>, and the macros of two iteration variables. A set brings its macros to the table of the parser of an environment
/// that has the set; without it the same call is an ordinary call.
/// </summary>
internal static class ExtensionMacros
{
    /// <summary>
    /// The name of the function that <c>transformMap</c> and <c>transformMapEntry</c> expand to, <c>cel.@mapInsert</c>: no text can
    /// write it.
    /// </summary>
    internal const string MapInsertFunction = "cel.@mapInsert";

    private const string MathNamespace = "math";
    private const string CelNamespace = "cel";
    private const string ProtoNamespace = "proto";

    // The function that math.greatest and math.least expand to: no text can write the name.
    private const string MaxFunction = "math.@max";
    private const string MinFunction = "math.@min";

    // The iteration variable of the comprehension that cel.bind and sortBy expand to; it has no range to take elements from.
    private const string UnusedVariable = "#unused";

    private static readonly Macro Greatest = new(
        "greatest",
        MathNamespace,
        Macro.AnyArgumentCount,
        isReceiver: true,
        static (target, arguments) => CheckExtreme("greatest", target, arguments),
        static (nodes, _, arguments, offset) => ExpandExtreme(MaxFunction, nodes, arguments, offset));

    private static readonly Macro Least = new(
        "least",
        MathNamespace,
        Macro.AnyArgumentCount,
        isReceiver: true,
        static (target, arguments) => CheckExtreme("least", target, arguments),
        static (nodes, _, arguments, offset) => ExpandExtreme(MinFunction, nodes, arguments, offset));

    // The hidden name that sortBy binds its receiver to, and the function that its expansion calls on it: no text can write either.
    internal const string SortByInput = "@__sortBy_input__";
    private const string SortByFunction = "@sortByAssociatedKeys";

    private static readonly Macro SortBy = new(
        "sortBy",
        null,
        2,
        isReceiver: true,
        static (target, arguments) => CheckSortBy(target!, arguments),
        static (nodes, target, arguments, offset) => ExpandSortBy(nodes, target!, arguments, offset));

    private static readonly Macro Bind = new(
        "bind",
        CelNamespace,
        3,
        isReceiver: true,
        static (_, arguments) => arguments[0] is IdentNode
            ? MacroCheck.Expand
            : MacroCheck.Error("cel.bind() variable names must be simple identifiers", arguments[0]),
        static (nodes, _, arguments, offset) => ExpandBind(nodes, arguments, offset));

    // proto.getExt(m, a.b.c) and proto.hasExt(m, a.b.c): the selection of the field whose name is the full name of the extension,
    // and the presence test of that selection.
    private static readonly Macro GetExt = new(
        "getExt",
        ProtoNamespace,
        2,
        isReceiver: true,
        static (_, arguments) => CheckExtension(arguments),
        static (nodes, _, arguments, offset) => ExpandExtension(nodes, arguments, offset, isPresenceTest: false));

    private static readonly Macro HasExt = new(
        "hasExt",
        ProtoNamespace,
        2,
        isReceiver: true,
        static (_, arguments) => CheckExtension(arguments),
        static (nodes, _, arguments, offset) => ExpandExtension(nodes, arguments, offset, isPresenceTest: true));

    // The macros of two iteration variables, all on a target of any kind: three arguments, and four with a filter for the
    // transformations. Every one of them checks the names of its variables alike.
    private static readonly Macro AllOfTwo = TwoVariables(
        "all",
        3,
        static (nodes, target, arguments, offset) => new Macro.Expansion(nodes, offset, twoVariables: true).All(target!, arguments));

    private static readonly Macro ExistsOfTwo = TwoVariables(
        "exists",
        3,
        static (nodes, target, arguments, offset) => new Macro.Expansion(nodes, offset, twoVariables: true).Exists(target!, arguments));

    private static readonly Macro ExistsOneOfTwo = TwoVariables("existsOne", 3, ExpandExistsOne);
    private static readonly Macro ExistsUnderscoreOneOfTwo = TwoVariables("exists_one", 3, ExpandExistsOne);
    private static readonly Macro TransformList3 = TwoVariables("transformList", 3, ExpandTransformList);
    private static readonly Macro TransformList4 = TwoVariables("transformList", 4, ExpandTransformList);
    private static readonly Macro TransformMap3 = TwoVariables("transformMap", 3, ExpandTransformMap);
    private static readonly Macro TransformMap4 = TwoVariables("transformMap", 4, ExpandTransformMap);
    private static readonly Macro TransformMapEntry3 = TwoVariables("transformMapEntry", 3, ExpandTransformMapEntry);
    private static readonly Macro TransformMapEntry4 = TwoVariables("transformMapEntry", 4, ExpandTransformMapEntry);

    /// <summary>
    /// The macros of the set <c>math</c>: <c>math.greatest</c> and <c>math.least</c>, in the order of its table.
    /// </summary>
    internal static ImmutableArray<Macro> Math { get; } = [Greatest, Least];

    /// <summary>
    /// The macros of the set <c>lists</c>: <c>sortBy</c>.
    /// </summary>
    internal static ImmutableArray<Macro> Lists { get; } = [SortBy];

    /// <summary>
    /// The macros of the set <c>bindings</c>: <c>cel.bind</c>.
    /// </summary>
    internal static ImmutableArray<Macro> Bindings { get; } = [Bind];

    /// <summary>
    /// The macros of the set <c>protos</c>: <c>proto.getExt</c> and <c>proto.hasExt</c>.
    /// </summary>
    internal static ImmutableArray<Macro> Protos { get; } = [GetExt, HasExt];

    /// <summary>
    /// The macros of the set of two-variable comprehensions: <c>all</c>, <c>exists</c>, <c>existsOne</c>, <c>exists_one</c>,
    /// <c>transformList</c>, <c>transformMap</c> and <c>transformMapEntry</c>, in the order of its table; each transformation is
    /// one entry for three arguments and one for four.
    /// </summary>
    internal static ImmutableArray<Macro> TwoVariableComprehensions { get; } =
    [
        AllOfTwo,
        ExistsOfTwo,
        ExistsOneOfTwo,
        ExistsUnderscoreOneOfTwo,
        TransformList3,
        TransformList4,
        TransformMap3,
        TransformMap4,
        TransformMapEntry3,
        TransformMapEntry4,
    ];

    // A macro of two variables of a receiver of any kind, with the names checked by CheckVariables.
    private static Macro TwoVariables(string name, int argumentCount, MacroExpansion expand) =>
        new(name, null, argumentCount, isReceiver: true, static (_, arguments) => CheckVariables(arguments), expand);

    // The names of the two iteration variables, in the order of cel-go: the first is an identifier, then the second, then they
    // differ — the error is at the second — then the first and the second are not the accumulator.
    private static MacroCheck CheckVariables(ImmutableArray<ExprNode> arguments)
    {
        if (arguments[0] is not IdentNode first)
        {
            return MacroCheck.Error("argument must be a simple name", arguments[0]);
        }

        if (arguments[1] is not IdentNode second)
        {
            return MacroCheck.Error("argument must be a simple name", arguments[1]);
        }

        if (string.Equals(first.Name, second.Name, StringComparison.Ordinal))
        {
            return MacroCheck.Error("duplicate variable name: " + second.Name, arguments[1]);
        }

        if (Macro.IsAccumulatorName(first.Name))
        {
            return MacroCheck.Error("iteration variable overwrites accumulator variable", arguments[0]);
        }

        return Macro.IsAccumulatorName(second.Name)
            ? MacroCheck.Error("iteration variable overwrites accumulator variable", arguments[1])
            : MacroCheck.Expand;
    }

    private static ComprehensionNode ExpandExistsOne(NodeFactory nodes, ExprNode? target, ImmutableArray<ExprNode> arguments, int offset) =>
        new Macro.Expansion(nodes, offset, twoVariables: true).ExistsOne(target!, arguments);

    private static ComprehensionNode ExpandTransformList(NodeFactory nodes, ExprNode? target, ImmutableArray<ExprNode> arguments, int offset) =>
        new Macro.Expansion(nodes, offset, twoVariables: true).Map(target!, arguments);

    private static ComprehensionNode ExpandTransformMap(NodeFactory nodes, ExprNode? target, ImmutableArray<ExprNode> arguments, int offset) =>
        new Macro.Expansion(nodes, offset, twoVariables: true).TransformMap(target!, arguments);

    private static ComprehensionNode ExpandTransformMapEntry(NodeFactory nodes, ExprNode? target, ImmutableArray<ExprNode> arguments, int offset) =>
        new Macro.Expansion(nodes, offset, twoVariables: true).TransformMapEntry(target!, arguments);

    // The second argument of getExt and hasExt is a chain of selections that ends in an identifier — a.b.c — with at least one
    // selection and no presence test among them: a bare identifier, a call, a constant, a list or a has() is not one. The error is
    // at the argument.
    private static MacroCheck CheckExtension(ImmutableArray<ExprNode> arguments)
    {
        ExprNode node = arguments[1];
        int selections = 0;
        while (node is SelectNode { IsPresenceTest: false } select)
        {
            selections++;
            node = select.Operand;
        }

        return selections > 0 && node is IdentNode
            ? MacroCheck.Expand
            : MacroCheck.Error("invalid extension field", arguments[1]);
    }

    // The selection of the field named by the chain — the names joined by dots, a leading dot of the root kept as it is — on the
    // first argument, as a selection or a presence test. It is the selection of an escaped field name, m.`a.b.c`, so checking and
    // evaluation need nothing for it; the nodes of the chain are not part of it.
    private static SelectNode ExpandExtension(NodeFactory nodes, ImmutableArray<ExprNode> arguments, int offset, bool isPresenceTest)
    {
        // The chain is walked from its last selection to its root, so its depth does not use the stack.
        List<string> fields = [];
        ExprNode node = arguments[1];
        while (node is SelectNode select)
        {
            fields.Add(select.Field);
            node = select.Operand;
        }

        var name = new StringBuilder(((IdentNode)node).Name);
        for (int i = fields.Count - 1; i >= 0; i--)
        {
            name.Append('.').Append(fields[i]);
        }

        return nodes.Select(arguments[0], name.ToString(), isPresenceTest, offset);
    }

    // The checks of math.greatest and math.least by the kind of the node, in the order of the rows of the table of errors: no
    // argument, then one argument that is neither a number nor a non-empty list literal of numbers, then the first argument of
    // several that is not a number. Whether an expression is a number is left to the overloads of the expansion.
    private static MacroCheck CheckExtreme(string name, ExprNode? target, ImmutableArray<ExprNode> arguments)
    {
        if (arguments.Length == 0)
        {
            return MacroCheck.Error("math." + name + "() requires at least one argument", target);
        }

        if (arguments.Length == 1)
        {
            return IsNumeric(arguments[0]) || IsNumericList(arguments[0])
                ? MacroCheck.Expand
                : MacroCheck.Error("math." + name + "() invalid single argument value", arguments[0]);
        }

        foreach (ExprNode argument in arguments)
        {
            if (!IsNumeric(argument))
            {
                return MacroCheck.Error("math." + name + "() simple literal arguments must be numeric", argument);
            }
        }

        return MacroCheck.Expand;
    }

    // One argument: math.@max(a); two: math.@max(a, b); three or more: math.@max([a, b, …]) with a list literal of the arguments.
    private static CallNode ExpandExtreme(string function, NodeFactory nodes, ImmutableArray<ExprNode> arguments, int offset)
    {
        if (arguments.Length <= 2)
        {
            return nodes.Call(function, null, arguments, offset);
        }

        ListNode list = nodes.List(arguments, [], offset);
        return nodes.Call(function, null, [list], offset);
    }

    // The receiver of sortBy is a list literal, an identifier, a selection, a comprehension or a call; the name is an identifier that
    // is not the accumulator. The first fault is the error: the receiver is its place for the kind, the argument for the name.
    private static MacroCheck CheckSortBy(ExprNode target, ImmutableArray<ExprNode> arguments)
    {
        if (target is not (ListNode or IdentNode or SelectNode or ComprehensionNode or CallNode))
        {
            return MacroCheck.Error("sortBy can only be applied to a list, identifier, comprehension, call or select expression", target);
        }

        if (arguments[0] is not IdentNode name)
        {
            return MacroCheck.Error("argument is not an identifier", arguments[0]);
        }

        return Macro.IsAccumulatorName(name.Name)
            ? MacroCheck.Error("iteration variable overwrites accumulator variable", arguments[0])
            : MacroCheck.Expand;
    }

    // The receiver bound to the hidden name by a comprehension over the empty list that takes no step, whose result sorts it by the
    // keys of a map of the receiver: r.map(v, k), as the standard macro builds it, over the hidden name.
    private static ComprehensionNode ExpandSortBy(NodeFactory nodes, ExprNode target, ImmutableArray<ExprNode> arguments, int offset)
    {
        ListNode range = nodes.List([], [], offset);
        ConstantNode condition = nodes.Constant(CelValue.FromBoolean(false), offset);
        IdentNode step = nodes.Ident(SortByInput, offset);
        IdentNode receiver = nodes.Ident(SortByInput, offset);
        IdentNode keysRange = nodes.Ident(SortByInput, offset);
        ComprehensionNode keys = new Macro.Expansion(nodes, offset).Map(keysRange, arguments);

        // The map of the keys is recorded as the standard macro map, which the planner charges as it charges that macro: 1 for the empty
        // list, 1 for each step with the expression of the key, and 1 for each key added.
        nodes.RecordMacroCall(keys, new MacroCall("map", keysRange, arguments));
        CallNode result = nodes.Call(SortByFunction, receiver, [keys], offset);
        return nodes.Comprehension(UnusedVariable, range, SortByInput, target, condition, step, result, offset);
    }

    // A comprehension over the empty list: the variable is the accumulator, the initial value is the bound expression, the
    // condition is false so that no step runs, and the result is the third argument. The parts are created in the order of cel-go:
    // the range, the condition, the step, the comprehension.
    private static ComprehensionNode ExpandBind(NodeFactory nodes, ImmutableArray<ExprNode> arguments, int offset)
    {
        string name = ((IdentNode)arguments[0]).Name;
        ListNode range = nodes.List([], [], offset);
        ConstantNode condition = nodes.Constant(CelValue.FromBoolean(false), offset);
        IdentNode step = nodes.Ident(name, offset);
        return nodes.Comprehension(UnusedVariable, range, name, arguments[1], condition, step, arguments[2], offset);
    }

    // A constant int, uint or double, or any node that is not a constant and not a list, map or message literal: an identifier,
    // a selection, a call such as dyn(5.4), an operator or a comprehension. The decision is by the kind of the node, not by the
    // type.
    private static bool IsNumeric(ExprNode node) => node switch
    {
        ConstantNode constant => OrderingRules.IsNumber(constant.Value.Kind),
        ListNode or StructNode => false,
        _ => true,
    };

    // A list literal with at least one element, every element numeric.
    private static bool IsNumericList(ExprNode node)
    {
        if (node is not ListNode { Elements.Length: > 0 } list)
        {
            return false;
        }

        foreach (ExprNode element in list.Elements)
        {
            if (!IsNumeric(element))
            {
                return false;
            }
        }

        return true;
    }
}
