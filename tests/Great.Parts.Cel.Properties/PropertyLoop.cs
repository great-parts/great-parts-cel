using System;
using System.Globalization;
using CsCheck;
using Xunit;

namespace Great.Parts.Cel.Properties;

/// <summary>
/// The loop of a property with a large domain (docs/design/tests.md, "Property tests and the seed"): case <c>i</c> is generated
/// from the pair of the seed and <c>i</c>, so the verdict is the same on every machine and in every run.
/// </summary>
internal static class PropertyLoop
{
    // The iterations of Sample that look for a smaller failing case after the loop has found one.
    private const int MinimizingIterations = 1000;

    // The longest text of a case in a failure message.
    private const int LongestShownCase = 2000;

    /// <summary>
    /// Checks a property on the cases <c>0</c> to <paramref name="count"/> − 1 and fails on the first case that breaks it, with the
    /// name of the property, the seed, the number of the case, the reason and the smallest failing case.
    /// </summary>
    /// <typeparam name="T">The type of a case.</typeparam>
    /// <param name="property">The name of the property in the failure message.</param>
    /// <param name="generator">Generates a case.</param>
    /// <param name="seed">The seed of the property.</param>
    /// <param name="count">The number of cases.</param>
    /// <param name="violation">The reason why a case breaks the property, or <see langword="null"/> when it holds.</param>
    /// <param name="show">The text of a case in the failure message.</param>
    internal static void Check<T>(string property, Gen<T> generator, ulong seed, int count, Func<T, string?> violation, Func<T, string> show)
    {
        for (int i = 0; i < count; i++)
        {
            // The stream of the generator is the number of the case, the state its seed.
            PCG random = new((uint)i, seed);
            ulong state = random.Seed;
            T value = generator.Generate(random, null!, out _);
            if (violation(value) is { } reason)
            {
                string smallest = Shown(Minimized(generator, random.ToString(state), value, violation, show));
                Assert.Fail(string.Create(
                    CultureInfo.InvariantCulture,
                    $"{property}: seed {seed}, case {i}: {reason}; smallest case: {smallest}"));
            }
        }
    }

    // Sample starts from the failing case and keeps the shortest failing case it meets. Sample takes the later iterations
    // from a seed of its own, so the smallest case may differ between runs; the verdict does not.
    private static string Minimized<T>(Gen<T> generator, string start, T failing, Func<T, string?> violation, Func<T, string> show)
    {
        string smallest = show(failing);
        try
        {
            generator.Sample(
                value =>
                {
                    if (violation(value) is null)
                    {
                        return true;
                    }

                    string text = show(value);
                    if (text.Length < smallest.Length)
                    {
                        smallest = text;
                    }

                    return false;
                },
                seed: start,
                iter: MinimizingIterations,
                threads: 1,
                print: show);
        }
        catch (CsCheckException)
        {
            // Sample reports the failure it has found; the smallest case is kept above.
        }

        return smallest;
    }

    private static string Shown(string text) =>
        text.Length <= LongestShownCase
            ? text
            : string.Create(CultureInfo.InvariantCulture, $"{text[..LongestShownCase]}… ({text.Length} characters)");
}
