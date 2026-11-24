using System.Collections.Generic;
using System.Collections.Immutable;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Policy.Yaml;

namespace Great.Parts.Cel.Policy;

// The model of a policy as the reader of the format builds it: what the document says, with the node of the document each part
// comes from. It is internal: the compilation of a policy reads it, applications do not.

// The evaluation semantic of a rule: the output of the first match that holds, or the outputs of all matches that hold.
internal enum PolicySemantic
{
    Unspecified,
    FirstMatch,
    Aggregate,
}

// The text of an expression, or of another string of the policy that the format reads as one, with the segments that tell where each
// part of the text stands in the document, and the node it comes from.
internal sealed class PolicyString(string text, ImmutableArray<CelSourceSegment> segments, YamlNode node)
{
    internal string Text { get; } = text;

    // At least one segment, the first at the offset 0.
    internal ImmutableArray<CelSourceSegment> Segments { get; } = segments;

    internal YamlNode Node { get; } = node;
}

// A variable of a rule: a name and the expression whose value the name stands for in the later expressions.
internal sealed class PolicyVariable(YamlNode node)
{
    internal YamlNode Node { get; } = node;

    internal PolicyString? Name { get; set; }

    internal PolicyString? Expression { get; set; }
}

// A match: a condition (true when the document has none), and either an output with an explanation or a nested rule.
internal sealed class PolicyMatch(YamlNode node)
{
    internal YamlNode Node { get; } = node;

    // Null when the document has no condition.
    internal PolicyString? Condition { get; set; }

    internal PolicyString? Output { get; set; }

    internal PolicyString? Explanation { get; set; }

    internal PolicyRule? Rule { get; set; }
}

// A rule: variables and matches.
internal sealed class PolicyRule(YamlNode node)
{
    internal YamlNode Node { get; } = node;

    internal PolicyString? Id { get; set; }

    internal PolicyString? Description { get; set; }

    internal List<PolicyVariable> Variables { get; } = [];

    internal List<PolicyMatch> Matches { get; } = [];

    internal PolicySemantic Semantic { get; set; }
}

// An import: the qualified name of a type that the expressions may name by its last part.
internal sealed class PolicyImport(YamlNode node)
{
    internal YamlNode Node { get; } = node;

    internal PolicyString? Name { get; set; }
}

// The policy: its name, description, imports and the root rule.
internal sealed class PolicyDocument(YamlNode node)
{
    internal YamlNode Node { get; } = node;

    internal PolicyString? Name { get; set; }

    // The description is a string of the YAML, not an expression.
    internal string? Description { get; set; }

    internal List<PolicyImport> Imports { get; } = [];

    internal PolicyRule? Rule { get; set; }
}
