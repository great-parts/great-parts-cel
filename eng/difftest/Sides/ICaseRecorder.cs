using System.Collections.Generic;
using Cel.Expr;

namespace Great.Parts.Cel.Eng.Difftest.Sides;

/// <summary>
/// Writes a case of the regression corpus with the outcome of cel-go as the expectation: the request <c>case</c> of the oracle
/// (docs/design/eng-oracle.md, "Protocol").
/// </summary>
internal interface ICaseRecorder
{
    /// <summary>Writes a case.</summary>
    /// <param name="name">The name of the case in its section: <c>&lt;seed&gt;-&lt;n&gt;</c>.</param>
    /// <param name="description">The description of the case.</param>
    /// <param name="expression">The expression as CEL source.</param>
    /// <param name="bindings">The bound variables.</param>
    /// <returns>The fields of a <c>SimpleTest</c> in text format; the text is copied into <c>pending.textproto</c> unchanged.</returns>
    string Case(string name, string description, string expression, IReadOnlyDictionary<string, Value> bindings);
}
