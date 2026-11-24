using System;
using System.Collections.Generic;
using System.Globalization;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Syntax;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Filtering;

/// <summary>
/// Translates the tree of a filter into the tree of a CEL expression with one <see cref="CelExprFactory"/> whose source text is the
/// filter, so that every node has its place in the filter; collects the errors of the translation.
/// </summary>
/// <remarks>
/// Chains of <c>AND</c>, of a sequence and of <c>OR</c> are flattened into lists and built as balanced trees with the same split
/// as the parser of CEL, so the tree of a filter equals the tree of the CEL text with the same operands and the depth of the
/// tree does not grow with the length of a chain.
/// </remarks>
internal sealed partial class FilterTranslator
{
    private const int MaxErrors = 100;

    private readonly string _filter;
    private readonly FieldCatalog _catalog;
    private readonly CelExprFactory _factory;
    private readonly List<CelError> _errors = [];

    // The names of the variables of the loops that enclose the part being translated.
    private readonly List<string> _loops = [];

    private FilterTranslator(string filter, FieldCatalog catalog)
    {
        _filter = filter;
        _catalog = catalog;
        _factory = new CelExprFactory(filter);
    }

    /// <summary>
    /// Translates a filter.
    /// </summary>
    /// <param name="filter">The text of the filter.</param>
    /// <param name="root">The tree of the filter.</param>
    /// <param name="catalog">The description of the environment.</param>
    /// <returns>The tree of the CEL expression, or all errors of the translation.</returns>
    internal static CelParseResult Translate(string filter, FilterExpr root, FieldCatalog catalog)
    {
        FilterTranslator translator = new(filter, catalog);
        CelExprNode tree = translator.Node(root);
        if (translator._errors.Count > 0)
        {
            return CelParseResult.FromErrors(translator._errors);
        }

        translator._factory.Position = null;
        return CelParseResult.FromParsedExpr(translator._factory.Build(tree));
    }

    private CelExprNode Node(FilterExpr expression)
    {
        switch (expression)
        {
            case FilterEmpty:
                _factory.Position = null;
                return _factory.Constant(CelValue.FromBoolean(true));
            case FilterJoin join:
                return Join(join);
            case FilterNot not:
                return Not(not);
            case FilterGroup group:
                return Node(group.Inner);
            case FilterRestriction restriction:
                return Restriction(restriction);
            case FilterMember member:
                return Global(member);
            default:
                return FunctionCall((FilterCall)expression);
        }
    }

    private CelExprNode Join(FilterJoin join)
    {
        List<CelExprNode> terms = new(join.Operands.Count);
        foreach (FilterExpr operand in join.Operands)
        {
            terms.Add(Node(operand));
        }

        return Balance(join.Kind == FilterJoinKind.Or ? "_||_" : "_&&_", terms, new JoinPlaces(join));
    }

    private CelCallNode Not(FilterNot not)
    {
        CelExprNode operand = Node(not.Operand);
        _factory.Position = not.MarkStart..not.MarkEnd;
        return _factory.Call("!_", operand);
    }

    // A chain of one operator over terms, as a balanced tree: the operator in the middle is the root. A single term is itself.
    private CelExprNode Balance(string function, List<CelExprNode> terms, JoinPlaces places) =>
        terms.Count == 1 ? terms[0] : Balance(function, terms, places, 0, terms.Count - 2);

    // The operators low to high over the terms low to high + 1.
    private CelCallNode Balance(string function, List<CelExprNode> terms, JoinPlaces places, int low, int high)
    {
        int middle = (low + high + 1) / 2;
        CelExprNode left = middle == low ? terms[middle] : Balance(function, terms, places, low, middle - 1);
        CelExprNode right = middle == high ? terms[middle + 1] : Balance(function, terms, places, middle + 1, high);
        _factory.Position = places.At(low, middle, high);
        return _factory.Call(function, left, right);
    }

    // An error of the translation at a range of the filter; after 100 errors one more error, which has no place, ends the list.
    private void Report(CelErrorCode code, string message, int start, int end, string? subject = null, CelType? expectedType = null, CelType? actualType = null)
    {
        if (_errors.Count > MaxErrors)
        {
            return;
        }

        if (_errors.Count == MaxErrors)
        {
            _factory.Position = null;
            _errors.Add(_factory.CreateError(CelErrorCode.TooManyErrors, "more than 100 errors in the filter; the others are not reported"));
            return;
        }

        _factory.Position = start..end;
        _errors.Add(_factory.CreateError(code, message, subject, expectedType, actualType));
    }

    private void Report(CelErrorCode code, string message, Range at, string? subject = null, CelType? expectedType = null, CelType? actualType = null) =>
        Report(code, message, at.Start.Value, at.End.Value, subject, expectedType, actualType);

    // The name of the variable of a loop over a list: e, then e1, e2, ... the first that no variable of the environment and no
    // enclosing loop has.
    private string LoopVariable()
    {
        for (int n = 0; ; n++)
        {
            string name = n == 0 ? "e" : "e" + n.ToString(CultureInfo.InvariantCulture);
            if (!_catalog.IsVariable(name) && !_loops.Contains(name))
            {
                return name;
            }
        }
    }

    // The node that stands for a part of the filter that has an error: the translation goes on to find the other errors.
    private CelConstantNode Placeholder()
    {
        _factory.Position = null;
        return _factory.Constant(CelValue.FromBoolean(true));
    }

    // Where the nodes of a join are in the filter: a keyword for AND and OR, the operands for a sequence, or one fixed range.
    private readonly struct JoinPlaces
    {
        private readonly FilterJoin? _join;
        private readonly (int Start, int End) _fixed;

        internal JoinPlaces(FilterJoin join)
        {
            _join = join;
            _fixed = default;
        }

        internal JoinPlaces(int start, int end)
        {
            _join = null;
            _fixed = (start, end);
        }

        // The range of the operator that joins the terms low to middle with the terms middle + 1 to high + 1.
        internal Range At(int low, int middle, int high)
        {
            if (_join is null)
            {
                return _fixed.Start.._fixed.End;
            }

            return _join.Kind == FilterJoinKind.Sequence
                ? _join.Operands[low].Start.._join.Operands[high + 1].End
                : _join.Keywords[middle].Start.._join.Keywords[middle].End;
        }
    }
}
