using System.Collections.Generic;
using System.Runtime.InteropServices;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Policy.Yaml;
using Great.Parts.Cel.Syntax;
using Great.Parts.Cel.Types;

namespace Great.Parts.Cel.Policy;

// The checks of the policy: the probe is type-checked once, and the types of its parts give the checks of the policy that cel-go makes
// when it compiles a rule (policy/compiler.go).
internal sealed partial class PolicyCompiler
{
    private const string BlockFunction = "cel.@block";

    // Checks the probe, then the policy. The errors of the expressions are those of type checking, at their places in the document. The
    // checks that read types need the types of every part, so they run when every text parsed and the probe has no error; the check of
    // unreachable matches reads the structure only and always runs.
    private void CheckProbe()
    {
        if (_root is null)
        {
            return;
        }

        CheckUnreachable();
        CelParsedExpr probe = _factory.Build(BuildProbe());
        CelValidationResult result = _checkEnvironment.ValidateParsed(probe, _documentPosition);
        AddAll(result.Errors);
        if (!_parseFailed && result.CheckedExpr is { } checkedProbe)
        {
            CheckTypes(checkedProbe.TypeMap);
        }
    }

    // cel.@block([the variables], [the conditions and the outputs]), or the list alone when the policy has no variables.
    private CelExprNode BuildProbe()
    {
        CelListNode parts = _factory.List(CollectionsMarshal.AsSpan(_parts));
        return _slots.Count == 0 ? parts : _factory.Call(BlockFunction, _factory.List(CollectionsMarshal.AsSpan(_slots)), parts);
    }

    private void CheckTypes(IReadOnlyDictionary<int, CelType> types)
    {
        foreach (CompiledVariable variable in _slotVariables)
        {
            variable.Type = TypeOfNode(types, _slots[variable.Slot]);
        }

        foreach (CompiledVariable variable in _slotVariables)
        {
            CheckOverlap(variable);
        }

        foreach (CompiledExpression condition in _conditions)
        {
            CheckCondition(condition, types);
        }

        foreach (CompiledRule rule in _postOrder)
        {
            rule.OutputType = OutputTypeOf(rule, types);
            CheckAgreement(rule, types);
        }
    }

    private static CelType TypeOfNode(IReadOnlyDictionary<int, CelType> types, CelExprNode node) =>
        types.TryGetValue(node.Id, out CelType? type) ? type : CelType.Dyn;

    // A variable that has the name of a visible one and another type is an error; the same type shadows it.
    private void CheckOverlap(CompiledVariable variable)
    {
        if (variable.Shadowed < 0 || variable.Type is not { } type || _slotVariables[variable.Shadowed].Type is not { } visible || type.Equals(visible))
        {
            return;
        }

        string name = VariableRewriter.Prefix + "." + variable.Name;
        Report(
            CelErrorCode.OverlappingVariable,
            "overlapping identifier for name '" + name + "'",
            variable.Source.Node,
            name,
            visible,
            type);
    }

    private void CheckCondition(CompiledExpression condition, IReadOnlyDictionary<int, CelType> types)
    {
        if (condition.Probe is null)
        {
            return;
        }

        CelType type = TypeOfNode(types, condition.Probe);
        if (!TypeRules.IsAssignable(CelType.Bool, type))
        {
            Report(
                CelErrorCode.ExpectedTypeMismatch,
                "expression of type '" + type + "' is not assignable to the expected type 'bool'",
                condition.Source.Node,
                null,
                CelType.Bool,
                type);
        }
    }

    // The type of the output of a match; of its nested rule when it has one.
    private static CelType TypeOfMatch(CompiledMatch match, IReadOnlyDictionary<int, CelType> types) =>
        match.Output?.Probe is { } probe ? TypeOfNode(types, probe) : match.Nested?.OutputType ?? CelType.Dyn;

    // The type of a rule is the type of its first match, a list of it for an aggregate rule; the matches agree with it.
    private static CelType OutputTypeOf(CompiledRule rule, IReadOnlyDictionary<int, CelType> types)
    {
        if (rule.Matches.Count == 0)
        {
            return CelType.Dyn;
        }

        CelType first = TypeOfMatch(rule.Matches[0], types);
        return rule.Semantic == PolicySemantic.Aggregate ? CelType.ListOf(first) : first;
    }

    // The outputs of the matches of a rule must agree: the type of each is assignable from the type of the first, or the other way round.
    private void CheckAgreement(CompiledRule rule, IReadOnlyDictionary<int, CelType> types)
    {
        CelType? previous = null;
        foreach (CompiledMatch match in rule.Matches)
        {
            CelType type = TypeOfMatch(match, types);
            previous ??= type;
            if (TypeRules.IsAssignable(previous, type) || TypeRules.IsAssignable(type, previous))
            {
                continue;
            }

            YamlNode node = match.Output?.Source.Node ?? match.Nested?.Source.Node ?? match.Source.Node;
            Report(
                CelErrorCode.IncompatibleOutputTypes,
                "incompatible output types: block has output type " + type + ", but previous outputs have type " + previous,
                node,
                null,
                previous,
                type);
            return;
        }
    }

    // A first-match rule whose unconditional match always gives an output makes the matches after it unreachable, and a condition that is
    // the literal false is never met.
    private void CheckUnreachable()
    {
        foreach (CompiledRule rule in _postOrder)
        {
            int last = rule.Matches.Count - 1;
            for (int i = last; i >= 0; i--)
            {
                CompiledMatch match = rule.Matches[i];
                if (match.ConditionIsFalse)
                {
                    Report(CelErrorCode.UnreachableMatch, "Condition is always false", match.Source.Node);
                }

                bool exhaustive = match.ConditionIsTrue && match.Nested is not { HasOptionalOutput: true };
                if (rule.Semantic == PolicySemantic.FirstMatch && exhaustive && i != last)
                {
                    ReportUnreachable(match);
                    break;
                }
            }
        }
    }

    private void ReportUnreachable(CompiledMatch match)
    {
        if (match.Output is not null)
        {
            Report(CelErrorCode.UnreachableMatch, "match creates unreachable outputs", match.Source.Node);
        }

        if (match.Nested is not null)
        {
            Report(CelErrorCode.UnreachableMatch, "rule creates unreachable outputs", match.Nested.Source.Node);
        }
    }
}
