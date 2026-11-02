using System.Collections.Generic;
using Great.Parts.Cel.Syntax;
using Great.Parts.Cel.Types;

namespace Great.Parts.Cel.Checking;

// The result of a successful check: the parsed expression, the reference of every identifier and call,
// the type of every node, both by node id, and the type of the whole expression. Its references hold
// the declarations and overloads of the environment that checked it.
internal sealed class CheckedExpression
{
    internal CheckedExpression(
        ParsedExpression parsed,
        IReadOnlyDictionary<int, Reference> references,
        IReadOnlyDictionary<int, CelType> types)
    {
        Parsed = parsed;
        References = references;
        Types = types;
        ResultType = types[parsed.Root.Id];
    }

    internal ParsedExpression Parsed { get; }

    internal IReadOnlyDictionary<int, Reference> References { get; }

    internal IReadOnlyDictionary<int, CelType> Types { get; }

    // The type of the root, which CelProgram<T>.ResultType reports.
    internal CelType ResultType { get; }
}
