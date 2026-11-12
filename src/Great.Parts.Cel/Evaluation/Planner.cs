using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Great.Parts.Cel.Checking;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Syntax;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Evaluation;

// Turns a checked expression, or a parsed one without type checking, into a plan of immutable nodes.
// Each distinct variable gets one slot that all its reads share, and each comprehension two local slots,
// for its iteration variable and its accumulator. Chains of _&&_ and of _||_ calls, which the parser builds
// as balanced trees, become one node each with the operands in the order of the text and the position of
// the outermost call. The parts that the expansion of a standard macro adds cost nothing; the expressions
// the author wrote in it are charged by their rows.
internal sealed class Planner
{
    private const string ConditionalFunction = "_?_:_";
    private const string AddFunction = "_+_";
    private const string IndexFunction = "_[_]";

    // The function of the special form of a block and the prefix of the name of a slot reference.
    private const string BlockFunction = "cel.@block";
    private const string SlotPrefix = "@index";

    // The index overloads of the standard library.
    private const string IndexListId = "index_list";
    private const string IndexMapId = "index_map";

    // The conversions of a string to a moment or a span of time, which the planner runs once for a string literal.
    private const string StringToTimestampId = "string_to_timestamp";
    private const string StringToDurationId = "string_to_duration";

    private readonly ParsedExpression _parsed;

    // The checked expression; null when planning without type checking.
    private readonly CheckedExpression? _checked;

    // The declarations of the environment: the overloads of a call without type checking, and the index overloads of an
    // operand of the static type dyn with it.
    private readonly DeclarationIndex _declarations;

    // The time zone database of the environment, which the plan keeps for the receivers of time with a zone; null when the
    // environment has none.
    private readonly CelTimeZoneDatabase? _timeZones;

    private readonly Dictionary<string, int> _variables = new(StringComparer.Ordinal);

    // The variables of the enclosing comprehensions with their local slots, the innermost last.
    private readonly List<(string Name, int Slot)> _locals = [];

    // The expressions the author wrote in the standard macros being planned.
    // Created at the first standard macro.
    private HashSet<ExprNode>? _authored;

    private int _localCount;

    // The blocks being planned that enclose the node being planned, the innermost last, and the totals that number the
    // cells and the blocks of the plan.
    private readonly List<BlockScope> _blocks = [];
    private int _slotCount;
    private int _blockCount;

    // Whether the node being planned was added by the expansion of a standard macro, and so costs nothing.
    private bool _added;

    // The accumulator slot of the map, filter or transformList whose step is being planned; -1 outside one.
    private int _appendSlot = -1;

    // The accumulator slot of the transformMap or transformMapEntry whose step is being planned; -1 outside one.
    private int _insertSlot = -1;

    private Planner(ParsedExpression parsed, CheckedExpression? checkedExpression, DeclarationIndex declarations, CelTimeZoneDatabase? timeZones)
    {
        _timeZones = timeZones;
        _parsed = parsed;
        _checked = checkedExpression;
        _declarations = declarations;
    }

    // Plans a checked expression against the declarations it was checked with: a call with one candidate of the
    // reference map calls it, a call with several chooses among them by Matches.
    internal static Plan Plan(CheckedExpression expression, DeclarationIndex declarations, CelTimeZoneDatabase? timeZones = null) =>
        new Planner(expression.Parsed, expression, declarations, timeZones).Build();

    // Plans a parsed expression without type checking: names resolve in the bindings at evaluation, and
    // every call other than the special forms chooses among all overloads of its name, call style and
    // argument count.
    internal static Plan PlanWithoutTypeCheck(ParsedExpression expression, DeclarationIndex declarations, CelTimeZoneDatabase? timeZones = null) =>
        new Planner(expression, null, declarations, timeZones).Build();

    private Plan Build()
    {
        PlanNode root = Visit(_parsed.Root, out int stackSize);
        return new Plan(root, _variables.Count, _localCount, stackSize, _parsed.Lines, _declarations.Types, _slotCount, _blockCount, _timeZones);
    }

    // The node of a syntax node, and the most argument values on the stack at one time while it is
    // evaluated.
    private PlanNode Visit(ExprNode node, out int stackSize)
    {
        int offset = _parsed.GetOffset(node.Id);
        stackSize = 0;

        // Planning recurses as deep as type checking did; a stack too short for it is the error of the node.
        if (!RuntimeHelpers.TryEnsureSufficientExecutionStack())
        {
            return new ConstantPlanNode(offset, EvaluationErrors.InsufficientStack(_parsed.Lines, offset));
        }

        if (_added && _authored!.Contains(node))
        {
            _added = false;
            PlanNode authored = VisitNode(node, offset, out stackSize);
            _added = true;
            return authored;
        }

        return VisitNode(node, offset, out stackSize);
    }

    private PlanNode VisitNode(ExprNode node, int offset, out int stackSize)
    {
        stackSize = 0;
        switch (node)
        {
            case ConstantNode constant:
                return new ConstantPlanNode(offset, constant.Value);
            case IdentNode identifier:
                return VisitIdentifier(identifier, offset, out stackSize);
            case SelectNode select:
                return VisitSelect(select, offset, out stackSize);
            case CallNode call:
                return VisitCall(call, offset, out stackSize);

            // The optional syntax is not supported yet; it is not taken for a map, a list or a message literal.
            case ListNode { OptionalIndices.IsEmpty: false }:
            case StructNode unsupported when unsupported.HasOptionalEntry():
                throw new NotImplementedException();
            case ListNode list:
                return new ListPlanNode(offset, VisitAll(list.Elements, out stackSize));
            case StructNode { MessageName.Length: > 0 } message:
                return VisitMessage(message, offset, out stackSize);
            case StructNode map:
                return VisitMap(map, offset, out stackSize);
            case ComprehensionNode comprehension:
                return VisitComprehension(comprehension, offset, out stackSize);
            default:
                throw new NotImplementedException();
        }
    }

    private PlanNode VisitIdentifier(IdentNode identifier, int offset, out int stackSize)
    {
        stackSize = 0;
        if (_blocks.Count > 0 && TryGetSlot(identifier.Name, out BlockScope? scope, out int number))
        {
            // The slot expression is evaluated on top of the values that the reading call has pushed: the stack that the
            // slots of the block need together. A read inside a slot expression is part of that sum already.
            stackSize = scope.InSlots ? 0 : scope.StackSize;
            return new SlotPlanNode(offset, _added ? 0UL : 1UL, scope.Slots, number, identifier.Name);
        }

        if (_checked is not null)
        {
            return VisitReference(_checked.References[identifier.Id], offset);
        }

        return identifier.Name[0] != '.' && TryGetLocal(identifier.Name, out int slot)
            ? Local(offset, slot, identifier.Name)
            : VisitName(identifier, [], offset);
    }

    // A name that type checking resolved: a variable of a comprehension, a declared variable, a type identifier, whose
    // type value costs 1 as a name read, or an enumeration constant, whose int value costs the same.
    private PlanNode VisitReference(Reference reference, int offset)
    {
        if (reference.Local is { } local)
        {
            _ = TryGetLocal(local, out int slot);
            return Local(offset, slot, local);
        }

        if (reference.Variable is { } variable)
        {
            return new VariablePlanNode(offset, VariableSlot(variable.Name), variable.Name, variable.Type, _declarations.Types);
        }

        if (reference.Constant is { } constant)
        {
            return new ConstantPlanNode(offset, CelValue.FromInt64(constant), 1);
        }

        return new ConstantPlanNode(offset, CelValue.FromType(reference.TypeValue!), 1);
    }

    // The read of a local: 1 unit, and nothing in the part that a macro adds or for the hidden name that sortBy binds its receiver to,
    // which no text can write and which is part of the expansion.
    private LocalPlanNode Local(int offset, int slot, string name) =>
        new(offset, slot, _added || string.Equals(name, ExtensionMacros.SortByInput, StringComparison.Ordinal) ? 0UL : 1UL);

    private PlanNode VisitSelect(SelectNode select, int offset, out int stackSize)
    {
        stackSize = 0;
        if (_checked is not null && _checked.References.TryGetValue(select.Id, out Reference? reference))
        {
            if (reference.ObjectType is { } objectType)
            {
                return new FieldPlanNode(offset, Visit(select.Operand, out stackSize), objectType, reference.FieldNumber, select.IsPresenceTest);
            }

            return VisitReference(reference, offset);
        }

        if (_checked is null && !select.IsPresenceTest && TryCollectChain(select, out IdentNode? root, out List<SelectNode>? chain))
        {
            return VisitName(root, chain, offset);
        }

        PlanNode operand = Visit(select.Operand, out stackSize);
        return new SelectPlanNode(offset, operand, select.Field, select.IsPresenceTest);
    }

    // A chain of selections that ends in an identifier that is not a variable of a comprehension, the
    // outermost selection first.
    private bool TryCollectChain(ExprNode node, [NotNullWhen(true)] out IdentNode? root, out List<SelectNode> chain)
    {
        chain = [];
        while (node is SelectNode { IsPresenceTest: false } select)
        {
            chain.Add(select);
            node = select.Operand;
        }

        root = node as IdentNode;
        return root is not null && (root.Name[0] == '.' || !TryGetLocal(root.Name, out _));
    }

    // A name without type checking: an identifier and the selections of its chain, the outermost first.
    // The candidates are the qualified names of the identifier and its first selections, the longest
    // first, each by the candidates of the container; the selections after a candidate select fields.
    private NamePlanNode VisitName(IdentNode root, List<SelectNode> chain, int offset)
    {
        bool leadingDot = root.Name[0] == '.';
        string first = leadingDot ? root.Name[1..] : root.Name;
        var prefixes = new string[chain.Count + 1];
        prefixes[0] = first;
        for (int i = 1; i <= chain.Count; i++)
        {
            prefixes[i] = prefixes[i - 1] + "." + chain[chain.Count - i].Field;
        }

        var candidates = new List<NamePlanNode.Candidate>();
        for (int selections = chain.Count; selections >= 0; selections--)
        {
            foreach (string name in Qualified(prefixes[selections], leadingDot))
            {
                CelType? typeValue = _declarations.TryGetTypeIdentifier(name, out CelType? type) ? type : null;
                CelType? objectType = _declarations.TryGetVariable(name, out CelVariableDecl? variable) && variable.Type.Kind == CelTypeKind.Object
                    ? variable.Type
                    : null;
                candidates.Add(new NamePlanNode.Candidate(name, selections, typeValue, objectType));
            }
        }

        var selects = new SelectPlanNode[chain.Count];
        for (int i = 0; i < chain.Count; i++)
        {
            SelectNode select = chain[chain.Count - 1 - i];
            selects[i] = new SelectPlanNode(_parsed.GetOffset(select.Id), null, select.Field, false);
        }

        return new NamePlanNode(offset, root.Name, [.. candidates], selects);
    }

    // The candidates of a name in the container, the longest first (language definition, "Name
    // Resolution"): in the container a.b the name x is a.b.x, a.x, then x; a name with a leading dot only
    // as written.
    private List<string> Qualified(string name, bool asWritten)
    {
        var names = new List<string>();
        string container = _declarations.Container;
        if (!asWritten)
        {
            int prefix = container.Length;
            while (prefix > 0)
            {
                names.Add(string.Concat(container.AsSpan(0, prefix), ".", name));
                prefix = Math.Max(container.AsSpan(0, prefix).LastIndexOf('.'), 0);
            }
        }

        names.Add(name);
        return names;
    }

    private PlanNode VisitCall(CallNode call, int offset, out int stackSize)
    {
        ImmutableArray<ExprNode> arguments = call.Arguments;
        if (call.Target is null)
        {
            switch (call.Function)
            {
                case AndPlanNode.Function when arguments.Length == 2:
                    return new AndPlanNode(offset, VisitAll(Operands(call), out stackSize));
                case OrPlanNode.Function when arguments.Length == 2:
                    return new OrPlanNode(offset, VisitAll(Operands(call), out stackSize));
                case ConditionalFunction when arguments.Length == 3:
                    return VisitConditional(arguments, offset, out stackSize);
                case NotStrictlyFalsePlanNode.Function when arguments.Length == 1:
                    return new NotStrictlyFalsePlanNode(offset, Visit(arguments[0], out stackSize));
                case AddFunction when _appendSlot >= 0 && IsAppend(arguments):
                    return new AppendPlanNode(offset, _appendSlot, Visit(((ListNode)arguments[1]).Elements[0], out stackSize));
                case MapInsertPlanNode.Function when _insertSlot >= 0 && IsInsert(arguments):
                    return VisitInsert(arguments, offset, out stackSize);
                default:
                    break;
            }
        }

        if (call.Target is null && arguments.Length == 2 && string.Equals(call.Function, BlockFunction, StringComparison.Ordinal))
        {
            return VisitBlock(call, offset, out stackSize);
        }

        return _checked is null ? VisitUncheckedCall(call, offset, out stackSize) : VisitCheckedCall(call, offset, out stackSize);
    }

    // cel.@block(slots, result) with a list literal of slots, which type checking requires: one node for each slot
    // expression and the node of the result, with a cell in the frame for each slot. A block of no slots is its result. A first
    // argument that is not such a literal can only come from a tree that was not checked: the call has no candidates, so it
    // is EvalNoMatchingOverload at the call after its arguments are evaluated.
    private PlanNode VisitBlock(CallNode call, int offset, out int stackSize)
    {
        if (call.Arguments[0] is not ListNode { OptionalIndices.IsEmpty: true } list)
        {
            return new DispatchPlanNode(offset, call.Function, [], [], !_added, VisitAll(call.Arguments, out stackSize));
        }

        if (list.Elements.Length == 0)
        {
            return Visit(call.Arguments[1], out stackSize);
        }

        var slots = new BlockSlots(_blockCount++, _slotCount, list.Elements.Length);
        _slotCount += slots.Count;
        var scope = new BlockScope(slots);
        _blocks.Add(scope);

        // A slot may read any slot of its block, so each is planned with the scope in place. The stack of a read of a slot of
        // this block is the sum of the stacks of its slot expressions: a chain of reads evaluates each slot at most once.
        scope.InSlots = true;
        for (int i = 0; i < slots.Count; i++)
        {
            slots.Expressions[i] = Visit(list.Elements[i], out int slotStack);
            scope.StackSize += slotStack;
        }

        scope.InSlots = false;
        PlanNode result = Visit(call.Arguments[1], out stackSize);
        _blocks.RemoveAt(_blocks.Count - 1);
        return new BlockPlanNode(offset, slots.Block, result);
    }

    // The slot that an identifier of the form @indexN reads: N is the digits without a sign or a leading zero, and the block is
    // the innermost enclosing one with more than N slots. Any other name stays an identifier.
    private bool TryGetSlot(string name, [NotNullWhen(true)] out BlockScope? scope, out int number)
    {
        scope = null;
        number = 0;
        if (!name.StartsWith(SlotPrefix, StringComparison.Ordinal) || name.Length == SlotPrefix.Length)
        {
            return false;
        }

        ReadOnlySpan<char> digits = name.AsSpan(SlotPrefix.Length);
        if (digits.Length > 9 || (digits[0] == '0' && digits.Length > 1))
        {
            return false;
        }

        foreach (char digit in digits)
        {
            if (digit is < '0' or > '9')
            {
                return false;
            }

            number = (number * 10) + (digit - '0');
        }

        for (int i = _blocks.Count - 1; i >= 0; i--)
        {
            if (number < _blocks[i].Slots.Count)
            {
                scope = _blocks[i];
                return true;
            }
        }

        return false;
    }

    private ConditionalPlanNode VisitConditional(ImmutableArray<ExprNode> arguments, int offset, out int stackSize)
    {
        PlanNode condition = Visit(arguments[0], out int conditionStack);
        PlanNode then = Visit(arguments[1], out int thenStack);
        PlanNode otherwise = Visit(arguments[2], out int elseStack);
        stackSize = Math.Max(conditionStack, Math.Max(thenStack, elseStack));
        return new ConditionalPlanNode(offset, condition, then, otherwise);
    }

    // The step @result + [e] that the expansion of map, filter and transformList writes.
    private static bool IsAppend(ImmutableArray<ExprNode> arguments) =>
        arguments.Length == 2
        && arguments[0] is IdentNode { Name: Macro.AccumulatorName }
        && arguments[1] is ListNode { Elements.Length: 1, OptionalIndices.IsEmpty: true };

    // The step cel.@mapInsert(@result, k, v) or cel.@mapInsert(@result, m) that the expansion of transformMap and transformMapEntry writes.
    private static bool IsInsert(ImmutableArray<ExprNode> arguments) =>
        arguments.Length is 2 or 3 && arguments[0] is IdentNode { Name: Macro.AccumulatorName };

    // The insertion into the map builder of the accumulator: the key, when the call has one, and the value or the map are planned;
    // the accumulator is read from its slot. The key, the value and the map are evaluated one after the other, none of them waits on
    // the argument stack.
    private MapInsertPlanNode VisitInsert(ImmutableArray<ExprNode> arguments, int offset, out int stackSize)
    {
        int keyStack = 0;
        PlanNode? key = arguments.Length == 3 ? Visit(arguments[1], out keyStack) : null;
        PlanNode operand = Visit(arguments[^1], out int operandStack);
        stackSize = Math.Max(keyStack, operandStack);
        return new MapInsertPlanNode(offset, _insertSlot, key, operand);
    }

    // A call with the candidates of the reference map. A call with a target whose candidates are global
    // overloads is the call of a qualified function name: the target is part of the name and is not
    // evaluated. Otherwise the target is the first argument. A _[_] whose operand has the static type dyn also
    // has the index overloads of the library that type checking did not keep.
    private PlanNode VisitCheckedCall(CallNode call, int offset, out int stackSize)
    {
        Reference reference = _checked!.References[call.Id];
        ImmutableArray<OverloadBinding> candidates = reference.Candidates;
        object? prepared = reference.Prepared;
        bool qualified = call.Target is null || (!candidates.IsEmpty && !candidates[0].Declaration.IsReceiverStyle);
        ImmutableArray<ExprNode> argumentNodes = qualified ? call.Arguments : [call.Target!, .. call.Arguments];
        ImmutableArray<PlanNode> arguments = VisitAll(argumentNodes, out stackSize);
        var argumentTypes = new CelType[argumentNodes.Length];
        for (int i = 0; i < argumentTypes.Length; i++)
        {
            argumentTypes[i] = _checked.Types[argumentNodes[i].Id];
        }

        if (call.Target is null && argumentTypes.Length == 2 && argumentTypes[0].Kind is CelTypeKind.Dyn or CelTypeKind.Any
            && string.Equals(call.Function, IndexFunction, StringComparison.Ordinal))
        {
            candidates = IndexCandidates(candidates);
        }

        if (candidates.Length == 1)
        {
            if (TryFoldTime(candidates[0], argumentNodes, offset) is { } folded)
            {
                stackSize = 0;
                return folded;
            }

            return new CallPlanNode(offset, candidates[0], Marks(candidates[0], argumentTypes), !_added, arguments, prepared, _declarations.PatternCache);
        }

        ImmutableArray<ImmutableArray<bool>>.Builder marks = ImmutableArray.CreateBuilder<ImmutableArray<bool>>(candidates.Length);
        foreach (OverloadBinding candidate in candidates)
        {
            marks.Add(Marks(candidate, argumentTypes));
        }

        return new DispatchPlanNode(offset, NameInErrors(call, candidates), candidates, marks.MoveToImmutable(), !_added, arguments, prepared, _declarations.PatternCache);
    }

    // The name that an error of a call names: the qualified name when the target of the call is part of it, math.sqrt and not sqrt.
    private static string NameInErrors(CallNode call, ImmutableArray<OverloadBinding> candidates) =>
        call.Target is not null && !candidates.IsEmpty && !candidates[0].Declaration.IsReceiverStyle ? candidates[0].Function : call.Function;

    // A call of timestamp or duration on a string literal, run once at planning on a call site with no limit and no
    // database: a value becomes a constant that charges the units of the call it replaces, the fixed units and the cost of the
    // literal, at the position of the call. An error value is not folded: the call stays and fails when it is evaluated. A call
    // that the expansion of a standard macro adds charges nothing.
    private ConstantPlanNode? TryFoldTime(OverloadBinding overload, ImmutableArray<ExprNode> argumentNodes, int offset)
    {
        if (overload.IsApplication
            || argumentNodes.Length != 1
            || argumentNodes[0] is not ConstantNode { Value: var literal }
            || !literal.TryGetString(out _)
            || overload.Declaration.OverloadId is not (StringToTimestampId or StringToDurationId))
        {
            return null;
        }

        ReadOnlySpan<CelValue> arguments = [literal];
        var site = new Declarations.CallSite(overload.Function, offset, _parsed.Lines);
        CelValue value = overload.Implementation!(arguments, in site);
        if (value.Kind == CelValueKind.Error)
        {
            return null;
        }

        ulong cost = _added ? 0 : overload.FixedCost + (overload.ArgumentCost?.Invoke(arguments) ?? 0);
        return new ConstantPlanNode(offset, value, cost);
    }

    // The candidates of _[_] on an operand of the static type dyn: those of the reference map, and index_list and
    // index_map of the library, in the order of the declarations. cel-go v0.32.0 indexes by the kind of the operand
    // at run time, so x[0u] with x bound to [7] is 7, though a uint key leaves type checking only index_map.
    private ImmutableArray<OverloadBinding> IndexCandidates(ImmutableArray<OverloadBinding> kept)
    {
        ImmutableArray<OverloadBinding> overloads = _declarations.GetOverloads(IndexFunction, 2);
        ImmutableArray<OverloadBinding>.Builder candidates = ImmutableArray.CreateBuilder<OverloadBinding>(overloads.Length);
        foreach (OverloadBinding overload in overloads)
        {
            if (kept.Contains(overload)
                || (!overload.IsApplication && overload.Declaration.OverloadId is IndexListId or IndexMapId))
            {
                candidates.Add(overload);
            }
        }

        return candidates.ToImmutable();
    }

    // A call without type checking: the overloads of the name, call style and argument count, each with
    // every argument marked. A target that is a chain of names naming a declared function with the name of
    // the call is part of that name; otherwise the target is the first argument.
    private DispatchPlanNode VisitUncheckedCall(CallNode call, int offset, out int stackSize)
    {
        string function = call.Function;
        bool receiverStyle = false;
        ImmutableArray<ExprNode> argumentNodes = call.Arguments;
        if (call.Target is { } target)
        {
            if (TryCollectChain(target, out IdentNode? root, out List<SelectNode>? chain)
                && TryResolveFunction(ChainName(root, chain) + "." + function, root.Name[0] == '.', out string? qualified))
            {
                function = qualified;
            }
            else
            {
                receiverStyle = true;
                argumentNodes = [target, .. call.Arguments];
            }
        }
        else
        {
            bool leadingDot = function[0] == '.';
            if (TryResolveFunction(leadingDot ? function[1..] : function, leadingDot, out string? resolved))
            {
                function = resolved;
            }
        }

        ImmutableArray<PlanNode> arguments = VisitAll(argumentNodes, out stackSize);
        ImmutableArray<OverloadBinding> candidates = _declarations.GetOverloads(function, receiverStyle, argumentNodes.Length);
        ImmutableArray<bool> all = AllMarked(argumentNodes.Length);
        ImmutableArray<ImmutableArray<bool>>.Builder marks = ImmutableArray.CreateBuilder<ImmutableArray<bool>>(candidates.Length);
        for (int i = 0; i < candidates.Length; i++)
        {
            marks.Add(all);
        }

        return new DispatchPlanNode(offset, call.Target is not null && !receiverStyle ? function : call.Function, candidates, marks.MoveToImmutable(), !_added, arguments, null, _declarations.PatternCache);
    }

    private static string ChainName(IdentNode root, List<SelectNode> chain)
    {
        string name = root.Name[0] == '.' ? root.Name[1..] : root.Name;
        for (int i = chain.Count - 1; i >= 0; i--)
        {
            name += "." + chain[i].Field;
        }

        return name;
    }

    private bool TryResolveFunction(string name, bool asWritten, out string function)
    {
        foreach (string candidate in Qualified(name, asWritten))
        {
            if (_declarations.TryGetFunction(candidate, out string? found))
            {
                function = found;
                return true;
            }
        }

        function = string.Empty;
        return false;
    }

    // The arguments that Matches checks at depth: for an overload of the application all of them, for an
    // overload of the library those whose static type does not cover the parameter type, and none for an overload that reads
    // the elements of its arguments itself (ShallowArguments).
    private static ImmutableArray<bool> Marks(OverloadBinding overload, CelType[] argumentTypes)
    {
        if (overload.ShallowArguments)
        {
            return ImmutableArray.Create(new bool[argumentTypes.Length]);
        }

        if (overload.IsApplication)
        {
            return AllMarked(argumentTypes.Length);
        }

        ImmutableArray<CelType> parameterTypes = overload.Declaration.ParameterTypes;
        var marks = new bool[argumentTypes.Length];
        for (int i = 0; i < marks.Length; i++)
        {
            marks[i] = !Covers(argumentTypes[i], parameterTypes[i]);
        }

        return ImmutableArray.Create(marks);
    }

    private static ImmutableArray<bool> AllMarked(int count)
    {
        var marks = new bool[count];
        Array.Fill(marks, true);
        return ImmutableArray.Create(marks);
    }

    // Whether every value of the static type fits the parameter type at the depth that Fits checks: dyn and
    // type parameters take anything; dyn and wrappers, at the top level or inside list and map, do not
    // guarantee another type. Other kinds are checked at the top level, which costs nothing.
    private static bool Covers(CelType argument, CelType parameter)
    {
        if (parameter.Kind is CelTypeKind.Dyn or CelTypeKind.Any or CelTypeKind.TypeParameter || argument.Equals(parameter))
        {
            return true;
        }

        return argument.Kind switch
        {
            CelTypeKind.Dyn or CelTypeKind.Any or CelTypeKind.Wrapper => false,
            CelTypeKind.List => parameter.Kind == CelTypeKind.List && Covers(argument.Parameters[0], parameter.Parameters[0]),
            CelTypeKind.Map => parameter.Kind == CelTypeKind.Map
                && Covers(argument.Parameters[0], parameter.Parameters[0])
                && Covers(argument.Parameters[1], parameter.Parameters[1]),
            _ => true,
        };
    }

    // The operands of a chain of _&&_ or of _||_ calls in the order of the text.
    private static ImmutableArray<ExprNode> Operands(CallNode call)
    {
        var operands = new List<ExprNode>();
        AddOperands(call, call.Function, operands);
        return [.. operands];
    }

    private static void AddOperands(CallNode call, string function, List<ExprNode> operands)
    {
        foreach (ExprNode argument in call.Arguments)
        {
            if (argument is CallNode { Target: null, Arguments.Length: 2 } inner && string.Equals(inner.Function, function, StringComparison.Ordinal))
            {
                AddOperands(inner, function, operands);
            }
            else
            {
                operands.Add(argument);
            }
        }
    }

    // A comprehension: the range and the initializer in the enclosing scopes, the loop condition and the
    // step with the accumulator and the iteration variable, the result with the accumulator only. In a
    // standard macro everything but the expressions the author wrote is planned as added.
    private ComprehensionPlanNode VisitComprehension(ComprehensionNode comprehension, int offset, out int stackSize)
    {
        MacroCall? macro = string.Equals(comprehension.AccuVar, Macro.AccumulatorName, StringComparison.Ordinal)
            ? _parsed.GetMacroCall(comprehension.Id)
            : null;
        bool appends = macro is not null && macro.Function is "map" or "filter" or "transformList";
        bool inserts = macro is not null && macro.Function is "transformMap" or "transformMapEntry";
        bool twoVariables = comprehension.IterVar2 is not null;

        // The builder of a list or of a map is the initializer: the literal [] or {} that the expansion writes is not planned.
        PlanNode range = Visit(comprehension.IterRange, out int rangeStack);
        int initializerStack = 0;
        PlanNode? initializer = appends || inserts ? null : Visit(comprehension.AccuInit, out initializerStack);
        int accumulator = _localCount++;
        int variable = _localCount++;
        int secondVariable = twoVariables ? _localCount++ : -1;

        bool added = _added;
        int appendSlot = _appendSlot;
        int insertSlot = _insertSlot;
        if (macro is not null)
        {
            // The arguments after the names of the variables are the expressions of the author.
            for (int i = twoVariables ? 2 : 1; i < macro.Arguments.Length; i++)
            {
                _ = (_authored ??= new(ReferenceEqualityComparer.Instance)).Add(macro.Arguments[i]);
            }

            _added = true;
        }

        _locals.Add((comprehension.AccuVar, accumulator));
        _locals.Add((comprehension.IterVar, variable));
        if (comprehension.IterVar2 is { } iterVar2)
        {
            _locals.Add((iterVar2, secondVariable));
        }

        PlanNode condition = Visit(comprehension.LoopCondition, out int conditionStack);
        _appendSlot = appends ? accumulator : -1;
        _insertSlot = inserts ? accumulator : -1;
        PlanNode step = Visit(comprehension.LoopStep, out int stepStack);
        _appendSlot = appendSlot;
        _insertSlot = insertSlot;
        _locals.RemoveAt(_locals.Count - 1);
        if (twoVariables)
        {
            _locals.RemoveAt(_locals.Count - 1);
        }

        PlanNode result = Visit(comprehension.Result, out int resultStack);
        _locals.RemoveAt(_locals.Count - 1);
        _added = added;

        stackSize = Math.Max(Math.Max(rangeStack, initializerStack), Math.Max(conditionStack, Math.Max(stepStack, resultStack)));
        return new ComprehensionPlanNode(offset, range, initializer, condition, step, result, variable, accumulator, secondVariable, inserts);
    }

    private MapPlanNode VisitMap(StructNode map, int offset, out int stackSize)
    {
        ImmutableArray<PlanNode>.Builder keys = ImmutableArray.CreateBuilder<PlanNode>(map.Entries.Length);
        ImmutableArray<PlanNode>.Builder values = ImmutableArray.CreateBuilder<PlanNode>(map.Entries.Length);
        stackSize = 0;
        foreach (StructEntry entry in map.Entries)
        {
            // The keys and the values before the entry stay on the stack, and then its key.
            int pushed = 2 * keys.Count;
            keys.Add(Visit(entry.Key, out int keyStackSize));
            values.Add(Visit(entry.Value, out int valueStackSize));
            stackSize = Math.Max(stackSize, Math.Max(pushed + keyStackSize, pushed + 1 + valueStackSize));
        }

        stackSize = Math.Max(stackSize, 2 * map.Entries.Length);

        return new MapPlanNode(offset, keys.MoveToImmutable(), values.MoveToImmutable());
    }

    // A message literal: the values stay on the stack while the next ones are evaluated. With type checking the description and
    // the field numbers are those of the reference map; without it the name of the type, with its candidates in the container,
    // and the field names resolve at evaluation.
    private MessagePlanNode VisitMessage(StructNode message, int offset, out int stackSize)
    {
        ImmutableArray<StructEntry> entries = message.Entries;
        ImmutableArray<PlanNode>.Builder values = ImmutableArray.CreateBuilder<PlanNode>(entries.Length);
        stackSize = entries.Length;
        for (int i = 0; i < entries.Length; i++)
        {
            values.Add(Visit(entries[i].Value, out int valueStackSize));
            stackSize = Math.Max(stackSize, i + valueStackSize);
        }

        if (_checked is not null)
        {
            ImmutableArray<int>.Builder numbers = ImmutableArray.CreateBuilder<int>(entries.Length);
            foreach (StructEntry entry in entries)
            {
                numbers.Add(_checked.References[entry.Id].FieldNumber);
            }

            return new MessagePlanNode(offset, _checked.References[message.Id].ObjectType!, numbers.MoveToImmutable(), values.MoveToImmutable());
        }

        bool leadingDot = message.MessageName[0] == '.';
        string name = leadingDot ? message.MessageName[1..] : message.MessageName;
        ImmutableArray<string>.Builder fieldNames = ImmutableArray.CreateBuilder<string>(entries.Length);
        foreach (StructEntry entry in entries)
        {
            fieldNames.Add(entry.FieldName!);
        }

        return new MessagePlanNode(
            offset,
            message.MessageName,
            [.. Qualified(name, leadingDot)],
            fieldNames.MoveToImmutable(),
            values.MoveToImmutable());
    }

    // The nodes of the arguments of a call or of the elements of a list, which stay on the stack while the
    // next ones are evaluated.
    private ImmutableArray<PlanNode> VisitAll(ImmutableArray<ExprNode> nodes, out int stackSize)
    {
        ImmutableArray<PlanNode>.Builder planned = ImmutableArray.CreateBuilder<PlanNode>(nodes.Length);
        stackSize = nodes.Length;
        for (int i = 0; i < nodes.Length; i++)
        {
            planned.Add(Visit(nodes[i], out int nodeStackSize));
            stackSize = Math.Max(stackSize, i + nodeStackSize);
        }

        return planned.MoveToImmutable();
    }

    private bool TryGetLocal(string name, out int slot)
    {
        for (int i = _locals.Count - 1; i >= 0; i--)
        {
            if (string.Equals(_locals[i].Name, name, StringComparison.Ordinal))
            {
                slot = _locals[i].Slot;
                return true;
            }
        }

        slot = -1;
        return false;
    }

    private int VariableSlot(string name)
    {
        int count = _variables.Count;
        ref int slot = ref CollectionsMarshal.GetValueRefOrAddDefault(_variables, name, out bool exists);
        if (!exists)
        {
            slot = count;
        }

        return slot;
    }

    // A block being planned: its slots, whether its slot expressions are being planned and the stack that the slot
    // expressions need together.
    private sealed class BlockScope(BlockSlots slots)
    {
        internal BlockSlots Slots { get; } = slots;

        internal bool InSlots { get; set; }

        internal int StackSize { get; set; }
    }
}
