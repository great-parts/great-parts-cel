using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using Great.Parts.Cel.Evaluation;
using Great.Parts.Cel.Generator.Samples.Families;
using Great.Parts.Cel.Generator.Samples.Rows;
using Great.Parts.Cel.Tests.Examples.Generated;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Allocation;

// The bytes that the created code allocates for the read of a member, by row ("Cost and allocation" of the page of the created code):
// a scalar, a string, bytes, a time and an object of a sealed class allocate nothing; a view of a list or a map is one object of at most
// 64 bytes, and `ImmutableArray<T>` is read without a box; a structure that is a list is boxed and has the view, two objects; the
// string of a `Guid` is the one allocation of its row; the copy of a set is the one that a set pays for. The measure is the smallest
// bytes of Run in three evaluations after two that are not counted, the way the allocation test of the evaluation takes it. The order of
// the samples is the test of the examples; this one takes the rows that the order does not read.
public sealed class GeneratedRowAllocationTests
{
    private const ulong ViewBytes = 64;

    private static readonly CelEnvironment Environment = GeneratedExample.Builder(ScalarContext.Default, CollectionContext.Default)
        .AddVariable<Scalars>("s")
        .AddVariable<Oblivious>("o")
        .AddVariable<Collections>("c")
        .Build()
        .GetEnvironmentOrThrow();

    private static readonly CelBindings Bindings = new CelBindings()
        .Add(
            "s",
            new Scalars
            {
                Int64 = 5,
                Double = 2.5,
                Bool = true,
                Text = "abc",
                Id = Guid.NewGuid(),
                Blob = [1, 2, 3],
                Memory = new byte[] { 1, 2 },
                Moment = new DateTimeOffset(2026, 11, 1, 12, 0, 0, TimeSpan.Zero),
                Elapsed = TimeSpan.FromMinutes(5),
                OptInt = 3,
            })
        .Add("o", new Oblivious { Text = "x", Blob = [1], Any = "y", Inner = new Scalars() })
        .Add(
            "c",
            new Collections
            {
                Mutable = [1, 2, 3],
                Immutable = [1, 2, 3],
                Fixed = new Triple(1, 2, 3),
                Index = new Dictionary<string, long> { ["a"] = 1 },
                Hash = [1, 2, 3, 4, 5, 6, 7],
                Queue = new Queue<long>([1, 2, 3]),
            });

    public static TheoryData<string> Free =>
    [
        "s.int64", "s.int32", "s.double", "s.bool", "s.text", "s.blob", "s.memory", "s.moment", "s.elapsed", "s.optInt", "o.inner", "o.text", "o.any",
    ];

    [Theory]
    [Trait("Requirement", "P-2")]
    [MemberData(nameof(Free))]
    public static void ReadOfAScalarAStringBytesATimeAndASealedObject_AllocatesNothing(string text) =>
        Assert.Equal(0, Allocated(text));

    [Theory]
    [Trait("Requirement", "P-2")]
    [InlineData("c.mutable", 1)]
    [InlineData("c.immutable", 1)]
    [InlineData("c.index", 1)]
    [InlineData("c.fixed", 2)]
    public static void ReadOfAViewOrABox_AllocatesAtMostTheObjectsOfItsRow(string text, int objects) =>
        Assert.InRange(Allocated(text), 1, (long)ViewBytes * objects);

    // The string of a Guid is the one allocation of its row: 36 code units with the header, the length and the terminator.
    [Fact]
    [Trait("Requirement", "P-2")]
    public void ReadOfAGuid_AllocatesTheStringOfItsRow()
    {
        long bytes = Allocated("s.id");

        Assert.InRange(bytes, 36 * 2, 96);
    }

    // The copy of a set is made at each read: its array of values and the view over it, and the length of it is the size of the set.
    [Fact]
    [Trait("Requirement", "P-2")]
    public void ReadOfASet_AllocatesTheCopy()
    {
        long bytes = Allocated("c.hash");

        Assert.InRange(bytes, 7 * 32, (7 * 32) + 24 + (2 * (long)ViewBytes));
    }

    // The smallest bytes of Run in three evaluations of the text after two that are not counted.
    private static long Allocated(string text)
    {
        Plan plan = Environment.Compile<CelValue>(text).GetProgramOrThrow().Plan;
        IReadOnlyDictionary<string, CelValue> values = Bindings.Values;
        for (int i = 0; i < 2; i++)
        {
            Assert.False(plan.Run(plan.CreateFrame(values, null, CancellationToken.None)).Kind == CelValueKind.Error, text);
        }

        long smallest = long.MaxValue;
        for (int i = 0; i < 3; i++)
        {
            Frame frame = plan.CreateFrame(values, null, CancellationToken.None);
            long before = GC.GetAllocatedBytesForCurrentThread();
            CelValue value = plan.Run(frame);
            long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.False(value.Kind == CelValueKind.Error, string.Create(CultureInfo.InvariantCulture, $"{text}: {value}"));
            smallest = Math.Min(smallest, bytes);
        }

        return smallest;
    }
}
