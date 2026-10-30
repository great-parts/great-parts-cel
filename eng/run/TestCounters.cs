using System.Globalization;
using System.IO;
using System.Xml;
using System.Xml.Linq;

namespace Great.Parts.Cel.Eng.Run;

/// <summary>Adds up the results of the xUnit.net XML reports that <c>eng/test</c> writes, for the last line of the command.</summary>
internal static class TestCounters
{
    /// <summary>The reports of the runs under JIT, one per test project and target.</summary>
    internal const string JitDirectory = "artifacts/test-results/jit";

    /// <summary>The reports of the runs of the Native AOT executables, one per published test project and target.</summary>
    internal const string AotDirectory = "artifacts/test-results/aot";

    /// <summary>The counters of the two kinds of runs, or <see langword="null"/> when no report was written.</summary>
    internal static string? Summary()
    {
        string? jit = Describe(JitDirectory);
        string? aot = Describe(AotDirectory);
        return jit is null && aot is null ? null : $"tests: jit {jit ?? "no report"}; aot {aot ?? "no report"}";
    }

    // "2280 passed, 0 failed, 0 skipped in 9 reports", or null when the directory has no report.
    private static string? Describe(string directory)
    {
        if (!Directory.Exists(directory))
        {
            return null;
        }

        string[] reports = Directory.GetFiles(directory, "report.xml", SearchOption.AllDirectories);
        if (reports.Length == 0)
        {
            return null;
        }

        int[] totals = new int[3];
        foreach (string report in reports)
        {
            int[] counts = Read(report);
            for (int index = 0; index < totals.Length; index++)
            {
                totals[index] += counts[index];
            }
        }

        return string.Create(CultureInfo.InvariantCulture, $"{totals[0]} passed, {totals[1]} failed, {totals[2]} skipped in {reports.Length} reports");
    }

    // Passed, failed and skipped of the assemblies of one report; zeros when the report cannot be read.
    private static int[] Read(string report)
    {
        int[] counts = new int[3];
        try
        {
            foreach (XElement assembly in XDocument.Load(report).Descendants("assembly"))
            {
                counts[0] += Count(assembly, "passed");
                counts[1] += Count(assembly, "failed") + Count(assembly, "errors");
                counts[2] += Count(assembly, "skipped");
            }
        }
        catch (XmlException)
        {
            return new int[3];
        }

        return counts;
    }

    private static int Count(XElement assembly, string attribute) =>
        int.TryParse((string?)assembly.Attribute(attribute), NumberStyles.None, CultureInfo.InvariantCulture, out int count) ? count : 0;
}
