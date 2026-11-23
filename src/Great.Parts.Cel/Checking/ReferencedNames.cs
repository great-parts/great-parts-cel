using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Runtime.InteropServices;
using Great.Parts.Cel.Syntax;

namespace Great.Parts.Cel.Checking;

// The names that an expression refers to, for CelValidationResult: the variables of the environment and the functions called by
// name, collected from the references that the checker resolved (docs/design/facade-2.0.md, "Validation"). The references are
// the tree's own: a local variable of a comprehension is not a declaration, and an operator or an internal name is not a name of
// a function. A macro leaves no call in the tree, but the text calls it by name, so its name comes from the record of the macro
// calls of the parser.
internal static class ReferencedNames
{
    /// <summary>
    /// Collects the names of the variables and the functions, each list without repetitions and in the order of
    /// <see cref="string.CompareOrdinal(string, string)"/>.
    /// </summary>
    /// <param name="references">The references by node id: all of a checked expression, or those resolved before an error.</param>
    /// <param name="macroCalls">The macro calls that the text writes, as the parser recorded them; default when the text did not parse.</param>
    /// <param name="variables">The names of the variables of the environment.</param>
    /// <param name="functions">The names of the functions and the macros that are an identifier or a qualified name.</param>
    internal static void Collect(
        IReadOnlyDictionary<int, Reference> references,
        ImmutableArray<CelMacroCall> macroCalls,
        out ImmutableArray<string> variables,
        out ImmutableArray<string> functions)
    {
        HashSet<string> variableNames = new(StringComparer.Ordinal);
        HashSet<string> functionNames = new(StringComparer.Ordinal);
        foreach (Reference reference in references.Values)
        {
            if (reference.Variable is { } variable)
            {
                variableNames.Add(variable.Name);
            }
            else if (!reference.Candidates.IsDefaultOrEmpty)
            {
                // The candidates of one call are overloads of one function.
                string function = reference.Candidates[0].Function;
                if (IsName(function))
                {
                    functionNames.Add(function);
                }
            }
        }

        if (!macroCalls.IsDefaultOrEmpty)
        {
            foreach (CelMacroCall call in macroCalls)
            {
                if (call.IsWritten && IsName(call.Function))
                {
                    functionNames.Add(call.Function);
                }
            }
        }

        variables = Sorted(variableNames);
        functions = Sorted(functionNames);
    }

    // An identifier, or identifiers joined by dots, such as math.greatest; the operators (_+_, !_, _?_:_) and the names with @ are
    // not.
    internal static bool IsName(string function)
    {
        bool start = true;
        foreach (char c in function)
        {
            if (c == '.')
            {
                if (start)
                {
                    return false;
                }

                start = true;
            }
            else if (!(c is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or '_' || (!start && c is >= '0' and <= '9')))
            {
                return false;
            }
            else
            {
                start = false;
            }
        }

        return !start;
    }

    private static ImmutableArray<string> Sorted(HashSet<string> names)
    {
        string[] sorted = [.. names];
        Array.Sort(sorted, StringComparer.Ordinal);
        return ImmutableCollectionsMarshal.AsImmutableArray(sorted);
    }
}
