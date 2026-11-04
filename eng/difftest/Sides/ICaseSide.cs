using System;
using System.Collections.Generic;
using Cel.Expr;

namespace Great.Parts.Cel.Eng.Difftest.Sides;

/// <summary>
/// One side of the differential run: the library or the oracle (docs/design/eng-difftest.md, "Outcomes and signatures",
/// "Findings and output"). The tests of eng/difftest use fake sides with given outcomes.
/// </summary>
internal interface ICaseSide
{
    /// <summary>Compiles and evaluates an expression of the profile with bindings.</summary>
    /// <param name="expression">The expression as CEL source.</param>
    /// <param name="bindings">The bound variables; a variable of the manifest that is absent is unbound.</param>
    /// <returns>The outcome: a compile error, an evaluation error or a value.</returns>
    SideOutcome Evaluate(string expression, IReadOnlyDictionary<string, Value> bindings);

    /// <summary>
    /// The offset of a time zone from UTC at an instant. The profile has no time zone data file until time: runs
    /// do not compare offsets, and both sides throw <see cref="NotSupportedException"/>.
    /// </summary>
    /// <param name="zone">The name of the zone in the time zone data file, such as <c>Europe/Berlin</c>.</param>
    /// <param name="instant">The instant.</param>
    /// <returns>The offset from UTC.</returns>
    TimeSpan ZoneOffset(string zone, DateTimeOffset instant);
}
