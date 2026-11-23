using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CsCheck;
using Great.Parts.Cel.Eng.Difftest.Profile;
using Great.Parts.Cel.Eng.Difftest.Sides;

namespace Great.Parts.Cel.Eng.Difftest.Corpus;

/// <summary>
/// The patterns of unknown data of a case for the partial comparison (docs/design/eng-difftest.md, "Partial evaluation"). They
/// are built from the attributes of the expression: a variable of the manifest with the selections and the indexes by a constant
/// that are applied to it. For each variable the candidates are the whole variable, each path of the expression, and each path with
/// a qualifier that matches any in place of the last one or after it. A case takes one or two variables and one candidate of
/// each, so that no access matches two patterns: cel-go picks among several matching patterns at random, and the library by order.
/// </summary>
internal static class PartialPatterns
{
    // XOR-ed into the seed, so that the draws of the patterns of a case are a stream of their own and the generation of the cases,
    // and with it every reproduction by seed and number, does not change.
    private const ulong StreamSalt = 0x5041525449414C21UL;

    // One case in this number takes a second variable.
    private const int SecondVariableOneIn = 3;

    /// <summary>The state of the draws of the patterns of case <c>n</c> of the run with a seed.</summary>
    /// <param name="seed">The seed of the run.</param>
    /// <param name="n">The number of the case.</param>
    internal static PCG State(ulong seed, ulong n) => CaseGenerator.CaseState(seed ^ StreamSalt, n);

    /// <summary>The patterns of a case; none when the expression reads no variable.</summary>
    /// <param name="seed">The seed of the run.</param>
    /// <param name="n">The number of the case.</param>
    /// <param name="generated">The case.</param>
    internal static IReadOnlyList<UnknownAttribute> For(ulong seed, ulong n, GeneratedCase generated)
    {
        IReadOnlyList<(string Variable, IReadOnlyList<UnknownAttribute> Candidates)> variables = Candidates(generated.Expression);
        if (variables.Count == 0)
        {
            return [];
        }

        PCG pcg = State(seed, n);
        List<(string Variable, IReadOnlyList<UnknownAttribute> Candidates)> remaining = [.. variables];
        List<UnknownAttribute> patterns = [];
        int count = pcg.OneIn(SecondVariableOneIn) ? 2 : 1;
        for (int taken = 0; taken < count && remaining.Count > 0; taken++)
        {
            int index = pcg.Below(remaining.Count);
            patterns.Add(pcg.Pick(remaining[index].Candidates));
            remaining.RemoveAt(index);
        }

        return patterns;
    }

    /// <summary>
    /// The variables an expression reads, in the order of their first read, each with its candidates: the whole variable first, then
    /// for each path of the expression the path, the path with its last qualifier replaced by a qualifier that matches any, and the
    /// path with such a qualifier after it, each once.
    /// </summary>
    /// <param name="expression">The expression.</param>
    internal static IReadOnlyList<(string Variable, IReadOnlyList<UnknownAttribute> Candidates)> Candidates(Expr expression)
    {
        List<UnknownAttribute> paths = [];
        Collect(expression, new HashSet<string>(StringComparer.Ordinal), paths);
        List<string> order = [.. paths.Select(path => path.Variable).Distinct(StringComparer.Ordinal)];
        return
        [
            .. order.Select(variable => (variable, (IReadOnlyList<UnknownAttribute>)Distinct(
                [new UnknownAttribute(variable, []), .. paths.Where(path => path.Variable == variable).SelectMany(Variants)]))),
        ];
    }

    // A path, the path with its last qualifier replaced by one that matches any, and the path with one more after it.
    private static IEnumerable<UnknownAttribute> Variants(UnknownAttribute path)
    {
        yield return path;
        if (path.Qualifiers.Count > 0)
        {
            yield return new UnknownAttribute(path.Variable, [.. path.Qualifiers.Take(path.Qualifiers.Count - 1), Wildcard]);
        }

        yield return new UnknownAttribute(path.Variable, [.. path.Qualifiers, Wildcard]);
    }

    private static AttributeQualifier Wildcard => new(QualifierKind.Any, string.Empty);

    private static List<UnknownAttribute> Distinct(IEnumerable<UnknownAttribute> attributes) =>
        [.. attributes.DistinctBy(attribute => attribute.Key, StringComparer.Ordinal)];

    // Adds the path of each node that is an attribute: a variable, or a variable with selections, presence tests and indexes by a
    // constant applied to it. A name that a macro or cel.bind declares hides the variable of the same name in its body.
    private static void Collect(Expr expression, HashSet<string> hidden, List<UnknownAttribute> paths)
    {
        if (PathOf(expression, hidden) is { } path)
        {
            paths.Add(path);
        }

        switch (expression)
        {
            case Call call:
                foreach (Expr argument in call.Args)
                {
                    Collect(argument, hidden, paths);
                }

                break;
            case Select select:
                Collect(select.Operand, hidden, paths);
                break;
            case PresenceTest test:
                Collect(test.Operand, hidden, paths);
                break;
            case ListLiteral list:
                foreach (Expr element in list.Elements)
                {
                    Collect(element, hidden, paths);
                }

                break;
            case MapLiteral map:
                foreach (MapEntry entry in map.Entries)
                {
                    Collect(entry.Key, hidden, paths);
                    Collect(entry.Value, hidden, paths);
                }

                break;
            case Comprehension comprehension:
                Collect(comprehension.Range, hidden, paths);
                HashSet<string> inner = [.. hidden, comprehension.Variable];
                if (comprehension.SecondVariable is { } second)
                {
                    inner.Add(second);
                }

                foreach (Expr argument in comprehension.Arguments)
                {
                    Collect(argument, inner, paths);
                }

                break;
        }
    }

    // The path of a node that is an attribute, or null.
    private static UnknownAttribute? PathOf(Expr expression, HashSet<string> hidden) => expression switch
    {
        Ident ident when IsVariable(ident.Name) && !hidden.Contains(ident.Name) => new UnknownAttribute(ident.Name, []),
        Select select => Extend(PathOf(select.Operand, hidden), Field(select.Field)),
        PresenceTest test => Extend(PathOf(test.Operand, hidden), Field(test.Field)),
        Call { Function: ProfileTables.Index, Args: [var operand, var key] } when Constant(key) is { } qualifier =>
            Extend(PathOf(operand, hidden), qualifier),
        _ => null,
    };

    private static UnknownAttribute? Extend(UnknownAttribute? path, AttributeQualifier qualifier) =>
        path is null ? null : new UnknownAttribute(path.Variable, [.. path.Qualifiers, qualifier]);

    private static AttributeQualifier Field(string name) => new(QualifierKind.String, name);

    // The qualifier of an index by a literal key; null for a key that is not one.
    private static AttributeQualifier? Constant(Expr key) => key switch
    {
        IntLiteral literal => new AttributeQualifier(QualifierKind.Int, literal.Value.ToString(CultureInfo.InvariantCulture)),
        UintLiteral literal => new AttributeQualifier(QualifierKind.Uint, literal.Value.ToString(CultureInfo.InvariantCulture)),
        StringLiteral literal => new AttributeQualifier(QualifierKind.String, literal.Value),
        BoolLiteral literal => new AttributeQualifier(QualifierKind.Bool, literal.Value ? "true" : "false"),
        _ => null,
    };

    private static bool IsVariable(string name) => ProfileTables.Variables.Any(variable => variable.Name == name);
}
