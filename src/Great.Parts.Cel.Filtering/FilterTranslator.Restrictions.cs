using System;
using System.Collections.Generic;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Syntax;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Filtering;

// The translation of a comparison of a field or a function with a value, and of a value without a field: the path of the field, the
// guards of the path, the value by the type of the field, the stars of a string, the null.
internal sealed partial class FilterTranslator
{
    // A comparison: with a value, or with a composite in parentheses, which is a comparison with each of its values.
    private CelExprNode Restriction(FilterRestriction restriction) =>
        restriction.Right switch
        {
            FilterGroup group => Composite(restriction, group.Inner),
            FilterCall call => FunctionValue(restriction, call),
            _ => Comparison(restriction, (FilterMember)restriction.Right),
        };

    // The composite on the right of a comparison: its values are compared with the field, the logical operators stay.
    private CelExprNode Composite(FilterRestriction head, FilterExpr composite)
    {
        switch (composite)
        {
            case FilterMember member:
                return Comparison(head, member);
            case FilterGroup group:
                return Composite(head, group.Inner);
            case FilterCall call:
                return FunctionValue(head, call);
            case FilterNot not:
                CelExprNode operand = Composite(head, not.Operand);
                _factory.Position = not.MarkStart..not.MarkEnd;
                return _factory.Call("!_", operand);
            case FilterJoin join:
                List<CelExprNode> terms = new(join.Operands.Count);
                foreach (FilterExpr operandOfJoin in join.Operands)
                {
                    terms.Add(Composite(head, operandOfJoin));
                }

                return Balance(join.Kind == FilterJoinKind.Or ? "_||_" : "_&&_", terms, new JoinPlaces(join));
            default:
                Report(
                    CelErrorCode.InvalidFilterRestriction,
                    "A composite on the right of a comparison holds values and the logical operators only.",
                    composite.Start,
                    composite.End,
                    Source(head.Left));
                return Placeholder();
        }
    }

    // A function on the right of a comparison: the right side takes values and logical operators only.
    private CelConstantNode FunctionValue(FilterRestriction head, FilterCall call)
    {
        Report(
            CelErrorCode.InvalidFilterRestriction,
            "The function " + FunctionName(call) + " cannot be the value of a comparison: only values and logical operators are allowed on the right.",
            call.Name[0].Start,
            call.Name[^1].End,
            Source(head.Left));
        return Placeholder();
    }

    // One comparison of the left side of a restriction with a value.
    private CelExprNode Comparison(FilterRestriction head, FilterMember value)
    {
        if (head.Left is FilterCall call)
        {
            return CallComparison(head, call, value);
        }

        FilterMember left = (FilterMember)head.Left;
        FieldRef? field = FieldOf(left);
        if (field is null)
        {
            return Placeholder();
        }

        return head.Comparator == FilterComparator.Has ? Has(head, field, value) : Compare(head, head.Comparator, field, value);
    }

    // A comparison of a field with a value by the type of the field.
    private CelExprNode Compare(FilterRestriction head, FilterComparator op, FieldRef field, FilterMember value)
    {
        CelType? declared = field.Path.FinalType;
        string? enumType = _catalog.EnumTypeOf(field.Path);
        bool absence = IsNull(value) && AllowsAbsence(declared);
        FilterWildcard wildcard = FilterValues.Wildcard(value);
        if (!IsComparable(head, field, op, absence, enumType is not null)
            || (wildcard.Kind != FilterWildcardKind.None && !WildcardAllowed(op, declared, Source(field.Member), value)))
        {
            return Placeholder();
        }

        int last = field.Path.Names.Count - 1;
        List<CelExprNode> terms = [];
        if (absence || wildcard.Kind == FilterWildcardKind.Presence)
        {
            Guards(field, last, includeLast: false, lastContinues: false, terms);
            AddPresence(field, absence ? op == FilterComparator.Equal : op == FilterComparator.NotEqual, withSize: false, terms);
        }
        else
        {
            Guards(field, last, includeLast: true, lastContinues: false, terms);
            CelExprNode target = PathExpression(field, last + 1, wholePath: false);
            CelExprNode? test = ValueTest(head, op, target, new ValueSpec(declared, enumType, Source(field.Member)), value, wildcard);
            if (test is null)
            {
                return Placeholder();
            }

            terms.Add(test);
        }

        return Balance("_&&_", terms, new JoinPlaces(field.Member.Start, value.End));
    }

    // A comparison of the result of a function with a value by the result type that the environment gives the function.
    private CelExprNode CallComparison(FilterRestriction head, FilterCall call, FilterMember value)
    {
        string subject = _filter[call.Start..call.End];
        CelType? declared = _catalog.ResultTypeOf(FunctionName(call), call.Arguments.Count);
        bool absence = IsNull(value) && AllowsAbsence(declared);
        FilterWildcard wildcard = FilterValues.Wildcard(value);
        if (head.Comparator == FilterComparator.Has)
        {
            Restricted(declared, subject, head, "the has operator needs a field, not a function");
            return Placeholder();
        }

        string? reason = Reason(Unwrapped(declared), head.Comparator, absence, isEnum: false);
        if (reason is not null)
        {
            Restricted(Unwrapped(declared), subject, head, reason);
            return Placeholder();
        }

        if (wildcard.Kind != FilterWildcardKind.None && !WildcardAllowed(head.Comparator, declared, subject, value))
        {
            return Placeholder();
        }

        CelExprNode target = FunctionCall(call);
        if (absence || wildcard.Kind == FilterWildcardKind.Presence)
        {
            bool equal = absence ? head.Comparator == FilterComparator.Equal : head.Comparator == FilterComparator.NotEqual;
            _factory.Position = value.Start..value.End;
            CelExprNode none = _factory.Constant(CelValue.Null);
            _factory.Position = head.ComparatorStart..head.ComparatorEnd;
            return _factory.Call(equal ? "_==_" : "_!=_", target, none);
        }

        return ValueTest(head, head.Comparator, target, new ValueSpec(declared, null, subject), value, wildcard) ?? Placeholder();
    }

    // The test of a target against a value that is neither null nor a lone star: a test of the pattern of a string, or the comparison
    // with the constant. Null after the error InvalidFilterValue.
    private CelExprNode? ValueTest(FilterRestriction head, FilterComparator op, CelExprNode target, ValueSpec spec, FilterMember value, FilterWildcard wildcard)
    {
        if (wildcard.Kind != FilterWildcardKind.None)
        {
            _factory.Position = value.Start..value.End;
            CelExprNode core = _factory.Constant(CelValue.FromString(wildcard.Core));
            string function = wildcard.Kind switch
            {
                FilterWildcardKind.StartsWith => "startsWith",
                FilterWildcardKind.EndsWith => "endsWith",
                _ => "contains",
            };
            _factory.Position = head.ComparatorStart..head.ComparatorEnd;
            CelExprNode pattern = _factory.MemberCall(function, target, core);
            return op == FilterComparator.NotEqual ? _factory.Call("!_", pattern) : pattern;
        }

        CelExprNode? literal = ValueNode(Unwrapped(spec.Declared), spec.EnumType, value, spec.Subject, spec.Declared);
        if (literal is null)
        {
            return null;
        }

        _factory.Position = head.ComparatorStart..head.ComparatorEnd;
        return _factory.Call(Operator(op), target, literal);
    }

    // The value of a restriction as a node by the type of what it is compared with, or null after InvalidFilterValue.
    private CelExprNode? ValueNode(CelType? type, string? enumType, FilterMember value, string subject, CelType? declared)
    {
        FilterValue read = FilterValues.Read(type, value, _catalog, enumType);
        if (read.Problem is { } problem)
        {
            Report(
                CelErrorCode.InvalidFilterValue,
                "The value " + _filter[value.Start..value.End] + " does not fit " + subject + ": " + problem + ".",
                value.Start,
                value.End,
                subject,
                declared);
            return null;
        }

        _factory.Position = value.Start..value.End;
        return read.Function is { } function
            ? _factory.Call(function, _factory.Constant(CelValue.FromString(read.Text!)))
            : _factory.Constant(read.Value!.Value);
    }

    // A star that stands for any text belongs to a string, and to the comparisons = and != only.
    private bool WildcardAllowed(FilterComparator op, CelType? declared, string subject, FilterMember value)
    {
        if (Unwrapped(declared) is { Kind: CelTypeKind.String } && op is FilterComparator.Equal or FilterComparator.NotEqual)
        {
            return true;
        }

        Report(
            CelErrorCode.InvalidFilterValue,
            "The value " + _filter[value.Start..value.End] + " has a wildcard '*', which is for a string with = and != only.",
            value.Start,
            value.End,
            subject,
            declared);
        return false;
    }

    // A value without a field: it is searched in the search fields, as a substring.
    private CelExprNode Global(FilterMember member)
    {
        if (_catalog.SearchPaths.IsEmpty)
        {
            Report(
                CelErrorCode.InvalidFilterRestriction,
                "The value " + member.Source + " has no field and the filter has no search fields.",
                member.Start,
                member.End);
            return Placeholder();
        }

        string text = member.Segments.Count == 1 && member.Segments[0].Quoted is { } quoted ? quoted.Value : member.Source;
        List<CelExprNode> alternatives = new(_catalog.SearchPaths.Length);
        foreach (ResolvedPath path in _catalog.SearchPaths)
        {
            FieldRef? field = Prepare(member, path, new PathPlace(member, isWritten: false));
            if (field is null)
            {
                return Placeholder();
            }

            List<CelExprNode> terms = [];
            Guards(field, path.Names.Count - 1, includeLast: true, lastContinues: false, terms);
            CelExprNode target = PathExpression(field, path.Names.Count, wholePath: false);
            _factory.Position = member.Start..member.End;
            CelExprNode literal = _factory.Constant(CelValue.FromString(text));
            terms.Add(_factory.MemberCall("contains", target, literal));
            alternatives.Add(Balance("_&&_", terms, new JoinPlaces(member.Start, member.End)));
        }

        return Balance("_||_", alternatives, new JoinPlaces(member.Start, member.End));
    }

    // Whether the comparator is allowed for the field: not through a repeated field, not for a list, a map or an object, not an
    // order of a bool or of an enumeration. Reports the restriction when it is not.
    private bool IsComparable(FilterRestriction head, FieldRef field, FilterComparator op, bool absence, bool isEnum)
    {
        ResolvedPath path = field.Path;
        int list = path.ListStep();
        if (list >= 0)
        {
            return Restricted(path.TypeAfter(list), Source(field.Member), head, "a repeated field in the path can only be used with ':'");
        }

        CelType? type = Unwrapped(path.FinalType);
        string? reason = Reason(type, op, absence, isEnum);
        return reason is null || Restricted(type, Source(field.Member), head, reason);
    }

    // Why a comparator does not fit a type; null when it does.
    private static string? Reason(CelType? type, FilterComparator op, bool absence, bool isEnum)
    {
        bool order = op is FilterComparator.Less or FilterComparator.LessOrEqual or FilterComparator.Greater or FilterComparator.GreaterOrEqual;
        if (absence)
        {
            return order ? "null has no order" : null;
        }

        if (!IsComparableType(type))
        {
            return "a value of the type " + type + " cannot be compared";
        }

        if (order && type is { Kind: CelTypeKind.Bool })
        {
            return "a value of the type bool has no order";
        }

        return order && isEnum ? "an enumeration has no order" : null;
    }

    private bool Restricted(CelType? type, string subject, FilterRestriction head, string reason)
    {
        Report(
            CelErrorCode.InvalidFilterRestriction,
            "The comparator " + _filter[head.ComparatorStart..head.ComparatorEnd] + " is not allowed for " + subject + ": " + reason + ".",
            head.ComparatorStart,
            head.ComparatorEnd,
            subject,
            actualType: type);
        return false;
    }

    // Whether values of the type can be written in a filter: a primitive, a time, an enumeration, a dynamic value, a type the
    // environment does not describe. A list, a map, an object and the other types cannot.
    private static bool IsComparableType(CelType? type) =>
        type is null
        || type.Kind is CelTypeKind.Dyn or CelTypeKind.Any or CelTypeKind.Bool or CelTypeKind.Int or CelTypeKind.UInt or CelTypeKind.Double
            or CelTypeKind.Decimal or CelTypeKind.String or CelTypeKind.Bytes or CelTypeKind.Timestamp or CelTypeKind.Duration;

    // The word null written without quotes: the absence of a value.
    private static bool IsNull(FilterMember value) =>
        value.Segments.Count == 1 && value.Segments[0].Quoted is null && string.Equals(value.Source, "null", StringComparison.Ordinal);

    // A wrapper, an object, a dynamic value and a type that the environment does not describe can be absent; the others are values.
    private static bool AllowsAbsence(CelType? type) =>
        type is null || type.Kind is CelTypeKind.Wrapper or CelTypeKind.Object or CelTypeKind.Dyn or CelTypeKind.Any;

    private string Source(FilterExpr node) => _filter[node.Start..node.End];

    private static CelType? Unwrapped(CelType? type) => type is { Kind: CelTypeKind.Wrapper } wrapper ? wrapper.Parameters[0] : type;

    private static string[] Names(FilterMember member)
    {
        string[] names = new string[member.Segments.Count];
        for (int i = 0; i < names.Length; i++)
        {
            names[i] = member.Segments[i].Text;
        }

        return names;
    }

    private static string Operator(FilterComparator comparator) =>
        comparator switch
        {
            FilterComparator.Equal => "_==_",
            FilterComparator.NotEqual => "_!=_",
            FilterComparator.Less => "_<_",
            FilterComparator.LessOrEqual => "_<=_",
            FilterComparator.Greater => "_>_",
            _ => "_>=_",
        };

    // What a value is compared with: its declared type, its enumeration type and the text for the errors.
    private readonly record struct ValueSpec(CelType? Declared, string? EnumType, string Subject);
}
