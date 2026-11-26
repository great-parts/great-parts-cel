using System.Text.Json;
using System.Text.Json.Serialization;
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
/// The JSON of the verification, written by the source generator of System.Text.Json: the reflection-based serializer is not
/// available in a Native AOT process, in which the rig verifies the libraries for a measurement under Native AOT.
/// </summary>
[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(VerifyRow[]))]
internal sealed partial class VerifyJson : JsonSerializerContext
{
}

/// <summary>
/// Verification before the measurement, against the expected value of the case:
/// a library whose result in a case is wrong or an error does not take part in that case.
/// </summary>
public static class Verification
{
    /// <summary>
    /// The environment variable that names a file of verification rows (verify.json). When it is set, the rows come from that file and
    /// not from a run: the rig sets it for a measurement under Native AOT, where the host of BenchmarkDotNet and the benchmark process
    /// must list the same targets in the same order, and the file is the verification that ran in a Native AOT process.
    /// </summary>
    public const string VerifiedFileVariable = "BENCH_VERIFIED";

    private static readonly string[] VariantNames =
    [
        "original text: compile, evaluate",
        "text with tail: compile, evaluate",
        "text with tail: full cycle",
    ];

    private static readonly Lazy<IReadOnlyList<VerifyRow>> Rows = new(Load);

    public static IReadOnlyList<VerifyRow> RunAll() =>
        [.. Scenarios.All.SelectMany(s => Libraries.Names.Select(n => Run(n, s)))];

    /// <summary>The rows of one library on every case.</summary>
    public static IReadOnlyList<VerifyRow> RunLibrary(string name) => [.. Scenarios.All.Select(s => Run(name, s))];

    /// <summary>
    /// The rows of a library that was not run (its process ended abnormally): every variant carries the reason as its result, so the
    /// library is excluded from every case like a library whose result is wrong.
    /// </summary>
    public static IReadOnlyList<VerifyRow> ExcludeLibrary(string name, string reason) => [.. Scenarios.All.Select(s => Excluded(name, s, reason))];

    /// <summary>
    /// Runs one library on one case. A library that cannot be created (its type initializer throws, as a library that does not work
    /// under Native AOT may do) is excluded from the case with the error as the result.
    /// </summary>
    public static VerifyRow Run(string name, Scenario s)
    {
        ILibraryAdapter library;
        try
        {
            library = Libraries.Get(name);
        }
        catch (Exception ex)
        {
            return Excluded(name, s, Describe(ex));
        }

        return Run(library, s);
    }

    public static VerifyRow Run(ILibraryAdapter library, Scenario s)
    {
        var expected = Scenarios.Expected(s);
        var order = Scenarios.Order(s.Lines);
        var text = library.Rewrite(s.Expression);
        var variants = new List<VariantResult>
        {
            Variant(VariantNames[0], expected, library, () =>
                library.Evaluate(library.Compile(text), order, Scenarios.Now)),
            Variant(VariantNames[1], expected, library, () =>
                library.Evaluate(library.Compile(ExpressionText.Next(text)), order, Scenarios.Now)),
            Variant(VariantNames[2], expected, library, () =>
                library.FullCycle(ExpressionText.Next(text), order, Scenarios.Now)),
        };
        return new VerifyRow(library.Name, s.Id, s.Expression, s.Lines, expected, variants, variants.All(v => v.Correct));
    }

    private static VerifyRow Excluded(string library, Scenario s, string reason)
    {
        var variants = VariantNames.Select(v => new VariantResult(v, reason, false)).ToList();
        return new VerifyRow(library, s.Id, s.Expression, s.Lines, Scenarios.Expected(s), variants, false);
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
            result = Describe(ex);
        }

        return new VariantResult(name, result, result == expected);
    }

    // The error that a type initializer threw is the cause the reader needs, not the wrapper.
    private static string Describe(Exception ex)
    {
        var cause = ex is TypeInitializationException { InnerException: { } inner } ? inner : ex;
        return "ERROR(" + cause.GetType().Name + ": " + cause.Message + ")";
    }

    private static IReadOnlyList<VerifyRow> Load()
    {
        var path = Environment.GetEnvironmentVariable(VerifiedFileVariable);
        return string.IsNullOrEmpty(path) ? RunAll() : ReadJson(path);
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
        File.WriteAllText(path, JsonSerializer.Serialize(rows.ToArray(), VerifyJson.Default.VerifyRowArray));
    }

    public static IReadOnlyList<VerifyRow> ReadJson(string path) =>
        JsonSerializer.Deserialize(File.ReadAllText(path), VerifyJson.Default.VerifyRowArray)
        ?? throw new InvalidOperationException("no verification rows in " + path);
}
