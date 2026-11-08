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
/// <see cref="Children"/> becomes the case; when none does and the root is a macro over a literal range, the first instance
/// of a body of the macro that is accepted becomes the case (<see cref="BodyInstances"/>). The result depends on the case and
/// the outcomes of the sides alone, not on time or on random choices, so the same case gives the same minimized case and the
/// same signature. A failed side ends the minimization with its exception.
/// </summary>
internal static class Minimizer
{
    /// <summary>Minimizes a divergent case.</summary>
    /// <param name="divergent">The divergent case.</param>
    /// <param name="diverges">Whether the outcomes of a case diverge. Called only for children and instances on the way down.</param>
    /// <param name="accepts">
    /// Whether the minimized instance of a body of the macro at the root, which diverges, replaces the root: its arguments are
    /// the root and the instance after its own minimization. <see langword="null"/> means that a macro has no body
    /// instances.
    /// </param>
    /// <returns>The divergent case whose root has no divergent child and no accepted body instance.</returns>
    /// <exception cref="IOException">An <see cref="IOException"/> of <paramref name="diverges"/> or <paramref name="accepts"/>, after which it is not called again.</exception>
    /// <exception cref="SideFailureException">A <see cref="SideFailureException"/> of <paramref name="diverges"/> or <paramref name="accepts"/>, after which it is not called again.</exception>
    internal static GeneratedCase Minimize(
        GeneratedCase divergent,
        Func<GeneratedCase, bool> diverges,
        Func<GeneratedCase, GeneratedCase, bool>? accepts = null)
    {
        GeneratedCase current = divergent;
        while (Next(current, diverges, accepts) is { } next)
        {
            current = next;
        }

        return current;
    }

    /// <summary>
    /// The children of a node: the arguments of a call, the elements of a list, the keys and values of a map, the operand of a
    /// selection and of a presence test, and the range of a macro only, since its other arguments read the iteration variable.
    /// </summary>
    /// <param name="node">The node.</param>
    internal static IEnumerable<Expr> Children(Expr node) => node switch
    {
        Call call => call.Args,
        ListLiteral list => list.Elements,
        MapLiteral map => map.Entries.SelectMany(entry => new[] { entry.Key, entry.Value }),
        Select select => [select.Operand],
        PresenceTest test => [test.Operand],
        Comprehension comprehension => [comprehension.Range],
        _ => [],
    };

    /// <summary>
    /// The instances of the bodies of a macro whose range is a list or map literal, or a literal in <c>dyn()</c>: for each
    /// argument that reads the iteration variable, in order, and each element of the range, in order (the key for a map), the
    /// argument with the variable replaced by the element in parentheses, or by <c>dyn(element)</c> when the range is in
    /// <c>dyn()</c>, so that the static types stay. Nothing for any other range or macro node.
    /// </summary>
    /// <param name="node">The node.</param>
    internal static IEnumerable<Expr> BodyInstances(Expr node)
    {
        if (node is not Comprehension macro || Elements(macro.Range, out bool inDyn) is not { } elements)
        {
            yield break;
        }

        foreach (Expr body in macro.Arguments.Where(argument => Reads(argument, macro.Variable)))
        {
            foreach (Expr element in elements)
            {
                Expr replacement = inDyn ? new Call(ProfileTables.Dyn, [element]) : element with { Parenthesized = true };
                yield return Replace(body, macro.Variable, replacement);
            }
        }
    }

    private static GeneratedCase? Next(GeneratedCase current, Func<GeneratedCase, bool> diverges, Func<GeneratedCase, GeneratedCase, bool>? accepts)
    {
        if (Children(current.Expression).Select(child => current with { Expression = child }).FirstOrDefault(diverges) is { } child)
        {
            return child;
        }

        if (accepts is null)
        {
            return null;
        }

        // An instance longer than the macro does not shrink the case: this also ends the descent.
        int length = current.Text.Length;
        foreach (GeneratedCase instance in BodyInstances(current.Expression).Select(body => current with { Expression = body }))
        {
            if (instance.Text.Length < length && diverges(instance))
            {
                GeneratedCase reduced = Minimize(instance, diverges, accepts);
                if (accepts(current, reduced))
                {
                    return reduced;
                }
            }
        }

        return null;
    }

    // The elements of a range of fixed order: of a list literal, the keys of a map literal; null for any other range.
    private static IReadOnlyList<Expr>? Elements(Expr range, out bool inDyn)
    {
        inDyn = range is Call { Function: ProfileTables.Dyn, Args: [_] };
        Expr literal = range is Call { Function: ProfileTables.Dyn, Args: [{ } argument] } ? argument : range;
        return literal switch
        {
            ListLiteral list => list.Elements,
            MapLiteral map => [.. map.Entries.Select(entry => entry.Key)],
            _ => null,
        };
    }

    // Whether a node reads a variable that is not shadowed by a macro on the way.
    private static bool Reads(Expr node, string variable) => node switch
    {
        Ident ident => ident.Name == variable,
        Comprehension macro => Reads(macro.Range, variable) || (macro.Variable != variable && macro.Arguments.Any(argument => Reads(argument, variable))),
        _ => Children(node).Any(child => Reads(child, variable)),
    };

    // The node with each free read of the variable replaced; a macro that declares the same variable keeps its arguments.
    private static Expr Replace(Expr node, string variable, Expr replacement) => node switch
    {
        Ident ident when ident.Name == variable => replacement,
        Call call => call with { Args = [.. call.Args.Select(argument => Replace(argument, variable, replacement))] },
        ListLiteral list => list with { Elements = [.. list.Elements.Select(element => Replace(element, variable, replacement))] },
        MapLiteral map => map with
        {
            Entries = [.. map.Entries.Select(entry => new MapEntry(Replace(entry.Key, variable, replacement), Replace(entry.Value, variable, replacement)))],
        },
        Select select => select with { Operand = Replace(select.Operand, variable, replacement) },
        PresenceTest test => test with { Operand = Replace(test.Operand, variable, replacement) },
        Comprehension macro => macro with
        {
            Range = Replace(macro.Range, variable, replacement),
            Arguments = macro.Variable == variable
                ? macro.Arguments
                : [.. macro.Arguments.Select(argument => Replace(argument, variable, replacement))],
        },
        _ => node,
    };
}
