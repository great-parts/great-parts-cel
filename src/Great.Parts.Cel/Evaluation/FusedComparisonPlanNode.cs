using System;
using System.Collections.Immutable;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Evaluation;

// The read of a field and its comparison with a literal as one node, v.f op c: v is a variable, a field of the input of the environment
// or the variable of a macro, f is read by FieldPlanNode or SelectPlanNode, op is ==, !=, <, <=, > or >=, and c is a literal of the kind
// int, uint, double, string, bool or null (null only with == and !=). No other form is fused, and a plan for partial evaluation is not.
//
// The node is the call node and the field node together, so it gives what they give: the same value, the same error with the same place,
// and the same units charged in the same order. It evaluates the field node first, as the call evaluates its first argument; an error
// value of the field is the result, and an unknown value is the result after the literal is evaluated, as a call returns it. Then it
// charges and calls as the call node does, on the two values held on the stack of the method, not on the stack of the frame, and:
// - when the value and the literal are of the same scalar kind, or the literal is null, it computes the answer itself, after the
//   same single charge that the call makes (the fixed cost of the overload of the ordering, 1 of the equality of the value rules),
//   so that nothing is looked up among the candidates and no implementation is called;
// - when the call is a dispatch and the value is of the kind of the literal, it calls the candidate that the dispatch would choose,
//   which planning found by the kind alone, without the search among the candidates;
// - otherwise it hands the two values to the call node, which checks and calls as it always does.
internal sealed class FusedComparisonPlanNode : PlanNode
{
    private const string EqualsId = "equals";
    private const string NotEqualsId = "not_equals";

    private readonly PlanNode _field;
    private readonly PlanNode _literalNode;
    private readonly CelValue _literal;
    private readonly Relation _relation;

    // The call of the comparison: one candidate, or the dispatch among several.
    private readonly CallPlanNode? _call;
    private readonly DispatchPlanNode? _dispatch;

    // The candidate of the dispatch for values of the kind of the literal; null when the node has no dispatch or none fits.
    private readonly OverloadBinding? _chosen;

    // What the node computes itself, the units it charges first, and the literal as the number that it is compared with.
    private readonly Shortcut _shortcut;
    private readonly ulong _units;
    private readonly long _integer;
    private readonly ulong _unsigned;
    private readonly double _real;
    private readonly bool _flag;

    private FusedComparisonPlanNode(
        PlanNode call,
        PlanNode field,
        PlanNode literalNode,
        CelValue literal,
        Relation relation,
        OverloadBinding? chosen,
        Shortcut shortcut,
        ulong units)
        : base(call.Offset, call.Cost)
    {
        _call = call as CallPlanNode;
        _dispatch = call as DispatchPlanNode;
        _field = field;
        _literalNode = literalNode;
        _literal = literal;
        _relation = relation;
        _chosen = chosen;
        _shortcut = shortcut;
        _units = units;
        _ = literal.TryGetInt64(out _integer);
        _ = literal.TryGetUInt64(out _unsigned);
        _ = literal.TryGetDouble(out _real);
        _ = literal.TryGetBoolean(out _flag);
    }

    private enum Relation
    {
        Equal,
        NotEqual,
        Less,
        LessEquals,
        Greater,
        GreaterEquals,
    }

    private enum Shortcut
    {
        // The call node or the chosen candidate does the comparison.
        None,

        // The literal is null and the relation is == or !=: the answer is whether the value is null, after the unit of the equality.
        NullEquality,

        // The literal is a number or a bool and the value is of its kind: the answer is the comparison of the two, after the units of the call.
        Scalar,
    }

    // The fused node for the call, which has the nodes of its two arguments; null when the call is not of the form v.f op c, or its
    // candidates are not those of the library that the node computes as the call does. Types is the type index of the environment.
    internal static FusedComparisonPlanNode? TryCreate(string function, PlanNode call, ImmutableArray<PlanNode> arguments, CelValue literal, TypeIndex types)
    {
        if (!IsOfTheForm(function, arguments, literal, out Relation relation))
        {
            return null;
        }

        return call switch
        {
            CallPlanNode { Overload.IsApplication: false } single => ForCall(single, arguments, literal, relation, types),
            DispatchPlanNode { Candidates.IsEmpty: false } dispatch => ForDispatch(dispatch, arguments, literal, relation, types),
            _ => null,
        };
    }

    // A call with one candidate of the library: the candidate is the equality for == and !=, an ordering for the others, and it fits two
    // values of the kind of the literal, so that the node computes as the call does.
    private static FusedComparisonPlanNode? ForCall(CallPlanNode call, ImmutableArray<PlanNode> arguments, CelValue literal, Relation relation, TypeIndex types)
    {
        OverloadBinding overload = call.Overload;
        ReadOnlySpan<CelValue> pair = [literal, literal];
        if ((relation <= Relation.NotEqual) != IsEquality(overload) || !overload.Matches(pair, [], null, types).IsFit)
        {
            return null;
        }

        (Shortcut shortcut, ulong units) = ChooseShortcut(relation, literal.Kind, overload);
        return new FusedComparisonPlanNode(call, arguments[0], arguments[1], literal, relation, null, shortcut, units);
    }

    // A dispatch of an ordering among candidates of the library, one of which fits two values of the kind of the literal.
    private static FusedComparisonPlanNode? ForDispatch(DispatchPlanNode dispatch, ImmutableArray<PlanNode> arguments, CelValue literal, Relation relation, TypeIndex types)
    {
        ReadOnlySpan<CelValue> pair = [literal, literal];
        if (relation <= Relation.NotEqual || HasApplicationCandidate(dispatch) || dispatch.Choose(pair, types) is not { } chosen)
        {
            return null;
        }

        (Shortcut shortcut, ulong units) = ChooseShortcut(relation, literal.Kind, chosen);
        return new FusedComparisonPlanNode(dispatch, arguments[0], arguments[1], literal, relation, chosen, shortcut, units);
    }

    // Whether the call has the form v.f op c: an operator of the closed list, the read of a field of a variable on the left, and on the
    // right a literal of the kinds that are fused, null only with == and !=.
    private static bool IsOfTheForm(string function, ImmutableArray<PlanNode> arguments, CelValue literal, out Relation relation) =>
        TryGetRelation(function, out relation)
        && arguments.Length == 2
        && IsFieldOfVariable(arguments[0])
        && arguments[1] is ConstantPlanNode
        && IsLiteralKind(literal.Kind)
        && (literal.Kind != CelValueKind.Null || relation <= Relation.NotEqual);

    internal override CelValue Evaluate(Frame frame)
    {
        if (!HasStack(frame, out CelValue error))
        {
            return error;
        }

        CelValue left = _field.Evaluate(frame);
        if (left.Kind == CelValueKind.Error)
        {
            // An error value ends the call at once. An unknown value does not: the literal is evaluated, which costs nothing and records
            // its offset, and then the unknown value is the result.
            if (left.IsUnknown)
            {
                _ = _literalNode.Evaluate(frame);
            }

            return left;
        }

        // The cost is charged after the checks, by the call.
        frame.Offset = Offset;
        return Compare(frame, left);
    }

    private CelValue Compare(Frame frame, CelValue left)
    {
        if (_shortcut == Shortcut.NullEquality)
        {
            Charge(frame);
            return CelValue.FromBoolean((left.Kind == CelValueKind.Null) == (_relation == Relation.Equal));
        }

        bool sameKind = left.Kind == _literal.Kind;
        if (_shortcut == Shortcut.Scalar && sameKind)
        {
            Charge(frame);
            return CelValue.FromBoolean(HoldsFor(left));
        }

        ReadOnlySpan<CelValue> values = [left, _literal];
        if (_dispatch is null)
        {
            return _call!.Complete(frame, values);
        }

        return _chosen is not null && sameKind ? _dispatch.Invoke(frame, _chosen, values) : _dispatch.Call(frame, values);
    }

    private void Charge(Frame frame)
    {
        if (frame.Metered && _units != 0)
        {
            frame.Charge(_units);
        }
    }

    // Whether the relation holds between the value and the literal, which are of one scalar kind: the comparison of the number line of the
    // value rules, in which a NaN is unordered, so every relation but != is false.
    private bool HoldsFor(CelValue left)
    {
        switch (_literal.Kind)
        {
            case CelValueKind.Int:
                _ = left.TryGetInt64(out long integer);
                return Holds(integer.CompareTo(_integer));
            case CelValueKind.UInt:
                _ = left.TryGetUInt64(out ulong unsigned);
                return Holds(unsigned.CompareTo(_unsigned));
            case CelValueKind.Double:
                _ = left.TryGetDouble(out double real);
                return Holds(real);
            default:
                _ = left.TryGetBoolean(out bool flag);
                return Holds(flag.CompareTo(_flag));
        }
    }

    private bool Holds(double real) => _relation switch
    {
        Relation.Equal => real == _real,
        Relation.NotEqual => real != _real,
        Relation.Less => real < _real,
        Relation.LessEquals => real <= _real,
        Relation.Greater => real > _real,
        _ => real >= _real,
    };

    private bool Holds(int comparison) => _relation switch
    {
        Relation.Equal => comparison == 0,
        Relation.NotEqual => comparison != 0,
        Relation.Less => comparison < 0,
        Relation.LessEquals => comparison <= 0,
        Relation.Greater => comparison > 0,
        _ => comparison >= 0,
    };

    // What the node computes itself for a candidate, and the units that the call charges before the comparison: the equality of the value
    // rules charges 1 for two scalars or for a null, and an overload of ordering its fixed cost, with no cost of its arguments for a number
    // or a bool; a string is left to the candidate, whose cost depends on the lengths.
    private static (Shortcut Kind, ulong Units) ChooseShortcut(Relation relation, CelValueKind literal, OverloadBinding candidate)
    {
        if (relation <= Relation.NotEqual)
        {
            return literal switch
            {
                CelValueKind.Null => (Shortcut.NullEquality, 1UL),
                CelValueKind.String => (Shortcut.None, 0UL),
                _ => (Shortcut.Scalar, 1UL),
            };
        }

        if (literal == CelValueKind.String || candidate.ArgumentCost is not null)
        {
            return (Shortcut.None, 0UL);
        }

        return (Shortcut.Scalar, candidate.FixedCost);
    }

    private static bool IsEquality(OverloadBinding overload) =>
        overload.Declaration.OverloadId is EqualsId or NotEqualsId;

    private static bool HasApplicationCandidate(DispatchPlanNode dispatch)
    {
        foreach (OverloadBinding candidate in dispatch.Candidates)
        {
            if (candidate.IsApplication)
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryGetRelation(string function, out Relation relation)
    {
        switch (function)
        {
            case "_==_":
                relation = Relation.Equal;
                return true;
            case "_!=_":
                relation = Relation.NotEqual;
                return true;
            case "_<_":
                relation = Relation.Less;
                return true;
            case "_<=_":
                relation = Relation.LessEquals;
                return true;
            case "_>_":
                relation = Relation.Greater;
                return true;
            case "_>=_":
                relation = Relation.GreaterEquals;
                return true;
            default:
                relation = default;
                return false;
        }
    }

    private static bool IsLiteralKind(CelValueKind kind) =>
        kind is CelValueKind.Int or CelValueKind.UInt or CelValueKind.Double or CelValueKind.String or CelValueKind.Bool or CelValueKind.Null;

    // The read of a field of a variable: a selection that is not a presence test, on the read of a variable of the environment, a field of
    // the input or the variable of a macro.
    private static bool IsFieldOfVariable(PlanNode node) => node switch
    {
        FieldPlanNode { IsPresenceTest: false } field => IsVariable(field.Operand),
        SelectPlanNode { IsPresenceTest: false, Operand: { } operand } => IsVariable(operand),
        _ => false,
    };

    private static bool IsVariable(PlanNode node) => node is VariablePlanNode or InputFieldPlanNode or LocalPlanNode;
}
