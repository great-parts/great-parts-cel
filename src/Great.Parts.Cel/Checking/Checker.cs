using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Syntax;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Checking;

// Gradual type checking of a parsed expression against the declarations of an environment. The tree is
// visited depth first, the children of a node before the node, except that a comprehension adds the
// scopes of its variables. The type of a node is a CelType, or null for the type error: a node with an
// error is assignable to every type and produces no further errors, and checking goes on, so every check
// error of the text is returned, ordered by offset. Type parameters of overloads are replaced by fresh
// type variables at each call and bound in one substitution of the expression; the checked expression
// has the types after the substitution, with the variables still free replaced by dyn.
internal sealed class Checker
{
    private const string LogicalAnd = "_&&_";
    private const string LogicalOr = "_||_";
    private const string OptionalTypeName = "optional_type";

    // The call of the optional selection e.?f and the overload of the set optional that stands for it.
    private const string OptionalSelectFunction = "_?._";
    private const string OptionalSelectId = "select_optional_field";

    private readonly ParsedExpression _parsed;
    private readonly DeclarationIndex _declarations;

    // The expected type of the root; null for none, as dyn.
    private readonly CelType? _expectedType;

    private readonly TypeSubstitution _substitution = new();
    private readonly Dictionary<int, Reference> _references = [];

    // The types of the nodes by id, before the final substitution.
    private readonly List<(int Id, CelType Type)> _types = [];

    private readonly List<CelError> _errors = [];

    // The calls with a candidate that carries a validator, each with that binding, in the order of the traversal; and the types
    // of the nodes after the substitution, made once when the validators or the result need them.
    private readonly List<(CallNode Call, OverloadBinding Binding)> _validations = [];
    private Dictionary<int, CelType>? _finalTypes;

    // The variables of the enclosing comprehensions, the innermost last; a null type is the type error.
    private readonly List<(string Name, CelType? Type)> _locals = [];

    // The selections of the chain of names being resolved, the outermost first.
    private readonly List<SelectNode> _chain = [];

    // The candidates of the call being resolved and their result types.
    private readonly List<OverloadBinding> _candidates = [];
    private readonly List<CelType> _candidateResults = [];

    // The type parameters of the overload being tried, each with its fresh variable, and its instantiated
    // parameter types.
    private string[] _parameterNames = new string[4];
    private CelType[] _freshVariables = new CelType[4];
    private int _freshCount;
    private CelType[] _instantiated = new CelType[4];

    // A name and, in front of it, the room for the container, where the candidates of a name are composed.
    private char[] _names = [];

    private int _variableCount;

    // The node being checked, for InternalError; null before the root and after it.
    private ExprNode? _current;

    // Whether checking stopped at InsufficientStack.
    private bool _stopped;

    internal Checker(ParsedExpression parsed, DeclarationIndex declarations, CelType? expectedType)
    {
        _parsed = parsed;
        _declarations = declarations;
        _expectedType = expectedType;
    }

    internal static CheckResult Check(ParsedExpression parsed, DeclarationIndex declarations, CelType? expectedType = null) =>
        new Checker(parsed, declarations, expectedType).Run();

    // Checks the expression and, when it has no error, its type against the expected type.
    internal CheckResult Run()
    {
        CelType? type = Visit(_parsed.Root);
        if (_errors.Count == 0 && _validations.Count > 0)
        {
            RunValidators();
        }

        if (_errors.Count == 0 && type is not null)
        {
            CheckExpectedType(type);
        }

        return _errors.Count == 0 ? new CheckResult(Checked()) : Failed();
    }

    // The result of a check that an unexpected exception interrupted: the errors found before it and
    // InternalError at the node being checked, ordered by offset.
    internal CheckResult Interrupted(Exception exception)
    {
        _errors.Add(CheckErrors.InternalError(exception, Location(_current ?? _parsed.Root)));
        return Failed();
    }

    // The types of the nodes after the substitution. The bindings no longer change, so the result for each type variable
    // is kept: a chain of bindings, such as that of the elements of a list of empty lists, is walked once for all nodes.
    private CheckedExpression Checked() => new(_parsed, _references, FinalTypes());

    private Dictionary<int, CelType> FinalTypes()
    {
        if (_finalTypes is not null)
        {
            return _finalTypes;
        }

        var types = new Dictionary<int, CelType>(_types.Count);
        var kept = new Dictionary<string, StrongBox<CelType>>(StringComparer.Ordinal);
        foreach ((int id, CelType type) in _types)
        {
            types.Add(id, SubstitutionRules.Apply(_substitution, type, freeToDyn: true, kept));
        }

        _finalTypes = types;
        return types;
    }

    // The validators of the calls, in the order of the calls by offset, each on the shape of its call with the final types. A
    // rejected call keeps the type that its candidates gave it, since the traversal is over; the errors have the place of the
    // call or the offset that the answer names.
    private void RunValidators()
    {
        var keys = new long[_validations.Count];
        var order = new (CallNode Call, OverloadBinding Binding)[_validations.Count];
        for (int i = 0; i < order.Length; i++)
        {
            order[i] = _validations[i];
            keys[i] = ((long)_parsed.GetOffset(order[i].Call.Id) << 32) | (uint)i;
        }

        Array.Sort(keys, order);
        Dictionary<int, CelType> types = FinalTypes();
        foreach ((CallNode call, OverloadBinding binding) in order)
        {
            CallShape shape = ShapeOf(call, binding, types);
            CallValidation answer = binding.Validator!(shape);
            if (!answer.IsOk)
            {
                _errors.Add(ValidationError(call, answer, shape));
            }
        }
    }

    // The receiver, when the overload takes one, and the arguments of the call. A call of a qualified function name has no
    // receiver: its target is part of the name.
    private CallShape ShapeOf(CallNode call, OverloadBinding binding, Dictionary<int, CelType> types)
    {
        ValueShape? receiver = binding.Declaration.IsReceiverStyle ? ShapeOf(call.Target!, types) : null;
        ImmutableArray<ValueShape>.Builder arguments = ImmutableArray.CreateBuilder<ValueShape>(call.Arguments.Length);
        foreach (ExprNode argument in call.Arguments)
        {
            arguments.Add(ShapeOf(argument, types));
        }

        return new CallShape(receiver, arguments.MoveToImmutable());
    }

    // The offset, the final type and, for a literal, its value: a string, the elements of a list or the entries of a map.
    private ValueShape ShapeOf(ExprNode node, Dictionary<int, CelType> types)
    {
        int offset = _parsed.GetOffset(node.Id);
        CelType type = types.TryGetValue(node.Id, out CelType? known) ? known : CelType.Dyn;
        switch (node)
        {
            case ConstantNode constant when constant.Value.TryGetString(out string? text):
                return new ValueShape(offset, type, text, null, false, null);
            case ListNode list:
                ImmutableArray<ValueShape>.Builder elements = ImmutableArray.CreateBuilder<ValueShape>(list.Elements.Length);
                foreach (ExprNode element in list.Elements)
                {
                    elements.Add(ShapeOf(element, types));
                }

                return new ValueShape(offset, type, null, elements.MoveToImmutable(), !list.OptionalIndices.IsEmpty, null);
            case StructNode { MessageName.Length: 0 } map:
                ImmutableArray<(ValueShape Key, ValueShape Value)>.Builder entries =
                    ImmutableArray.CreateBuilder<(ValueShape Key, ValueShape Value)>(map.Entries.Length);
                foreach (StructEntry entry in map.Entries)
                {
                    entries.Add((ShapeOf(entry.Key, types), ShapeOf(entry.Value, types)));
                }

                return new ValueShape(offset, type, null, null, false, entries.MoveToImmutable());
            default:
                return new ValueShape(offset, type, null, null, false, null);
        }
    }

    // The error of a rejected call: the message of its code, at the part that the answer names or at the call.
    private CelError ValidationError(CallNode call, CallValidation answer, CallShape shape)
    {
        CelSourceLocation location = answer.Offset is int offset ? _parsed.Lines.GetLocation(offset) : Location(call);
        switch (answer.Code)
        {
            case CelErrorCode.InvalidFormatString:
                return CheckErrors.InvalidFormatString(answer.Detail, location);
            case CelErrorCode.NoMatchingOverload:
                int count = shape.Arguments.Length + (shape.Receiver is null ? 0 : 1);
                var argumentTypes = new CelType[count];
                int next = 0;
                if (shape.Receiver is { } receiver)
                {
                    argumentTypes[next++] = receiver.Type;
                }

                foreach (ValueShape argument in shape.Arguments)
                {
                    argumentTypes[next++] = argument.Type;
                }

                return CheckErrors.NoMatchingOverload(call.Function, argumentTypes, location);
            default:
                throw new UnreachableException("A validator answers InvalidFormatString or NoMatchingOverload.");
        }
    }

    // The errors ordered by offset; errors at one offset keep the order in which they were found.
    private CheckResult Failed()
    {
        var keys = new long[_errors.Count];
        var errors = new CelError[_errors.Count];
        for (int i = 0; i < errors.Length; i++)
        {
            errors[i] = _errors[i];
            keys[i] = ((long)errors[i].Location.Offset << 32) | (uint)i;
        }

        Array.Sort(keys, errors);
        return new CheckResult(ImmutableCollectionsMarshal.AsImmutableArray(errors));
    }

    private void CheckExpectedType(CelType type)
    {
        if (_expectedType is null || _expectedType.Kind == CelTypeKind.Dyn)
        {
            return;
        }

        _freshCount = 0;
        CelType expected = HasTypeParameters(_expectedType) ? Instantiate(_expectedType) : _expectedType;
        if (!AssignabilityRules.IsAssignable(type, expected, _substitution, []))
        {
            _errors.Add(CheckErrors.ExpectedTypeMismatch(Shown(type), _expectedType, Location(_parsed.Root)));
        }
    }

    private CelType? Visit(ExprNode node)
    {
        if (_stopped)
        {
            return null;
        }

        if (!RuntimeHelpers.TryEnsureSufficientExecutionStack())
        {
            _stopped = true;
            _errors.Add(CheckErrors.InsufficientStack(Location(node)));
            return null;
        }

        ExprNode? parent = _current;
        _current = node;
        CelType? type = node switch
        {
            ConstantNode constant => ConstantType(constant.Value.Kind),
            IdentNode identifier => VisitIdentifier(identifier),
            SelectNode select => VisitSelect(select),
            CallNode call => VisitCall(call),
            ListNode list => VisitList(list),
            StructNode { MessageName.Length: 0 } map => VisitMap(map),
            StructNode message => VisitMessage(message),
            ComprehensionNode comprehension => VisitComprehension(comprehension),
            _ => throw new UnreachableException("The parser creates no other node."),
        };
        _current = parent;
        Record(node, type);
        return type;
    }

    private void Record(ExprNode node, CelType? type)
    {
        if (type is not null)
        {
            _types.Add((node.Id, type));
        }
    }

    private static CelType ConstantType(CelValueKind kind) => kind switch
    {
        CelValueKind.Null => CelType.Null,
        CelValueKind.Bool => CelType.Bool,
        CelValueKind.Int => CelType.Int,
        CelValueKind.UInt => CelType.UInt,
        CelValueKind.Double => CelType.Double,
        CelValueKind.String => CelType.String,
        CelValueKind.Bytes => CelType.Bytes,
#if NET11_0_OR_GREATER
        CelValueKind.Decimal => CelType.Decimal,
#endif
        _ => throw new UnreachableException("A literal has no other kind."),
    };

    // A variable of an enclosing comprehension, unless the name has a leading dot; then the declarations by
    // the candidates of the container.
    private CelType? VisitIdentifier(IdentNode identifier)
    {
        string name = identifier.Name;
        bool leadingDot = name[0] == '.';
        if (!leadingDot && TryGetLocal(name, out CelType? localType))
        {
            _references.Add(identifier.Id, Reference.ToLocal(name));
            return localType;
        }

        ReadOnlySpan<char> written = leadingDot ? name.AsSpan(1) : name;
        written.CopyTo(NameRoom(written.Length));
        if (TryResolveName(written.Length, leadingDot, out Reference? reference, out CelType? type))
        {
            _references.Add(identifier.Id, reference);
            return type;
        }

        _errors.Add(CheckErrors.UndeclaredReference(name, _declarations.Container, Location(identifier)));
        return null;
    }

    private CelType? VisitSelect(SelectNode select)
    {
        if (!select.IsPresenceTest && TryCollectChain(select, out IdentNode? root))
        {
            return VisitChain(root);
        }

        return SelectField(select, Visit(select.Operand));
    }

    // A chain of selections that ends in an identifier, such as a.b.c: the longest prefix that names a
    // declaration wins, and the selections after it select fields. Nothing declared is UndeclaredReference
    // at the identifier.
    private CelType? VisitChain(IdentNode root)
    {
        int count = _chain.Count;
        int length = WriteChainName(root, null);
        bool leadingDot = root.Name[0] == '.';
        int resolved = count;
        CelType? type = null;
        while (true)
        {
            if (TryResolveName(length, leadingDot, out Reference? reference, out CelType? found))
            {
                _references.Add(ChainNode(root, resolved).Id, reference);
                type = found;
                break;
            }

            if (resolved == 0)
            {
                _errors.Add(CheckErrors.UndeclaredReference(root.Name, _declarations.Container, Location(root)));
                break;
            }

            length -= 1 + _chain[count - resolved].Field.Length;
            resolved--;
        }

        // The outermost selection is recorded by Visit.
        if (resolved < count)
        {
            Record(ChainNode(root, resolved), type);
        }

        for (int selects = resolved + 1; selects <= count; selects++)
        {
            SelectNode select = _chain[count - selects];
            type = SelectField(select, type);
            if (selects < count)
            {
                Record(select, type);
            }
        }

        return type;
    }

    // The node of the chain that the identifier and its first selections form: the identifier for none.
    private ExprNode ChainNode(IdentNode root, int selects) => selects == 0 ? root : _chain[_chain.Count - selects];

    // The selection of a field. An operand of the type optional_type(U) is optional: the rule goes on with the head of U and the
    // result is wrapped, as cel-go does (maybeUnwrapOptional). A presence test has the type bool, whatever the operand that has a type.
    private CelType? SelectField(SelectNode select, CelType? operandType)
    {
        if (operandType is null)
        {
            // A node with an error has the type error and produces no further errors, a presence test as well as a selection.
            return null;
        }

        CelType head = Head(operandType);
        bool optional = head.Kind == CelTypeKind.Optional;
        if (optional)
        {
            head = Head(head.Parameters[0]);
        }

        return SelectionType(select.Field, head, optional, select.IsPresenceTest, select, Location(select));
    }

    // The type of the selection of the field on an operand whose head is given, after an optional operand has been unwrapped once:
    // the value type of a map, the type of the field of an object type, dyn for dyn, Any, error or a type parameter, which is bound
    // to dyn; any other type has no fields and is NoSuchField at the place. The result of an optional selection is optional_type of
    // it, except for a presence test. The reference map records the field of an object only for a selection whose operand is not
    // optional: for an optional one the description of the value is known at evaluation, and the planner needs no reference to choose its node.
    private CelType? SelectionType(string field, CelType head, bool optional, bool isPresenceTest, SelectNode? select, CelSourceLocation location)
    {
        CelType result;
        switch (head.Kind)
        {
            case CelTypeKind.Map:
                result = head.Parameters[1];
                break;
            case CelTypeKind.Dyn:
                result = CelType.Dyn;
                break;
            case CelTypeKind.Any:
                // The value is the content that the Any holds, whose type only evaluation knows: as dyn.
                result = CelType.Dyn;
                break;
            case CelTypeKind.Object:
                CelType? fieldType = ObjectFieldType(field, head, optional, select, location);
                if (fieldType is null)
                {
                    return null;
                }

                result = fieldType;
                break;
            case CelTypeKind.TypeParameter:
                _ = AssignabilityRules.IsAssignable(CelType.Dyn, head, _substitution, []);
                result = CelType.Dyn;
                break;
            default:
                _errors.Add(CheckErrors.NoSuchField(field, Shown(head), location));
                return null;
        }

        return isPresenceTest ? CelType.Bool : optional ? OptionalType(result) : result;
    }

    // The type of a field of an object type, or null with the error added: no such field is NoSuchField with the full name of the
    // type, at the place, and a field that the description excluded from expressions is FieldNotAccessible with the reason it gives.
    private CelType? ObjectFieldType(string field, CelType head, bool optional, SelectNode? select, CelSourceLocation location)
    {
        if (!_declarations.Types.TryGetObjectType(head.Name, out CelObjectType? description)
            || !description.TryGetField(field, out CelObjectField? declared))
        {
            string? reason = description?.GetExcludedFieldReason(field);
            _errors.Add(reason is null
                ? CheckErrors.NoSuchField(field, head, location)
                : CheckErrors.FieldNotAccessible(field, head.Name, reason, location));
            return null;
        }

#if NET11_0_OR_GREATER
        // A field of the type decimal is usable only where the environment declares the type: the description is valid in
        // every environment, and the selection is where it is refused. The node has no type and no reference.
        if (RefusesDecimalField(description, declared, location))
        {
            return null;
        }
#endif

        if (select is not null && !optional)
        {
            _references.Add(select.Id, Reference.ToField(description, declared.Number));
        }

        return declared.Type;
    }

#if NET11_0_OR_GREATER
    // The reason of FieldNotAccessible for a field of the type decimal in an environment without the set decimal.
    private const string DecimalFieldReason = "the CEL type decimal needs the extension set 'decimal'";

    // A field whose type mentions decimal, in an environment that has no set decimal, is FieldNotAccessible at the place; true when it
    // was reported.
    private bool RefusesDecimalField(CelObjectType description, CelObjectField field, CelSourceLocation location)
    {
        if (_declarations.Types.DecimalDeclared || !MentionsDecimal(field.Type))
        {
            return false;
        }

        _errors.Add(CheckErrors.FieldNotAccessible(field.Name, description.Name, DecimalFieldReason, location));
        return true;
    }

    // Whether the type is decimal or has it at any depth: list(decimal), map(string, decimal), optional_type(decimal).
    private static bool MentionsDecimal(CelType type)
    {
        if (type.Kind == CelTypeKind.Decimal)
        {
            return true;
        }

        foreach (CelType parameter in type.Parameters)
        {
            if (MentionsDecimal(parameter))
            {
                return true;
            }
        }

        return false;
    }
#endif

    // optional_type(T).
    private static CelType OptionalType(CelType valueType) => new(CelTypeKind.Optional, OptionalTypeName, [valueType]);

    private CelType? VisitCall(CallNode call)
    {
        ExprNode? target = call.Target;
        if (target is null)
        {
            if (call.Function is LogicalAnd or LogicalOr && call.Arguments.Length == 2)
            {
                return VisitLogical(call);
            }

            return call.Function == OptionalSelectFunction && call.Arguments.Length == 2 && HasOptionalSelect()
                ? VisitOptionalSelect(call)
                : VisitGlobalCall(call);
        }

        // A target that is a chain of names can be part of a qualified function name: a.b.f(x) calls a.b.f.
        if (TryCollectChain(target, out IdentNode? root))
        {
            int length = WriteChainName(root, call.Function);
            if (TryResolveFunction(length, root.Name[0] == '.', out string? qualified))
            {
                return VisitArguments(call.Arguments, 0, out CelType[] types) ? Resolve(call, qualified, false, types) : null;
            }
        }

        CelType? targetType = Visit(target);
        bool argumentsChecked = VisitArguments(call.Arguments, 1, out CelType[] argumentTypes);
        if (targetType is null || !argumentsChecked)
        {
            return null;
        }

        argumentTypes[0] = targetType;
        call.Function.AsSpan().CopyTo(NameRoom(call.Function.Length));
        if (!TryResolveFunction(call.Function.Length, false, out string? function))
        {
            _errors.Add(CheckErrors.UndeclaredReference(call.Function, _declarations.Container, Location(call)));
            return null;
        }

        return Resolve(call, function, true, argumentTypes);
    }

    // Whether an overload of the set optional stands for the call _?._ in this environment.
    private bool HasOptionalSelect()
    {
        foreach (OverloadBinding overload in _declarations.GetOverloads(OptionalSelectFunction, false, 2))
        {
            if (!overload.IsApplication && string.Equals(overload.Declaration.OverloadId, OptionalSelectId, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    // The call _?._(e, 'f') that e.?f is: checked as any call, so its candidate, the one overload select_optional_field with its
    // validator, is recorded for the planner. The type is not the optional_type(V) of the declaration, which stands for the identifier
    // of the overload, but the selection rule on the head of e, wrapped, as cel-go types OptSelect. An operand that is optional is
    // unwrapped once. The name of the field is the string constant that the parser writes; a tree with another second argument
    // keeps the type of its candidates and is rejected by the validator after the traversal.
    private CelType? VisitOptionalSelect(CallNode call)
    {
        if (!VisitArguments(call.Arguments, 0, out CelType[] argumentTypes))
        {
            return null;
        }

        CelType? type = Resolve(call, OptionalSelectFunction, false, argumentTypes);
        if (type is null || call.Arguments[1] is not ConstantNode { Value: var name } || !name.TryGetString(out string? field))
        {
            return type;
        }

        CelType head = Head(argumentTypes[0]);
        if (head.Kind == CelTypeKind.Optional)
        {
            head = Head(head.Parameters[0]);
        }

        return SelectionType(field, head, true, false, null, Location(call));
    }

    private CelType? VisitGlobalCall(CallNode call)
    {
        if (!VisitArguments(call.Arguments, 0, out CelType[] argumentTypes))
        {
            return null;
        }

        string name = call.Function;
        bool leadingDot = name[0] == '.';
        ReadOnlySpan<char> written = leadingDot ? name.AsSpan(1) : name;
        written.CopyTo(NameRoom(written.Length));
        if (!TryResolveFunction(written.Length, leadingDot, out string? function))
        {
            _errors.Add(CheckErrors.UndeclaredReference(name, _declarations.Container, Location(call)));
            return null;
        }

        return Resolve(call, function, false, argumentTypes);
    }

    // && and ||: each operand must be assignable to bool, otherwise TypeMismatch at that operand, as cel-go
    // checks them.
    private CelType? VisitLogical(CallNode call)
    {
        ImmutableArray<ExprNode> operands = call.Arguments;
        CelType? left = Visit(operands[0]);
        CelType? right = Visit(operands[1]);
        bool assignable = Expect(operands[0], left, CelType.Bool) & Expect(operands[1], right, CelType.Bool);
        if (!assignable || left is null || right is null)
        {
            return null;
        }

        _references.Add(call.Id, Reference.ToCandidates(_declarations.GetOverloads(call.Function, false, 2)));
        return CelType.Bool;
    }

    // Checks every argument, so that each reports its errors; false when one has the type error. The types
    // start at the index first, after the room for a receiver.
    private bool VisitArguments(ImmutableArray<ExprNode> arguments, int first, out CelType[] types)
    {
        int count = first + arguments.Length;
        types = count == 0 ? [] : new CelType[count];
        bool checkedAll = true;
        for (int i = 0; i < arguments.Length; i++)
        {
            CelType? type = Visit(arguments[i]);
            checkedAll &= type is not null;
            types[first + i] = type ?? CelType.Dyn;
        }

        return checkedAll;
    }

    // The overloads of the function that accept the argument types, in the order of the index; each
    // successful trial keeps its bindings. The type is the result type of the first candidate after the
    // substitution, or dyn when the candidates differ.
    private CelType? Resolve(CallNode call, string function, bool isReceiverStyle, CelType[] argumentTypes)
    {
        ImmutableArray<OverloadBinding> overloads = _declarations.GetOverloads(function, isReceiverStyle, argumentTypes.Length);
        _candidates.Clear();
        _candidateResults.Clear();
        foreach (OverloadBinding overload in overloads)
        {
            if (overload.DynOnly && !HasDynArgument(argumentTypes))
            {
                continue;
            }

            int mark = _substitution.Mark;
            if (TryOverload(overload.Declaration, argumentTypes, out CelType? result))
            {
                _candidates.Add(overload);
                _candidateResults.Add(result);
            }
            else
            {
                _substitution.Rollback(mark);
            }
        }

        if (_candidates.Count == 0)
        {
            for (int i = 0; i < argumentTypes.Length; i++)
            {
                argumentTypes[i] = Shown(argumentTypes[i]);
            }

            _errors.Add(CheckErrors.NoMatchingOverload(function, argumentTypes, Location(call)));
            return null;
        }

        ImmutableArray<OverloadBinding> kept = _candidates.Count == overloads.Length ? overloads : [.. _candidates];
        object? prepared = PrepareConstant(call, kept, isReceiverStyle);
        _references.Add(call.Id, Reference.ToCandidates(kept, prepared));
        foreach (OverloadBinding candidate in _candidates)
        {
            if (candidate.Validator is not null)
            {
                _validations.Add((call, candidate));
            }
        }

        CelType type = SubstitutionRules.Apply(_substitution, _candidateResults[0], freeToDyn: false);
        for (int i = 1; i < _candidateResults.Count; i++)
        {
            if (!SubstitutionRules.Apply(_substitution, _candidateResults[i], freeToDyn: false).Equals(type))
            {
                return CelType.Dyn;
            }
        }

        return type;
    }

    // The preparation of the constant argument of a call that type checking accepted: when every candidate names the same argument
    // and the node there is a string literal, the preparer of the overload takes the value of the literal, and an error is
    // InvalidRegex at the literal; the object it returns is kept in the reference of the call. Nothing else is a constant: not a
    // variable, a call such as dyn('('), a concatenation or an element of a list, and nothing is folded, so the class of the outcome
    // depends on the form of the text alone. A candidate of the application has no constant argument, so a call it can take is
    // prepared by none.
    private object? PrepareConstant(CallNode call, ImmutableArray<OverloadBinding> candidates, bool isReceiverStyle)
    {
        if (candidates.IsEmpty || candidates[0].ConstantArgument is not { } constant)
        {
            return null;
        }

        for (int i = 1; i < candidates.Length; i++)
        {
            if (candidates[i].ConstantArgument is not { } other || other.Index != constant.Index)
            {
                return null;
            }
        }

        // The receiver counts as the argument 0; a call of a qualified function name has no receiver.
        ExprNode? node = isReceiverStyle ? (constant.Index == 0 ? call.Target : call.Arguments[constant.Index - 1]) : call.Arguments[constant.Index];
        if (node is not ConstantNode { Value: var value } || !value.TryGetString(out string? text))
        {
            return null;
        }

        ConstantPreparation preparation = constant.Prepare(text, _declarations.PatternCache);
        if (preparation.Error is { } error)
        {
            _errors.Add(CheckErrors.InvalidRegex(error, Location(node)));
            return null;
        }

        return preparation.Prepared;
    }

    // Tries the argument types, the receiver first, against the parameter types of the overload, its type
    // parameters replaced by fresh variables, which are the only ones a trial inside a type value may bind.
    private bool TryOverload(CelOverloadDecl overload, CelType[] argumentTypes, [NotNullWhen(true)] out CelType? result)
    {
        ImmutableArray<CelType> parameterTypes = overload.ParameterTypes;
        _freshCount = 0;
        if (!HasTypeParameters(overload))
        {
            for (int i = 0; i < argumentTypes.Length; i++)
            {
                if (!AssignabilityRules.IsAssignable(argumentTypes[i], parameterTypes[i], _substitution, []))
                {
                    result = null;
                    return false;
                }
            }

            result = overload.ResultType;
            return true;
        }

        if (_instantiated.Length < parameterTypes.Length)
        {
            _instantiated = new CelType[parameterTypes.Length];
        }

        for (int i = 0; i < parameterTypes.Length; i++)
        {
            _instantiated[i] = Instantiate(parameterTypes[i]);
        }

        result = Instantiate(overload.ResultType);
        for (int i = 0; i < argumentTypes.Length; i++)
        {
            if (!AssignabilityRules.IsAssignable(argumentTypes[i], _instantiated[i], _substitution, _freshVariables.AsSpan(0, _freshCount)))
            {
                result = null;
                return false;
            }
        }

        return true;
    }

    // Whether an argument has the static type dyn after the substitution.
    private bool HasDynArgument(CelType[] argumentTypes)
    {
        foreach (CelType argumentType in argumentTypes)
        {
            if (Head(argumentType).Kind is CelTypeKind.Dyn or CelTypeKind.Any)
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasTypeParameters(CelOverloadDecl overload)
    {
        foreach (CelType parameterType in overload.ParameterTypes)
        {
            if (HasTypeParameters(parameterType))
            {
                return true;
            }
        }

        return HasTypeParameters(overload.ResultType);
    }

    private static bool HasTypeParameters(CelType type)
    {
        if (type.Kind == CelTypeKind.TypeParameter)
        {
            return true;
        }

        foreach (CelType parameter in type.Parameters)
        {
            if (HasTypeParameters(parameter))
            {
                return true;
            }
        }

        return false;
    }

    // The type with each type parameter replaced by its fresh variable, created at its first occurrence.
    private CelType Instantiate(CelType type)
    {
        if (type.Kind == CelTypeKind.TypeParameter)
        {
            return FreshVariableFor(type.Name);
        }

        ImmutableArray<CelType> parameters = type.Parameters;
        if (parameters.IsEmpty || !HasTypeParameters(type))
        {
            return type;
        }

        ImmutableArray<CelType>.Builder instantiated = ImmutableArray.CreateBuilder<CelType>(parameters.Length);
        foreach (CelType parameter in parameters)
        {
            instantiated.Add(Instantiate(parameter));
        }

        return new CelType(type.Kind, type.Name, instantiated.MoveToImmutable());
    }

    private CelType FreshVariableFor(string parameterName)
    {
        for (int i = 0; i < _freshCount; i++)
        {
            if (string.Equals(_parameterNames[i], parameterName, StringComparison.Ordinal))
            {
                return _freshVariables[i];
            }
        }

        if (_freshCount == _freshVariables.Length)
        {
            Array.Resize(ref _parameterNames, 2 * _freshCount);
            Array.Resize(ref _freshVariables, 2 * _freshCount);
        }

        CelType variable = NewVariable();
        _parameterNames[_freshCount] = parameterName;
        _freshVariables[_freshCount++] = variable;
        return variable;
    }

    // A type variable of this expression: _var0, _var1, and so on.
    private CelType NewVariable() => CelType.TypeParameter("_var" + (_variableCount++).ToString(CultureInfo.InvariantCulture));

    // The join of the element types; an empty list has a fresh element type.
    private CelType? VisitList(ListNode list)
    {
        ImmutableArray<ExprNode> elements = list.Elements;
        ImmutableArray<int> optionalIndices = list.OptionalIndices;
        int nextOptional = 0;
        CelType? joined = null;
        bool hasError = false;
        for (int i = 0; i < elements.Length; i++)
        {
            CelType? type = Visit(elements[i]);
            if (nextOptional < optionalIndices.Length && optionalIndices[nextOptional] == i)
            {
                nextOptional++;
                type = OptionalValue(elements[i], type);
            }

            hasError |= type is null;
            joined = Join(joined, type);
        }

        return hasError ? null : CelType.ListOf(joined ?? NewVariable());
    }

    // The joins of the key types and of the value types; the key types are not checked.
    private CelType? VisitMap(StructNode map)
    {
        CelType? keys = null;
        CelType? values = null;
        bool hasError = false;
        foreach (StructEntry entry in map.Entries)
        {
            CelType? keyType = Visit(entry.Key);
            CelType? valueType = Visit(entry.Value);
            if (entry.IsOptional)
            {
                valueType = OptionalValue(entry.Value, valueType);
            }

            hasError |= keyType is null || valueType is null;
            keys = Join(keys, keyType);
            values = Join(values, valueType);
        }

        return hasError ? null : CelType.MapOf(keys ?? NewVariable(), values ?? NewVariable());
    }

    private CelType? Join(CelType? joined, CelType? next) =>
        next is null ? joined : joined is null ? next : AssignabilityRules.Join(joined, next, _substitution);

    // The value of an optional element or entry: the value type of an optional_type, or dyn; any other type
    // is TypeMismatch, as cel-go checks it.
    private CelType? OptionalValue(ExprNode node, CelType? type)
    {
        if (type is null)
        {
            return null;
        }

        CelType head = Head(type);
        if (head.Kind == CelTypeKind.Optional)
        {
            return head.Parameters[0];
        }

        if (head.Kind == CelTypeKind.Dyn)
        {
            return head;
        }

        CelType shown = Shown(head);
        _errors.Add(CheckErrors.TypeMismatch(new CelType(CelTypeKind.Optional, OptionalTypeName, [shown]), shown, Location(node)));
        return null;
    }

    // A message literal: the field values are checked in order, whatever the literal gives; then its name resolves by
    // the container with the order of the declaration index. No candidate is UndeclaredReference; a variable, a
    // constant or a type identifier that is not the name of a described type is NotAType with the resolved name; a
    // described type that is not constructible is TypeNotConstructible, its field names not looked up. All at the literal,
    // whose type is the type error. A constructible description gets the checks of its fields. The candidate is looked up
    // in the type index, not among the type identifiers, since the type of the description of a well-known message
    // is not an object type.
    private CelType? VisitMessage(StructNode message)
    {
        ImmutableArray<StructEntry> entries = message.Entries;
        CelType?[] valueTypes = entries.IsEmpty ? [] : new CelType?[entries.Length];
        for (int i = 0; i < valueTypes.Length; i++)
        {
            valueTypes[i] = Visit(entries[i].Value);
        }

        string name = message.MessageName;
        bool leadingDot = name[0] == '.';
        ReadOnlySpan<char> written = leadingDot ? name.AsSpan(1) : name;
        written.CopyTo(NameRoom(written.Length));
        var candidates = new NameCandidates(_names, _declarations.Container, written.Length, leadingDot);
        while (candidates.MoveNext(out ReadOnlySpan<char> candidate))
        {
            bool variable = _declarations.TryGetVariable(candidate, out _);
            if (!variable && _declarations.Types.TryGetObjectType(candidate, out CelObjectType? description))
            {
                if (description.IsConstructible)
                {
                    return CheckMessageFields(message, description, valueTypes);
                }

                _errors.Add(CheckErrors.TypeNotConstructible(description.Name, Location(message)));
                return null;
            }

            if (variable || _declarations.TryGetTypeIdentifier(candidate, out _) || _declarations.Types.TryGetEnumConstant(candidate, out _))
            {
                _errors.Add(CheckErrors.NotAType(candidate.ToString(), Location(message)));
                return null;
            }
        }

        _errors.Add(CheckErrors.UndeclaredReference(name, _declarations.Container, Location(message)));
        return null;
    }

    // The fields of a literal of a constructible description, each at the colon of its entry: no such field is NoSuchField, or
    // FieldNotAccessible when the description gives a reason; a value whose type is not assignable to the type of its field is
    // TypeMismatch. The checks go on after an error. The reference map records the description for the literal and the number
    // of the field for each entry whose field exists, which the planner binds. The type of the literal is the type of the
    // description, also when an entry has an error, so the errors of the rest of the expression are not lost.
    private CelType CheckMessageFields(StructNode message, CelObjectType description, CelType?[] valueTypes)
    {
        _references.Add(message.Id, Reference.ToMessage(description));
        for (int i = 0; i < valueTypes.Length; i++)
        {
            StructEntry entry = message.Entries[i];
            string field = entry.FieldName!;
            CelSourceLocation location = _parsed.Lines.GetLocation(_parsed.GetOffset(entry.Id));
            if (!description.TryGetField(field, out CelObjectField? declared))
            {
                string? reason = description.GetExcludedFieldReason(field);
                _errors.Add(reason is null
                    ? CheckErrors.NoSuchField(field, description.Name, location)
                    : CheckErrors.FieldNotAccessible(field, description.Name, reason, location));
                continue;
            }

            _references.Add(entry.Id, Reference.ToField(description, declared.Number));
            if (valueTypes[i] is not { } valueType)
            {
                continue;
            }

            // The value of an entry with ? must be assignable to optional_type of the field: an optional of a type assignable to it
            // and dyn pass. A null is no optional, so it is a mismatch whatever the legacy rule of assignment says.
            CelType expected = entry.IsOptional ? OptionalType(declared.Type) : declared.Type;
            int mark = _substitution.Mark;
            if ((entry.IsOptional && Head(valueType).Kind == CelTypeKind.Null)
                || !AssignabilityRules.IsAssignable(valueType, expected, _substitution, []))
            {
                _substitution.Rollback(mark);
                _errors.Add(CheckErrors.TypeMismatch(Shown(expected), Shown(valueType), location));
            }
        }

        return description.Type;
    }

    // The range and the accumulator initializer in the enclosing scopes; the loop condition and the step
    // with the accumulator and the iteration variable; the result with the accumulator only. The type is
    // that of the result, also when the range is invalid. A comprehension of two variables declares the second variable after
    // the first, both before the loop condition and the step.
    private CelType? VisitComprehension(ComprehensionNode comprehension)
    {
        CelType? rangeType = Visit(comprehension.IterRange);
        bool twoVariables = comprehension.IterVar2 is not null;
        (CelType? variableType, CelType? secondType) = IterationTypes(comprehension.IterRange, rangeType, twoVariables);
        CelType? accumulatorType = Visit(comprehension.AccuInit);
        _locals.Add((comprehension.AccuVar, accumulatorType));
        _locals.Add((comprehension.IterVar, variableType));
        if (comprehension.IterVar2 is { } iterVar2)
        {
            _locals.Add((iterVar2, secondType));
        }

        _ = Expect(comprehension.LoopCondition, Visit(comprehension.LoopCondition), CelType.Bool);
        _ = Expect(comprehension.LoopStep, Visit(comprehension.LoopStep), accumulatorType);
        _locals.RemoveAt(_locals.Count - 1);
        if (twoVariables)
        {
            _locals.RemoveAt(_locals.Count - 1);
        }

        CelType? result = Visit(comprehension.Result);
        _locals.RemoveAt(_locals.Count - 1);
        return result;
    }

    // The types of the iteration variables. One variable: the element type of a list, the key type of a map. Two: the index, an int,
    // and the element of a list; the key and the value of a map. Dyn for dyn, error or a type parameter, which is bound to dyn;
    // another type is InvalidComprehensionRange and the type error for both. The second type is meaningful for two variables only.
    private (CelType? First, CelType? Second) IterationTypes(ExprNode range, CelType? rangeType, bool twoVariables)
    {
        if (rangeType is null)
        {
            return (CelType.Dyn, CelType.Dyn);
        }

        CelType head = Head(rangeType);
        switch (head.Kind)
        {
            case CelTypeKind.List:
                return twoVariables ? (CelType.Int, head.Parameters[0]) : (head.Parameters[0], null);
            case CelTypeKind.Map:
                return (head.Parameters[0], twoVariables ? head.Parameters[1] : null);
            case CelTypeKind.Dyn:
                return (head, head);
            case CelTypeKind.Any:
                return (CelType.Dyn, CelType.Dyn);
            case CelTypeKind.TypeParameter:
                _ = AssignabilityRules.IsAssignable(CelType.Dyn, head, _substitution, []);
                return (CelType.Dyn, CelType.Dyn);
            default:
                _errors.Add(CheckErrors.InvalidComprehensionRange(Shown(head), Location(range)));
                return (null, null);
        }
    }

    // Whether a part is assignable to the type its construct requires; otherwise TypeMismatch at the part,
    // and the bindings of the failed trial are undone. The type error passes on either side.
    private bool Expect(ExprNode node, CelType? actual, CelType? expected)
    {
        if (actual is null || expected is null)
        {
            return true;
        }

        int mark = _substitution.Mark;
        if (AssignabilityRules.IsAssignable(actual, expected, _substitution, []))
        {
            return true;
        }

        _substitution.Rollback(mark);
        _errors.Add(CheckErrors.TypeMismatch(Shown(expected), Shown(actual), Location(node)));
        return false;
    }

    // Collects the selections of a chain of names that starts at the node and ends in an identifier that is
    // not a variable of a comprehension: such a chain can be a qualified name.
    private bool TryCollectChain(ExprNode node, [NotNullWhen(true)] out IdentNode? root)
    {
        _chain.Clear();
        while (node is SelectNode { IsPresenceTest: false } select)
        {
            _chain.Add(select);
            node = select.Operand;
        }

        root = node as IdentNode;
        return root is not null && (root.Name[0] == '.' || !TryGetLocal(root.Name, out _));
    }

    // Writes the name of the identifier and the collected selections, then the last name if there is one,
    // without a leading dot, into the name buffer; returns the length.
    private int WriteChainName(IdentNode root, string? last)
    {
        ReadOnlySpan<char> first = root.Name[0] == '.' ? root.Name.AsSpan(1) : root.Name;
        int length = first.Length + (last is null ? 0 : 1 + last.Length);
        foreach (SelectNode select in _chain)
        {
            length += 1 + select.Field.Length;
        }

        Span<char> name = NameRoom(length);
        first.CopyTo(name);
        int position = first.Length;
        for (int i = _chain.Count - 1; i >= -1; i--)
        {
            string? segment = i >= 0 ? _chain[i].Field : last;
            if (segment is not null)
            {
                name[position++] = '.';
                segment.AsSpan().CopyTo(name[position..]);
                position += segment.Length;
            }
        }

        return length;
    }

    private bool TryGetLocal(string name, out CelType? type)
    {
        for (int i = _locals.Count - 1; i >= 0; i--)
        {
            if (string.Equals(_locals[i].Name, name, StringComparison.Ordinal))
            {
                type = _locals[i].Type;
                return true;
            }
        }

        type = null;
        return false;
    }

    // The room in the name buffer for a name of the length, after the room for the container.
    private Span<char> NameRoom(int length)
    {
        int start = _declarations.Container.Length + 1;
        if (_names.Length < start + length)
        {
            Array.Resize(ref _names, Math.Max(start + length, 2 * _names.Length));
        }

        return _names.AsSpan(start, length);
    }

    // The variable, the type identifier or the enumeration constant that the name in the name buffer refers to, by the
    // candidates of the container; for each candidate in this order, as cel-go v0.32.0 Env.LookupIdent does.
    private bool TryResolveName(int length, bool asWritten, [NotNullWhen(true)] out Reference? reference, [NotNullWhen(true)] out CelType? type)
    {
        var candidates = new NameCandidates(_names, _declarations.Container, length, asWritten);
        while (candidates.MoveNext(out ReadOnlySpan<char> candidate))
        {
            if (_declarations.TryGetVariable(candidate, out CelVariableDecl? variable))
            {
                reference = Reference.ToVariable(variable);
                type = variable.Type;
                return true;
            }

            if (_declarations.TryGetTypeIdentifier(candidate, out CelType? typeValue))
            {
                reference = Reference.ToTypeIdentifier(typeValue);
                type = CelType.TypeOf(typeValue);
                return true;
            }

            if (_declarations.Types.TryGetEnumConstant(candidate, out long constant))
            {
                reference = Reference.ToConstant(constant);
                type = CelType.Int;
                return true;
            }
        }

        reference = null;
        type = null;
        return false;
    }

    // The declared function that the name in the name buffer refers to, by the candidates of the container.
    private bool TryResolveFunction(int length, bool asWritten, [NotNullWhen(true)] out string? function)
    {
        var candidates = new NameCandidates(_names, _declarations.Container, length, asWritten);
        while (candidates.MoveNext(out ReadOnlySpan<char> candidate))
        {
            if (_declarations.TryGetFunction(candidate, out function))
            {
                return true;
            }
        }

        function = null;
        return false;
    }

    // The type itself, or the binding of a bound type parameter, at the top level.
    private CelType Head(CelType type)
    {
        while (type.Kind == CelTypeKind.TypeParameter && _substitution.TryGet(type.Name, out CelType? bound))
        {
            type = bound;
        }

        return type;
    }

    // The type as messages show it: after the substitution, free variables as dyn.
    private CelType Shown(CelType type) => SubstitutionRules.Apply(_substitution, type, freeToDyn: true);

    private CelSourceLocation Location(ExprNode node) => _parsed.Lines.GetLocation(_parsed.GetOffset(node.Id));

    // The candidates of a name in the container, the longest first (language definition, "Name
    // Resolution"): in the container a.b the name x is a.b.x, a.x, then x; a name with a leading dot only
    // as written. The name stands in the buffer after the room for the container, and each candidate is
    // composed in front of it, so no string is created.
    private ref struct NameCandidates
    {
        private readonly Span<char> _buffer;
        private readonly ReadOnlySpan<char> _container;
        private readonly int _length;

        // The length of the prefix of the container in the next candidate; -1 after the last.
        private int _prefix;

        internal NameCandidates(char[] buffer, string container, int length, bool asWritten)
        {
            _buffer = buffer;
            _container = container;
            _length = length;
            _prefix = asWritten ? 0 : container.Length;
        }

        internal bool MoveNext(out ReadOnlySpan<char> candidate)
        {
            int nameStart = _container.Length + 1;
            if (_prefix <= 0)
            {
                candidate = _prefix == 0 ? _buffer.Slice(nameStart, _length) : default;
                bool hasNext = _prefix == 0;
                _prefix = -1;
                return hasNext;
            }

            ReadOnlySpan<char> prefix = _container[.._prefix];
            int start = nameStart - _prefix - 1;
            prefix.CopyTo(_buffer[start..]);
            _buffer[nameStart - 1] = '.';
            candidate = _buffer.Slice(start, _prefix + 1 + _length);
            _prefix = Math.Max(prefix.LastIndexOf('.'), 0);
            return true;
        }
    }
}
