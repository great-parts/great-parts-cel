using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Great.Parts.Cel.Generator.Tests.Examples;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Great.Parts.Cel.Generator.Tests.Limit;

// The calibration of the limit of the closure, 250 types ("Closure and the limit" of the page of the generator). It is an explicit test: it
// runs only when it is asked for, and is no gate of the regular run. For each N of the series and for two shapes of the graph — a chain,
// where each of N types has ten members of mixed kinds and refers to the next one, and a dense graph, where each type refers to five
// others — it builds a compilation in memory, runs the generator twice to warm up, and then ten times, each after the edit that an editor
// faces: one member of one type changes its type, so the reading of the context runs whole and the model is modified. It measures the wall
// time and the bytes allocated by the thread of the run and writes the median of the ten, with the verdict of the rule of the page, to
// artifacts/limit-calibration.txt of the working copy and to the output of the test.
public sealed class LimitCalibration
{
    private const int Warmups = 2;
    private const int Runs = 10;
    private const double LimitMilliseconds = 250;
    private const double LimitMegabytes = 100;

    // The limit of the closure that the page of the generator names; the series goes past it to show the cost of the stop.
    private const int Limit = 250;

    private static readonly int[] Series = [100, 250, 500, 1000, 2000];

    private static readonly string[] Shapes = ["chain", "dense"];

    [Fact(Explicit = true)]
    [Trait("Requirement", "F-8")]
    public void LimitOfTheClosure_IsCalibratedByTheMedianTimeAndAllocation()
    {
        var report = new StringBuilder();
        var medians = new Dictionary<(string Shape, int N), (double Ms, double Mb)>();

        // The process is warm before the series: the first runs of a process are the compiler and the generator compiled for the first
        // time, which no edit of an editor pays.
        foreach (string shape in Shapes)
        {
            _ = Measure(shape, 250);
        }

        foreach (string shape in Shapes)
        {
            foreach (int n in Series)
            {
                (double ms, double mb, double generatorMs, bool tooLarge) = Measure(shape, n);
                medians[(shape, n)] = (ms, mb);
                report.AppendLine(string.Create(CultureInfo.InvariantCulture, $"{shape} N={n}: median {ms:F1} ms (generator {generatorMs:F1} ms), {mb:F2} MB allocated, GPCEL009 {(tooLarge ? "yes" : "no")}"));
                Assert.Equal(n > Limit, tooLarge);
            }
        }

        bool stays = Shapes.All(shape => medians[(shape, Limit)].Ms <= LimitMilliseconds && medians[(shape, Limit)].Mb <= LimitMegabytes);
        int fits = Series.Where(n => n < Limit && Shapes.All(shape => medians[(shape, n)].Ms <= LimitMilliseconds && medians[(shape, n)].Mb <= LimitMegabytes)).DefaultIfEmpty(0).Max();
        report.AppendLine(stays
            ? string.Create(CultureInfo.InvariantCulture, $"verdict: the limit stays {Limit} (at N = {Limit} both shapes are within {LimitMilliseconds} ms and {LimitMegabytes} MB)")
            : string.Create(CultureInfo.InvariantCulture, $"verdict: the limit changes to the largest value of the series that both shapes meet: {(fits > 0 ? fits.ToString(CultureInfo.InvariantCulture) : "none of the values of the series below the limit")}"));
        string text = report.ToString();
        TestContext.Current.TestOutputHelper?.WriteLine(text);
        string? root = RepositoryRoot();
        if (root is not null)
        {
            Directory.CreateDirectory(Path.Combine(root, "artifacts"));
            File.WriteAllText(Path.Combine(root, "artifacts", "limit-calibration.txt"), text);
        }
    }

    // The median time in milliseconds and the median bytes in megabytes of ten runs after an edit, the median of the time that the driver
    // reports for the generator itself, and whether the context is too large.
    private static (double Ms, double Mb, double GeneratorMs, bool TooLarge) Measure(string shape, int n)
    {
        string original = Source(shape, n, "long");
        CSharpCompilation compilation = GeneratorExample.Compile(original);
        SyntaxTree tree = compilation.SyntaxTrees[0];
        var options = new CSharpParseOptions(LanguageVersion.CSharp12);
        GeneratorDriver driver = GeneratorExample.Driver();
        driver = driver.RunGenerators(compilation);

        var times = new List<double>();
        var bytes = new List<double>();
        var generatorTimes = new List<double>();
        bool tooLarge = false;
        for (int i = 0; i < Warmups + Runs; i++)
        {
            string edited = Source(shape, n, i % 2 == 0 ? "int" : "long");
            CSharpCompilation changed = compilation.ReplaceSyntaxTree(tree, CSharpSyntaxTree.ParseText(edited, options, path: "Source.cs"));
            long before = GC.GetAllocatedBytesForCurrentThread();
            long start = Stopwatch.GetTimestamp();
            driver = driver.RunGenerators(changed);
            double ms = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            GeneratorDriverRunResult result = driver.GetRunResult();
            tooLarge = result.Diagnostics.Any(diagnostic => diagnostic.Id == "GPCEL009");
            if (i >= Warmups)
            {
                times.Add(ms);
                bytes.Add(allocated / (1024.0 * 1024.0));
                generatorTimes.Add(driver.GetTimingInfo().ElapsedTime.TotalMilliseconds);
            }

            // The next run starts from the edited tree, as an editor does: the other variant is the next edit.
            compilation = changed;
            tree = changed.SyntaxTrees[0];
        }

        return (Median(times), Median(bytes), Median(generatorTimes), tooLarge);
    }

    private static double Median(List<double> values)
    {
        double[] sorted = [.. values.Order()];
        return sorted[sorted.Length / 2];
    }

    // The types T0 ... T{n-1}: ten members of mixed kinds each (the first of the type `first`) and the references of the shape.
    private static string Source(string shape, int n, string first)
    {
        var text = new StringBuilder(GeneratorExample.Preamble);
        text.Append("[CelObject(typeof(T0))] public partial class Ctx : CelTypeContext { }\n");
        for (int i = 0; i < n; i++)
        {
            text.Append("public class T").Append(i).Append(" { ");
            text.Append("public ").Append(i == 0 ? first : "long").Append(" A { get; set; } public string B { get; set; } = \"\"; public bool C { get; set; } public double D { get; set; } ");
            text.Append("public Guid E { get; set; } public DateTimeOffset F { get; set; } public List<long> G { get; set; } = new(); ");
            text.Append("public Dictionary<string, long> H { get; set; } = new(); public byte[] I { get; set; } = []; public TimeSpan J { get; set; } ");
            int references = shape == "chain" ? 1 : 5;
            for (int r = 1; r <= references; r++)
            {
                if (n > 1)
                {
                    text.Append("public T").Append((i + r) % n).Append(" R").Append(r).Append(" { get; set; } = null!; ");
                }
            }

            text.Append("}\n");
        }

        return text.ToString();
    }

    private static string? RepositoryRoot()
    {
        for (DirectoryInfo? directory = new(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Great.Parts.Cel.slnx")))
            {
                return directory.FullName;
            }
        }

        return null;
    }
}
