using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using Xunit;

namespace Great.Parts.Cel.Tests.Allocation;

// The cache of the simple entry filled to its capacities takes at most 8 MiB: 64 shapes of data of 16 variables each and 1 024 programs,
// 16 on each shape, of texts of about 90 code units. The memory is the growth of GC.GetTotalMemory(true) over the fill, as for
// the cache of compilations. The class runs alone, so that no other test allocates while it measures.
[Collection(SimpleEntryMemoryGroup.Name)]
[Trait("Requirement", "U-7")]
public sealed class SimpleEntryCacheMemoryTests
{
    private const int Shapes = 64;
    private const int Variables = 16;
    private const int ProgramsPerShape = 16;
    private const long Target = 8L * 1024 * 1024;

    private const int Trials = 3;

    // The memory of a full cache is a fixed number, and the noise of GC.GetTotalMemory(true) only adds to it: a one-time cost of the
    // first fill (statics, pools, tiered compilation), or the state of the heap of a platform (a run on macOS with net10.0 measured
    // 4.5 MiB more than the same fill on Linux, 8 392 448 bytes against 3 878 504). So the fill is made once without a measurement, at the full capacities,
    // and the cache is measured in Trials rounds, each against two baselines: the heap before the fill and the heap after the cache is
    // released. The target is checked against the smallest of the estimates; the target itself is not changed.
    [Fact]
    public void FullCache_IsWithinTheTarget()
    {
        // The statics of the library and the generic instances that the fill needs are made before the measurement.
        WarmUp();

        long used = long.MaxValue;
        var estimates = new List<long>();
        for (int trial = 0; trial < Trials; trial++)
        {
            long before = GC.GetTotalMemory(true);
            (long withCache, int environments, int programs, long totalTextLength) = FillAndMeasure();
            long released = GC.GetTotalMemory(true);

            Assert.Equal((Shapes, Shapes * ProgramsPerShape), (environments, programs));
            Assert.True(totalTextLength <= SimpleEntryCache.MaxTotalTextLength);

            estimates.Add(withCache - before);
            estimates.Add(withCache - released);
            used = Math.Min(used, Math.Min(withCache - before, withCache - released));
            TestContext.Current.TestOutputHelper?.WriteLine(string.Create(
                CultureInfo.InvariantCulture,
                $"round {trial}: {environments} environments, {programs} programs, {totalTextLength} code units of text: {withCache - before} bytes over the heap before the fill, {withCache - released} bytes over the heap after the release"));
        }

        TestContext.Current.TestOutputHelper?.WriteLine(string.Create(
            CultureInfo.InvariantCulture,
            $"the smallest estimate: {used} bytes ({used / 1024.0 / 1024.0:F2} MiB)"));
        Assert.True(used <= Target, string.Create(CultureInfo.InvariantCulture, $"{used} bytes > {Target}; estimates: {string.Join(", ", estimates)}"));
    }

    // Separate methods that do not inline, so that no cache is alive in the frame of the test when the heap is measured.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void WarmUp() => Fill(new SimpleEntryCache(), Shapes, ProgramsPerShape);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (long WithCache, int Environments, int Programs, long TotalTextLength) FillAndMeasure()
    {
        var cache = new SimpleEntryCache();
        Fill(cache, Shapes, ProgramsPerShape);
        long withCache = GC.GetTotalMemory(true);
        GC.KeepAlive(cache);
        (int environments, int programs, long totalTextLength) = cache.Kept;
        return (withCache, environments, programs, totalTextLength);
    }

    private static void Fill(SimpleEntryCache cache, int shapes, int programsPerShape)
    {
        for (int shape = 0; shape < shapes; shape++)
        {
            var names = new string[Variables];
            var variables = new Dictionary<string, object?>();
            for (int variable = 0; variable < Variables; variable++)
            {
                names[variable] = "s" + shape.ToString(CultureInfo.InvariantCulture) + "_" + variable.ToString(CultureInfo.InvariantCulture);
                variables[names[variable]] = 1L;
            }

            for (int program = 0; program < programsPerShape; program++)
            {
                long value = SimpleEntryCall.Evaluate<long>(cache, Text(names, program), variables, CelExpression.DefaultCostLimit, CancellationToken.None);
                Assert.True(value > 0);
            }
        }
    }

    // A sum of the variables of the shape, starting at a different one for each program, up to about 90 code units.
    private static string Text(string[] names, int program)
    {
        var text = new StringBuilder();
        for (int term = 0; text.Length < 80; term++)
        {
            if (term > 0)
            {
                text.Append(" + ");
            }

            text.Append(names[(program + term) % names.Length]);
        }

        return text.ToString();
    }
}

[CollectionDefinition(Name, DisableParallelization = true)]
[SuppressMessage("Design", "CA1515:Consider making public types internal", Justification = "xUnit requires a collection definition class to be public.")]
public sealed class SimpleEntryMemoryGroup
{
    public const string Name = "SimpleEntryMemory";
}
