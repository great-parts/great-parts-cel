using System.Collections.Generic;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Syntax;
using Great.Parts.Cel.Types;

namespace Great.Parts.Cel.Filtering;

// The has operator ':': a lone star is the presence of the field, a value is looked up by the type of the field - in a list, in a map,
// as a field of an object, or as a scalar compared with '=' - and a repeated field on the path is walked with a loop.
internal sealed partial class FilterTranslator
{
    // field:value
    private CelExprNode Has(FilterRestriction head, FieldRef field, FilterMember value)
    {
        int list = field.Path.ListStep();
        if (list >= 0)
        {
            return InElements(head, field, list, value);
        }

        if (IsStar(value))
        {
            List<CelExprNode> terms = [];
            Guards(field, field.Path.Names.Count - 1, includeLast: false, lastContinues: false, terms);
            AddPresence(field, negated: false, withSize: true, terms);
            return Balance("_&&_", terms, new JoinPlaces(field.Member.Start, value.End));
        }

        CelType? type = field.Path.FinalType;
        switch (type?.Kind)
        {
            case CelTypeKind.List:
            case CelTypeKind.Map:
                return HasIn(head, field, value);
            case CelTypeKind.Object:
                return HasMember(field, value);
            case CelTypeKind.Dyn:
            case CelTypeKind.Any:
            case null:
                Restricted(type, Source(field.Member), head, "the has operator with a value needs a field of a known type");
                return Placeholder();
            default:
                return Compare(head, FilterComparator.Equal, field, value);
        }
    }

    // A lone star, written in quotes or not.
    private static bool IsStar(FilterMember value) =>
        FilterValues.Wildcard(value).Kind == FilterWildcardKind.Presence
        || (value.Segments.Count == 1 && value.Segments[0].Quoted is null && value.Source == "*");

    // A repeated field before the end of the path: some element of the list has the rest of the path with the value.
    private CelExprNode InElements(FilterRestriction head, FieldRef field, int list, FilterMember value)
    {
        List<CelExprNode> terms = [];
        Guards(field, list, includeLast: true, lastContinues: true, terms);
        string variable = LoopVariable();
        _loops.Add(variable);
        CelExprNode body = Has(head, field.Element(list, variable), value);
        _loops.RemoveAt(_loops.Count - 1);
        terms.Add(Exists(field, list, variable, body));
        return Balance("_&&_", terms, new JoinPlaces(field.Member.Start, value.End));
    }

    // The loop over the list that ends at a part of the path: items.exists(variable, body).
    private CelExprNode Exists(FieldRef field, int list, string variable, CelExprNode body)
    {
        CelExprNode range = PathExpression(field, list + 1, wholePath: false);
        _factory.Position = field.Place.Part(list);
        CelExprNode iteration = _factory.Ident(variable);
        _factory.Position = field.Place.Whole;
        return _factory.ExpandMacro("exists", range, iteration, body);
    }

    // The value is an element of the list, or a key of the map. A star in a string is a pattern that some element matches.
    private CelExprNode HasIn(FilterRestriction head, FieldRef field, FilterMember value)
    {
        CelType container = field.Path.FinalType!;
        CelType? member = Unwrapped(container.Parameters[0]);
        string subject = Source(field.Member);
        if (!IsComparableType(member))
        {
            Restricted(container, subject, head, "a value cannot be looked up in a " + container);
            return Placeholder();
        }

        // A map has no elements to match a pattern: its star is a value that does not fit, as the star of a field that is not a string.
        FilterWildcard wildcard = FilterValues.Wildcard(value);
        if (wildcard.Kind != FilterWildcardKind.None
            && !WildcardAllowed(FilterComparator.Equal, container.Kind == CelTypeKind.List ? member : container, subject, value))
        {
            return Placeholder();
        }

        int last = field.Path.Names.Count - 1;
        List<CelExprNode> terms = [];
        Guards(field, last, includeLast: true, lastContinues: false, terms);
        CelExprNode? lookup = wildcard.Kind == FilterWildcardKind.None
            ? Lookup(field, last, ValueNode(member, null, value, subject, container))
            : ElementsMatch(head, field, new ValueSpec(member, null, subject), value, wildcard);
        if (lookup is null)
        {
            return Placeholder();
        }

        terms.Add(lookup);
        return Balance("_&&_", terms, new JoinPlaces(field.Member.Start, value.End));
    }

    // The test that a constant is in the list or the map at the end of the path: constant in path.
    private CelCallNode? Lookup(FieldRef field, int last, CelExprNode? constant)
    {
        if (constant is null)
        {
            return null;
        }

        CelExprNode container = PathExpression(field, last + 1, wholePath: false);
        _factory.Position = field.Place.Whole;
        return _factory.Call("@in", constant, container);
    }

    // The test that some element of the list at the end of the path matches the pattern of a string: list.exists(e, e.startsWith(...)).
    private CelExprNode? ElementsMatch(FilterRestriction head, FieldRef field, ValueSpec element, FilterMember value, FilterWildcard wildcard)
    {
        int last = field.Path.Names.Count - 1;
        string variable = LoopVariable();
        _loops.Add(variable);
        _factory.Position = field.Place.Part(last);
        CelExprNode item = _factory.Ident(variable);
        CelExprNode? body = ValueTest(head, FilterComparator.Equal, item, element, value, wildcard);
        _loops.RemoveAt(_loops.Count - 1);
        return body is null ? null : Exists(field, last, variable, body);
    }

    // object:name - the object has the field of the name.
    private CelExprNode HasMember(FieldRef field, FilterMember value)
    {
        string name = value.Segments[0].Text;
        if (value.Segments.Count != 1 || name.Length == 0)
        {
            Report(
                CelErrorCode.InvalidFilterValue,
                "The value " + _filter[value.Start..value.End] + " is not the name of a field.",
                value.Start,
                value.End,
                Source(field.Member),
                field.Path.FinalType);
            return Placeholder();
        }

        int last = field.Path.Names.Count - 1;
        List<CelExprNode> terms = [];
        Guards(field, last, includeLast: true, lastContinues: true, terms);
        CelExprNode owner = PathExpression(field, last + 1, wholePath: false);
        _factory.Position = value.Start..value.End;
        CelExprNode select = _factory.Select(owner, name);
        terms.Add(_factory.ExpandMacro("has", null, select));
        return Balance("_&&_", terms, new JoinPlaces(field.Member.Start, value.End));
    }
}
