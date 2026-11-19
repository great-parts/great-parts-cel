using System.Text.Json;
using SpeedBench.Adapters;

namespace SpeedBench;

/// <summary>Result of one way of running a case: its canonical result or the error.</summary>
public sealed record VariantResult(string Variant, string Result, bool Correct);

/// <summary>Verification of one library on one case: every variant must give the expected value.</summary>
public sealed record VerifyRow(
    string Library,
    string Scenario,
    string Expression,
    int Lines,
    string Expected,
    IReadOnlyList<VariantResult> Variants,
    bool Correct);

/// <summary>
/// Verification before the measurement, against the expected value of the case:
/// a library whose result in a case is wrong or an error does not take part in that case.
/// </summary>
public static class Verification
{
    private static readonly Lazy<IReadOnlyList<VerifyRow>> Rows = new(RunAll);

    public static IReadOnlyList<VerifyRow> RunAll() =>
        [.. Scenarios.All.SelectMany(s => Libraries.All.Select(l => Run(l, s)))];

    public static VerifyRow Run(ILibraryAdapter library, Scenario s)
    {
        var expected = Scenarios.Expected(s);
        var order = Scenarios.Order(s.Lines);
        var text = library.Rewrite(s.Expression);
        var variants = new List<VariantResult>
        {
            Variant("original text: compile, evaluate", expected, library, () =>
                library.Evaluate(library.Compile(text), order, Scenarios.Now)),
            Variant("text with tail: compile, evaluate", expected, library, () =>
                library.Evaluate(library.Compile(ExpressionText.Next(text)), order, Scenarios.Now)),
            Variant("text with tail: full cycle", expected, library, () =>
                library.FullCycle(ExpressionText.Next(text), order, Scenarios.Now)),
        };
        return new VerifyRow(library.Name, s.Id, s.Expression, s.Lines, expected, variants, variants.All(v => v.Correct));
    }

    private static VariantResult Variant(string name, string expected, ILibraryAdapter library, Func<object?> run)
    {
        string result;
        try
        {
            result = library.Canonical(run());
        }
        catch (Exception ex)
        {
            result = "ERROR(" + ex.GetType().Name + ": " + ex.Message + ")";
        }

        return new VariantResult(name, result, result == expected);
    }

    /// <summary>Benchmark targets "Library:Case" of the selected cases where the library gives the expected value.</summary>
    public static IEnumerable<string> MeasuredTargets()
    {
        var selected = Scenarios.Selected().Select(s => s.Id).ToHashSet();
        return Rows.Value.Where(r => r.Correct && selected.Contains(r.Scenario)).Select(r => Target(r.Library, r.Scenario));
    }

    public static string Target(string library, string scenario) => library + ":" + scenario;

    public static (ILibraryAdapter Library, Scenario Scenario) ParseTarget(string target)
    {
        var colon = target.IndexOf(':', StringComparison.Ordinal);
        return (Libraries.Get(target[..colon]), Scenarios.Get(target[(colon + 1)..]));
    }

    public static void WriteJson(string path, IReadOnlyList<VerifyRow> rows)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        File.WriteAllText(path, JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true }));
    }
}
