using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Great.Parts.Cel.Generator.Tests.Examples;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Generator.Tests.Output;

// The constants that the generator computes for the charges of a read ("Charges of a read" of the page of the created code): the size S of
// a structure that the read boxes, in bytes, without padding, and the formulas that the created code returns for a set and for a collection
// without an index. The test compiles types whose size is known, creates the created code, and asks the description for the units of each
// member (FieldReadCost, which the evaluation calls with a cost limit), so the constant and the formula are checked in one step; and reads
// the structures that are collections of the families that the samples have no member for.
public sealed class CostConstantsTests
{
    private const string Types = """
        using System;
        using System.Collections;
        using System.Collections.Generic;
        using System.Text.Json;
        using Great.Parts.Cel.Values;
        namespace Acme;
        [CelObject(typeof(Holder))] public partial class Ctx : CelTypeContext { }
        public struct S64 { public long A, B, C, D, E, F, G, H; }
        public struct S65 { public long A, B, C, D, E, F, G, H; public bool Z; }
        public struct Nest { public S64 Inner; public Guid Id; }
        public struct WithNullable { public int? N; }
        public struct Empty { }
        public readonly struct Bag : IReadOnlyCollection<long>
        {
            private readonly long a;
            public Bag(long a) { this.a = a; }
            public int Count => 3;
            public IEnumerator<long> GetEnumerator() { yield return a; yield return a + 1; yield return a + 2; }
            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }
        public class Holder
        {
            public S64 Sixty { get; set; }
            public S65 Over { get; set; }
            public Nest Nested { get; set; }
            public S65? OptOver { get; set; }
            public Empty Nothing { get; set; }
            public WithNullable Optional { get; set; }
            public JsonElement Json { get; set; }
            public Bag Box { get; set; }
            public HashSet<long> Numbers { get; set; } = new();
            public HashSet<string> Texts { get; set; } = new();
            public HashSet<Guid> Ids { get; set; } = new();
            public Queue<string> Queue { get; set; } = new();
            public CelValue? Maybe { get; set; }
            public JsonElement? MaybeJson { get; set; }
        }
        """;

    private static readonly MethodInfo CostMethod = Method("FieldReadCost");
    private static readonly MethodInfo ReadMethod = Method("GetField");
    private static readonly MethodInfo HasMethod = Method("HasField");

    private static MethodInfo Method(string name) =>
        typeof(CelObjectType).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)!;

    // The structures of known sizes: 8 longs are 64 bytes, a ninth byte makes it 65, a structure in a structure adds its own, a
    // Nullable<X> is X and one byte, an empty structure is one byte; the units are 1 + ceil(S / 64).
    [Theory]
    [Trait("Requirement", "R-3")]
    [InlineData("Sixty", 2)]
    [InlineData("Over", 3)]
    [InlineData("Nested", 3)]
    [InlineData("OptOver", 3)]
    [InlineData("Nothing", 2)]
    [InlineData("Optional", 2)]
    [InlineData("Json", 2)]
    public static void StructureThatTheReadBoxes_ChargesOnePlusTheBlocksOfSixtyFourBytes(string member, long units)
    {
        using var loaded = new GeneratorExample.Loaded(Types);
        (CelObjectType type, object holder) = Open(loaded);
        if (member == "OptOver")
        {
            Set(holder, member, Activator.CreateInstance(loaded.Assembly.GetType("Acme.S65")!));
        }

        Assert.Equal(units, Cost(type, holder, member));
    }

    // A set of n elements is 1 + c * n * bits(n); c is 1 for numbers and 1 + ceil(2 * Lmax / 10) for string, bytes and Guid.
    [Fact]
    [Trait("Requirement", "R-3")]
    public void Set_ChargesByTheFormulaOfItsFamilyAndTheLengthOfItsLongestElement()
    {
        using var loaded = new GeneratorExample.Loaded(Types);
        (CelObjectType type, object holder) = Open(loaded);

        Assert.Equal(1, Cost(type, holder, "Numbers"));
        Assert.Equal(1, Cost(type, holder, "Texts"));
        Set(holder, "Numbers", new HashSet<long> { 1, 2, 3, 4, 5, 6, 7 });
        Set(holder, "Texts", new HashSet<string> { "abcdefghij", "a", "bb" });
        Set(holder, "Ids", new HashSet<Guid> { Guid.NewGuid(), Guid.NewGuid() });
        Assert.Equal(22, Cost(type, holder, "Numbers"));
        Assert.Equal(19, Cost(type, holder, "Texts"));
        Assert.Equal(37, Cost(type, holder, "Ids"));
    }

    // A collection without an index is 1 + n; a member that is null is 0.
    [Fact]
    [Trait("Requirement", "R-3")]
    public void CollectionWithoutAnIndex_ChargesOnePlusItsSize_AndNullChargesNothing()
    {
        using var loaded = new GeneratorExample.Loaded(Types);
        (CelObjectType type, object holder) = Open(loaded);
        Set(holder, "Queue", new Queue<string>(["a", "b", "c", "d"]));

        Assert.Equal(5, Cost(type, holder, "Queue"));
        Set(holder, "Queue", null);
        Set(holder, "Numbers", null);
        Assert.Equal(0, Cost(type, holder, "Queue"));
        Assert.Equal(0, Cost(type, holder, "Numbers"));
    }

    // A structure that is a collection and implements only IReadOnlyCollection<T>: the box of the structure and the copy of the family,
    // each with its own "1 +"; the read has no test for null, has() is true, and the value is the list of the elements in the order of
    // enumeration.
    [Fact]
    [Trait("Requirement", "R-3")]
    [Trait("Requirement", "F-2")]
    public void StructureThatIsACollectionWithoutAnIndex_IsBoxedAndCopied()
    {
        using var loaded = new GeneratorExample.Loaded(Types);
        (CelObjectType type, object holder) = Open(loaded);
        Set(holder, "Box", Activator.CreateInstance(loaded.Assembly.GetType("Acme.Bag")!, 10L));

        Assert.Equal((1 + 1) + (1 + 3), Cost(type, holder, "Box"));
        Assert.True((bool)HasMethod.Invoke(type, [holder, Number(type, "Box")])!);
        var value = (CelValue)ReadMethod.Invoke(type, [holder, Number(type, "Box")])!;
        Assert.True(value.TryGetList(out CelList? list));
        long[] numbers = [.. Enumerable.Range(0, list!.Count).Select(i => list[i].TryGetInt64(out long number) ? number : -1)];
        Assert.Equal([10L, 11L, 12L], numbers);
    }

    // Nullable<CelValue> and Nullable<JsonElement>: an empty value is the CEL null and has() is false; a value is read as it is.
    [Fact]
    [Trait("Requirement", "F-2")]
    public void NullableCelValueAndNullableJsonElement_AreNullAndAbsentWhenEmpty()
    {
        using var loaded = new GeneratorExample.Loaded(Types);
        (CelObjectType type, object holder) = Open(loaded);

        foreach (string member in new[] { "Maybe", "MaybeJson" })
        {
            Assert.False((bool)HasMethod.Invoke(type, [holder, Number(type, member)])!);
            Assert.Equal(CelValueKind.Null, ((CelValue)ReadMethod.Invoke(type, [holder, Number(type, member)])!).Kind);
        }

        Set(holder, "Maybe", CelValue.FromInt64(5));
        Assert.True((bool)HasMethod.Invoke(type, [holder, Number(type, "Maybe")])!);
        Assert.Equal(CelValue.FromInt64(5), (CelValue)ReadMethod.Invoke(type, [holder, Number(type, "Maybe")])!);
    }

    private static (CelObjectType Type, object Holder) Open(GeneratorExample.Loaded loaded) =>
        (loaded.Provider("Acme.Ctx").ObjectTypes.Single(type => type.Name == "Acme.Holder"), loaded.New("Acme.Holder"));

    private static int Number(CelObjectType type, string member) =>
        type.Fields.Single(field => field.Name == char.ToLowerInvariant(member[0]) + member[1..]).Number;

    private static long Cost(CelObjectType type, object holder, string member) =>
        (long)CostMethod.Invoke(type, [holder, Number(type, member)])!;

    private static void Set(object holder, string member, object? value) =>
        holder.GetType().GetProperty(member)!.SetValue(holder, value);
}
