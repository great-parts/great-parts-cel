using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using Great.Parts.Cel.Generator.Samples.Families;
using Xunit;

namespace Great.Parts.Cel.Properties.Evaluation;

// The memory of the rows of data of the application that the generator describes, over the sample class Collections of the
// generator samples (docs/design/tests.md, "Cost and memory of the extension sets"): the views of lists and of dictionaries, the
// sorted copy of a set at each read, the copy of a collection without an index, the box of a structure and the traversal of nested
// values. The bytes that an evaluation allocates beyond its frame are at most the units it charges times B — the units of the read
// that the created code adds with `FieldReadCost` included, since the limit is `ulong.MaxValue` — on the sizes of CostRows, through
// Compile and through the planner without type checking.
public sealed class GeneratedDataCostTests
{
    private static readonly CelEnvironment DataEnvironment = new CelEnvironmentBuilder(CelStandardLibrary.V1)
        .AddTypes(CollectionContext.Default)
        .AddVariable<Collections>("c")
        .Build()
        .GetEnvironmentOrThrow();

    private static readonly CostTable Table = new CostTable(DataEnvironment)
        // Lists: a view over the source, and the structure that is boxed.
        .Add("generated, List<long>, read", size => Bound("c.mutable", new Collections { Mutable = Longs(size) }))
        .Add("generated, List<long>, traversal", size => Bound("c.mutable.all(v, v >= 0)", new Collections { Mutable = Longs(size) }))
        .Add("generated, long[], read", size => Bound("c.array", new Collections { Array = Longs(size).ToArray() }))
        .Add("generated, ImmutableArray<long>, read", size => Bound("c.immutable", new Collections { Immutable = ImmutableArray.CreateRange(Longs(size)) }))
        .Add("generated, ImmutableArray<long>, traversal", size => Bound("c.immutable.all(v, v >= 0)", new Collections { Immutable = ImmutableArray.CreateRange(Longs(size)) }))
        .Add("generated, structure that is a list", _ => Bound("c.fixed", new Collections { Fixed = new Triple(1, 2, 3) }))

        // Sets: the sorted copy that each read makes.
        .Add("generated, HashSet<long>", size => Bound("c.hash", new Collections { Hash = new HashSet<long>(Longs(size)) }))
        .Add("generated, IReadOnlySet<long>", size => Bound("c.readOnlySet", new Collections { ReadOnlySet = new HashSet<long>(Longs(size)) }))
        .Add("generated, ImmutableHashSet<long>", size => Bound("c.immutableSet", new Collections { ImmutableSet = Longs(size).ToImmutableHashSet() }))
        .Add("generated, SortedSet<string>", size => Bound("c.sorted", new Collections { Sorted = new SortedSet<string>(Keys(size)) }))

        // Collections without an index: the copy that each read makes.
        .Add("generated, Queue<long>", size => Bound("c.queue", new Collections { Queue = new Queue<long>(Longs(size)) }))
        .Add("generated, Stack<string>", size => Bound("c.stack", new Collections { Stack = new Stack<string>(Keys(size)) }))
        .Add("generated, LinkedList<long>", size => Bound("c.linked", new Collections { Linked = new LinkedList<long>(Longs(size)) }))

        // Dictionaries: the view, and the enumeration that is in the order of the source or by ascending key.
        .Add("generated, Dictionary<string, long>, read", size => Bound("c.index", new Collections { Index = Pairs(size) }))
        .Add("generated, Dictionary<string, long>, traversal", size => Bound("c.index.all(k, k != 'zz')", new Collections { Index = Pairs(size) }))
        .Add("generated, SortedDictionary<string, long>, traversal", size => Bound("c.sortedIndex.all(k, k != 'zz')", new Collections { SortedIndex = new SortedDictionary<string, long>(Pairs(size)) }))
        .Add("generated, Dictionary<long, string>, traversal", size => Bound("c.byNumber.all(k, k >= 0)", new Collections { ByNumber = Longs(size).ToDictionary(number => number, number => Key((int)number)) }))
        .Add("generated, Dictionary<Guid, long>, traversal", size => Bound("c.byGuid.all(k, k != '')", new Collections { ByGuid = Longs(size).ToDictionary(number => new Guid((int)number, 0, 0, new byte[8]), number => number) }))

        // Values nested in values.
        .Add("generated, nested lists, traversal", size => Bound("c.nested.all(l, l.size() >= 0)", new Collections { Nested = [.. Enumerable.Range(0, size).Select(_ => Longs(3))] }))
        .Add("generated, objects in a list, traversal", size => Bound("c.lines.all(l, l.qty >= 0)", new Collections { Lines = [.. Enumerable.Range(0, size).Select(index => new Line { Sku = Key(index), Qty = index })] }));

    public static TheoryData<string> RowNames() => Table.Names();

    [Theory]
    [Trait("Requirement", "R-3")]
    [MemberData(nameof(RowNames))]
    public static void Row_AllocatesWithinCost(string row) => Table.Check(row);

    private static (string, CelBindings) Bound(string text, Collections collections)
    {
        var bindings = new CelBindings();
        bindings.Add("c", collections);
        return (text, bindings);
    }

    // The numbers 0, 1, … of the size.
    private static List<long> Longs(int size) => [.. Enumerable.Range(0, size).Select(index => (long)index)];

    // The keys "0000", "0001", … of the size.
    private static List<string> Keys(int size) => [.. Enumerable.Range(0, size).Select(Key)];

    private static string Key(int index) => index.ToString("D4", CultureInfo.InvariantCulture);

    // The pairs of the keys "0000", "0001", … and the value 1.
    private static Dictionary<string, long> Pairs(int size) => Keys(size).ToDictionary(key => key, _ => 1L);
}
