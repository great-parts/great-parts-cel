using System.Collections.Generic;

namespace Great.Parts.Cel.Policy;

// The policy of explanations, as GetExplanationOutputPolicy of cel-go makes it: the same policy in which the output of each match is its
// explanation. A match with an output but no explanation has no output then, and a match without an output or a nested rule is not part
// of its rule, though its condition is still checked. The imports stay, so the explanations can name the imported types. The rules are
// copied with an explicit stack, so the depth of the policy costs no stack.
internal static class ExplanationPolicy
{
    internal static CelPolicy Of(CelPolicy policy)
    {
        PolicyDocument source = policy.Document;
        var document = new PolicyDocument(source.Node)
        {
            Name = source.Name,
            Description = source.Description,
        };
        document.Imports.AddRange(source.Imports);
        if (source.Rule is not null)
        {
            document.Rule = Convert(source.Rule);
        }

        return new CelPolicy(policy.Text, policy.SourceName, document);
    }

    private static PolicyRule Convert(PolicyRule root)
    {
        PolicyRule converted = Start(root);
        Stack<(PolicyRule Source, PolicyRule Target)> pending = new();
        pending.Push((root, converted));
        while (pending.Count > 0)
        {
            (PolicyRule source, PolicyRule target) = pending.Pop();
            foreach (PolicyMatch match in source.Matches)
            {
                var explained = new PolicyMatch(match.Node) { Condition = match.Condition, Output = match.Explanation };
                if (match.Rule is not null)
                {
                    explained.Rule = Start(match.Rule);
                    pending.Push((match.Rule, explained.Rule));
                }

                target.Matches.Add(explained);
            }
        }

        return converted;
    }

    // A rule with the variables of the source and no matches yet.
    private static PolicyRule Start(PolicyRule source)
    {
        var rule = new PolicyRule(source.Node)
        {
            Id = source.Id,
            Description = source.Description,
            Semantic = source.Semantic,
        };
        rule.Variables.AddRange(source.Variables);
        return rule;
    }
}
