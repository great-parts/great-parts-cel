using System.Collections.Generic;
using Cel.Expr;
using CsCheck;

namespace Great.Parts.Cel.Eng.Difftest.Profile;

/// <summary>A generated case: an expression of the profile and the bindings of the variables of the manifest.</summary>
/// <param name="Expression">The expression.</param>
/// <param name="Bindings">The bound variables in the order of the manifest; an unbound variable is absent.</param>
internal sealed record GeneratedCase(Expr Expression, IReadOnlyDictionary<string, Value> Bindings)
{
    /// <summary>The expression as CEL source.</summary>
    internal string Text => ExprPrinter.Print(Expression);
}

/// <summary>
/// The generator of the cases of a run. Case <c>n</c> of a run depends on the seed of the run and <c>n</c> alone: its
/// CsCheck generator state is derived from the two numbers, and every choice is drawn from that state.
/// </summary>
internal sealed class CaseGenerator : Gen<GeneratedCase>
{
    // One variable in ten stays unbound.
    private const int UnboundOneIn = 10;

    private CaseGenerator()
    {
    }

    /// <summary>The generator; the size of a case is its number of nodes, then the length of its text.</summary>
    internal static CaseGenerator Instance { get; } = new();

    /// <summary>Generates case <c>n</c> of the run with a seed.</summary>
    /// <param name="seed">The seed of the run.</param>
    /// <param name="n">The number of the case.</param>
    internal static GeneratedCase Case(ulong seed, ulong n) => Instance.Generate(CaseState(seed, n), null, out _);

    /// <summary>The CsCheck generator state of case <c>n</c> of the run with a seed.</summary>
    /// <param name="seed">The seed of the run.</param>
    /// <param name="n">The number of the case.</param>
    internal static PCG CaseState(ulong seed, ulong n)
    {
        ulong state = Mix(Mix(seed) ^ n);
        return new PCG((uint)(Mix(state) >> 32), state);
    }

    /// <inheritdoc/>
    public override GeneratedCase Generate(PCG pcg, Size? min, out Size size)
    {
        ExprBuilder builder = new(pcg);
        (Expr expression, int nodes) = builder.Build(builder.Target(), depth: 1, ProfileTables.MaxNodes);
        Dictionary<string, Value> bindings = [];
        foreach (ProfileVariable variable in ProfileTables.Variables)
        {
            if (!pcg.OneIn(UnboundOneIn))
            {
                // A call of matches that takes s1 as its subject or its pattern has its text bound to it.
                bindings[variable.Name] = variable.Name == "s1" && builder.S1Binding is { } text
                    ? new Value { StringValue = text }
                    : ProfileValues.Binding(pcg, variable);
            }
        }

        GeneratedCase generated = new(expression, bindings);
        size = new Size((ulong)nodes, new Size((ulong)generated.Text.Length));
        return generated;
    }

    // SplitMix64: spreads the bits of a number, so near seeds and case numbers give unrelated states.
    private static ulong Mix(ulong value)
    {
        ulong z = value + 0x9E3779B97F4A7C15UL;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }
}
