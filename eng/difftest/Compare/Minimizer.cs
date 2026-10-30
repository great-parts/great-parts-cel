using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Great.Parts.Cel.Eng.Difftest.Profile;
using Great.Parts.Cel.Eng.Difftest.Sides;

namespace Great.Parts.Cel.Eng.Difftest.Compare;

/// <summary>
/// Shrinks a divergent case (docs/design/eng-difftest.md, "Outcomes and signatures", "Minimization") by a deterministic
/// descent: while a child of the root still diverges with the same bindings, the first such child in the order of
/// <see cref="Children"/> becomes the case. The result depends on the case and the outcomes of the sides alone, not on time
/// or on random choices, so the same case gives the same minimized case and the same signature. A failed side ends the
/// minimization with its exception.
/// </summary>
internal static class Minimizer
{
    /// <summary>Minimizes a divergent case.</summary>
    /// <param name="divergent">The divergent case.</param>
    /// <param name="diverges">Whether the outcomes of a case diverge. Called only for children on the way down.</param>
    /// <returns>The divergent case whose root has no divergent child.</returns>
    /// <exception cref="IOException">An <see cref="IOException"/> of <paramref name="diverges"/>, after which it is not called again.</exception>
    /// <exception cref="SideFailureException">A <see cref="SideFailureException"/> of <paramref name="diverges"/>, after which it is not called again.</exception>
    internal static GeneratedCase Minimize(GeneratedCase divergent, Func<GeneratedCase, bool> diverges)
    {
        GeneratedCase current = divergent;
        while (Children(current.Expression).Select(child => current with { Expression = child }).FirstOrDefault(diverges) is { } child)
        {
            current = child;
        }

        return current;
    }

    /// <summary>The children of a node: the arguments of a call, the elements of a list, the keys and values of a map.</summary>
    /// <param name="node">The node.</param>
    internal static IEnumerable<Expr> Children(Expr node) => node switch
    {
        Call call => call.Args,
        ListLiteral list => list.Elements,
        MapLiteral map => map.Entries.SelectMany(entry => new[] { entry.Key, entry.Value }),
        _ => [],
    };
}
