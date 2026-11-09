using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Cel.Expr;
using Great.Parts.Cel.Eng.Difftest.Profile;
using Great.Parts.Cel.Eng.Difftest.Sides;

namespace Great.Parts.Cel.Eng.Difftest.Compare;

/// <summary>How the outcomes of a node agree: the verdict of a classification of a node on the way down.</summary>
internal enum NodeVerdict
{
    /// <summary>The outcomes agree and are not both errors.</summary>
    Agrees,

    /// <summary>The outcomes agree because both sides give an error, whatever its cause.</summary>
    AgreesInError,

    /// <summary>The outcomes diverge.</summary>
    Diverges,
}

/// <summary>
/// Shrinks a divergent case (docs/design/eng-difftest.md, "Outcomes and signatures", "Minimization") by a deterministic
/// descent: while a child of the root still diverges with the same bindings, the first such child in the order of
/// <see cref="Children"/> becomes the case. When no child of the root diverges, the descent looks under each child on which
/// both sides give an error, in the order of the source, since two errors agree whatever their causes and such a child can
/// hide the node that diverges: under it the first child that diverges becomes the case, and when none does, the descent looks
/// in the same way under those of its children on which both sides give an error, depth first, before the next child of the
/// root. A node found so becomes the case without any condition. Under a macro only its range is searched, as the children
/// are those of <see cref="Children"/>. When nothing diverges by these rules and the root is a macro, the first instance of a
/// body of the macro that makes the case smaller and is accepted becomes the case (<see cref="BodyInstances"/>). A case is
/// smaller than another when it has fewer macro nodes, or as many and a shorter text. Each node below the root is classified
/// at most once by the descent from a root. The result depends on the case and the outcomes of the sides alone, not on time or
/// on random choices, so the same case gives the same minimized case and the same signature. A failed side ends the
/// minimization with its exception.
/// </summary>
internal static class Minimizer
{
    /// <summary>Minimizes a divergent case.</summary>
    /// <param name="divergent">The divergent case.</param>
    /// <param name="classify">
    /// Whether the outcomes of a case diverge, agree, or agree in an error on both sides. Called only for children, nodes under
    /// children that agree in an error, and instances on the way down; at most once for each node of the descent from a root.
    /// </param>
    /// <param name="accepts">
    /// Whether the minimized instance of a body of the macro at the root, which diverges, replaces the root: its arguments are
    /// the root and the instance after its own minimization. <see langword="null"/> means that a macro has no body
    /// instances.
    /// </param>
    /// <param name="rangeValue">
    /// The value that the library gives for a case, <see langword="null"/> when it gives none: it is asked for the range of a
    /// macro that is not a literal, whose elements are those of its value (<see cref="BodyInstances"/>).
    /// <see langword="null"/> means that such a range has no body instances.
    /// </param>
    /// <returns>The divergent case whose root has no divergent node under it and no accepted body instance.</returns>
    /// <exception cref="IOException">An <see cref="IOException"/> of <paramref name="classify"/>, <paramref name="accepts"/> or <paramref name="rangeValue"/>, after which it is not called again.</exception>
    /// <exception cref="SideFailureException">A <see cref="SideFailureException"/> of <paramref name="classify"/>, <paramref name="accepts"/> or <paramref name="rangeValue"/>, after which it is not called again.</exception>
    internal static GeneratedCase Minimize(
        GeneratedCase divergent,
        Func<GeneratedCase, NodeVerdict> classify,
        Func<GeneratedCase, GeneratedCase, bool>? accepts = null,
        Func<GeneratedCase, Value?>? rangeValue = null)
    {
        GeneratedCase current = divergent;
        while (Next(current, classify, accepts, rangeValue) is { } next)
        {
            current = next;
        }

        return current;
    }

    /// <summary>
    /// The children of a node: the arguments of a call, the elements of a list, the keys and values of a map, the operand of a
    /// selection and of a presence test, and the range of a macro only, since its other arguments are taken as instances of its
    /// bodies (<see cref="BodyInstances"/>).
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
    /// The instances of the bodies of a macro, for each argument after the iteration variable, in the order of the source. An
    /// argument that does not read the variable has one instance, the argument itself, whatever the range holds (elements, none
    /// or an error): it does not depend on an element, and the range is not asked. An argument that reads the variable has an
    /// instance for each element of the range, in order (the key for a map): the argument with each free read of the variable
    /// replaced by a node that stands for the element and fits wherever the variable does. For a list or map literal, or such a
    /// literal in
    /// <c>dyn()</c>, the node is the element in parentheses, or <c>dyn(element)</c> when the range is in <c>dyn()</c>, so that
    /// the static types stay. For any other range (a variable, <c>dyn(x1)</c>, <c>lx + []</c>, the result of <c>map</c> or
    /// <c>filter</c>) the elements are those of its value that the library gives: for a list, the index <c>range[i]</c> for
    /// each position <c>i</c> from 0; for a map, <c>dyn(k)</c> for each key <c>k</c> in the order the library iterates the map.
    /// A range whose value is not a list or a map (an error, an unbound variable, a scalar) has no instances of the arguments
    /// that read the variable. Nothing for a node that is not a macro.
    /// </summary>
    /// <param name="node">The node.</param>
    /// <param name="rangeValue">
    /// The value of the range of the macro on the library, <see langword="null"/> when it gives none. Asked once, and only for a
    /// range that is not a literal and only when the first body that reads the iteration variable is reached.
    /// </param>
    internal static IEnumerable<Expr> BodyInstances(Expr node, Func<Expr, Value?> rangeValue)
    {
        if (node is not Comprehension macro)
        {
            yield break;
        }

        IReadOnlyList<Expr>? replacements = null;
        foreach (Expr body in macro.Arguments)
        {
            if (!Reads(body, macro.Variable))
            {
                // Independent of the element: the argument itself, whatever the range holds.
                yield return body;
                continue;
            }

            replacements ??= Replacements(macro, rangeValue);
            foreach (Expr replacement in replacements)
            {
                yield return Replace(body, macro.Variable, replacement);
            }
        }
    }

    /// <summary>The number of macro nodes of an expression, the first measure of how small a case is.</summary>
    /// <param name="node">The expression.</param>
    internal static int MacroNodes(Expr node) => node switch
    {
        Comprehension macro => 1 + MacroNodes(macro.Range) + macro.Arguments.Sum(MacroNodes),
        _ => Children(node).Sum(MacroNodes),
    };

    /// <summary>
    /// The next case of the descent from a root: the first node that diverges below it, found by the search under the children
    /// on which both sides give an error (<see cref="FirstDivergent"/>), without any condition; else the first accepted instance
    /// of a body of the macro at the root, which is asked for only if nothing was found; <see langword="null"/> when there is
    /// none.
    /// </summary>
    private static GeneratedCase? Next(
        GeneratedCase current,
        Func<GeneratedCase, NodeVerdict> classify,
        Func<GeneratedCase, GeneratedCase, bool>? accepts,
        Func<GeneratedCase, Value?>? rangeValue)
    {
        if (FirstDivergent(current, classify) is { } found)
        {
            return found;
        }

        if (accepts is null)
        {
            return null;
        }

        // An instance is asked for only when it is smaller than the case: fewer macro nodes, or as many and a shorter text.
        // This is what ends the descent.
        int macros = MacroNodes(current.Expression);
        int length = current.Text.Length;
        Value? ValueOfRange(Expr range) => rangeValue?.Invoke(current with { Expression = range });
        foreach (GeneratedCase instance in BodyInstances(current.Expression, ValueOfRange).Select(body => current with { Expression = body }))
        {
            if (IsSmaller(instance, macros, length) && classify(instance) == NodeVerdict.Diverges)
            {
                GeneratedCase reduced = Minimize(instance, classify, accepts, rangeValue);
                if (accepts(current, reduced))
                {
                    return reduced;
                }
            }
        }

        return null;
    }

    // The first node below a node that diverges: the first child that does, else the first node that does found by the same
    // search under the children on which both sides give an error, in the order of the source, depth first. The children are
    // classified in order and the search stops at the first that diverges; a node is classified once, and the children that
    // agree in an error keep their verdict for the search under them.
    private static GeneratedCase? FirstDivergent(GeneratedCase node, Func<GeneratedCase, NodeVerdict> classify)
    {
        List<GeneratedCase> inError = [];
        foreach (Expr child in Children(node.Expression))
        {
            GeneratedCase candidate = node with { Expression = child };
            switch (classify(candidate))
            {
                case NodeVerdict.Diverges:
                    return candidate;
                case NodeVerdict.AgreesInError:
                    inError.Add(candidate);
                    break;
            }
        }

        foreach (GeneratedCase candidate in inError)
        {
            if (FirstDivergent(candidate, classify) is { } found)
            {
                return found;
            }
        }

        return null;
    }

    private static bool IsSmaller(GeneratedCase candidate, int macros, int length)
    {
        int candidateMacros = MacroNodes(candidate.Expression);
        return candidateMacros < macros || (candidateMacros == macros && candidate.Text.Length < length);
    }

    // The nodes that stand for the elements of the range of a macro, in the order of the elements.
    private static IReadOnlyList<Expr> Replacements(Comprehension macro, Func<Expr, Value?> rangeValue)
    {
        bool inDyn = macro.Range is Call { Function: ProfileTables.Dyn, Args: [_] };
        Expr literal = macro.Range is Call { Function: ProfileTables.Dyn, Args: [{ } argument] } ? argument : macro.Range;
        IReadOnlyList<Expr>? elements = literal switch
        {
            ListLiteral list => list.Elements,
            MapLiteral map => [.. map.Entries.Select(entry => entry.Key)],
            _ => null,
        };
        if (elements is not null)
        {
            return [.. elements.Select(element => inDyn ? new Call(ProfileTables.Dyn, [element]) : element with { Parenthesized = true })];
        }

        // Any other range: the elements are those of its value, which both sides give alike since the range agrees.
        Value? value = rangeValue(macro.Range);
        return value?.KindCase switch
        {
            Value.KindOneofCase.ListValue =>
                [.. Enumerable.Range(0, value.ListValue.Values.Count)
                    .Select(index => new Call(ProfileTables.Index, [macro.Range, new IntLiteral(index, IntegerForm.DecimalDigits)]))],
            Value.KindOneofCase.MapValue =>
                [.. value.MapValue.Entries.Select(entry => KeyLiteral(entry.Key)).OfType<Expr>().Select(key => new Call(ProfileTables.Dyn, [key]))],
            _ => [],
        };
    }

    // The literal of a key of a map: an int, a uint, a bool or a string; null for any other value.
    private static Expr? KeyLiteral(Value key) => key.KindCase switch
    {
        Value.KindOneofCase.Int64Value => new IntLiteral(key.Int64Value, IntegerForm.DecimalDigits),
        Value.KindOneofCase.Uint64Value => new UintLiteral(key.Uint64Value, IntegerForm.DecimalDigits, 'u'),
        Value.KindOneofCase.BoolValue => new BoolLiteral(key.BoolValue),
        Value.KindOneofCase.StringValue => StringKey(key.StringValue),
        _ => null,
    };

    // A string in apostrophes, each code point as itself where it may stand so, else in the first form that fits.
    private static StringLiteral StringKey(string value)
    {
        List<Escape> units = [];
        foreach (Rune rune in value.EnumerateRunes())
        {
            units.Add(TextUnits.StringForms(rune.Value, Quote.Apostrophe, raw: false)[0]);
        }

        return new StringLiteral(value, new TextForm(Quote.Apostrophe, null, units));
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
