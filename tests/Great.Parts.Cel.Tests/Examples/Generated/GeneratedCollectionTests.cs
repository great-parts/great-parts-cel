using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Generator.Samples.Families;
using Great.Parts.Cel.Generator.Samples.Rows;
using Xunit;
using static Great.Parts.Cel.Tests.Examples.Generated.GeneratedExample;

namespace Great.Parts.Cel.Tests.Examples.Generated;

// The collection rows of the type mapping ("Objects and collections") read from members of a type that the generator described (the sample class
// Collections): lists are views over the source, sets are sorted copies, other collections are copies in the order of
// enumeration, dictionaries are views whose order is the order of the source for Dictionary and for the other sorted classes
// and the ascending key for a class the library does not know. A member declared with the writable interface is wrapped only
// for an instance that is not read-only. The place of a read is the "." of the selection.
public sealed class GeneratedCollectionTests
{
    private static readonly CelEnvironment Environment = Builder(CollectionContext.Default)
        .AddVariable<Collections>("c")
        .Build()
        .GetEnvironmentOrThrow();

    [Fact]
    [Trait("Requirement", "F-2")]
    [Trait("Requirement", "P-2")]
    public void Lists_AreReadInTheOrderOfTheSource()
    {
        var collections = new Collections
        {
            Array = [1, 2, 3],
            ReadOnly = ["a", "b"],
            Mutable = [4, 5],
            Immutable = [6, 7],
            ImmutableL = [8],
            ReadOnlyColl = new List<long> { 9 }.AsReadOnly(),
            AsIList = new List<long> { 10, 11 },
        };
        CelBindings bindings = new CelBindings().Add("c", collections);

        Assert.True(Evaluate<bool>(Environment, "c.array == [1, 2, 3] && c.readOnly == ['a', 'b'] && c.mutable == [4, 5]", bindings));
        Assert.True(Evaluate<bool>(Environment, "c.immutable == [6, 7] && c.immutableL == [8] && c.readOnlyColl == [9] && c.asIList == [10, 11]", bindings));
    }

    // A list is a view over the source, not a copy: what the application changes between two evaluations is read by the second.
    [Fact]
    [Trait("Requirement", "F-2")]
    [Trait("Requirement", "P-2")]
    public void List_IsAViewOverTheSource()
    {
        var collections = new Collections { Mutable = [1] };
        CelBindings bindings = new CelBindings().Add("c", collections);
        Assert.True(Evaluate<bool>(Environment, "c.mutable.size() == 1", bindings));

        collections.Mutable.Add(2);

        Assert.True(Evaluate<bool>(Environment, "c.mutable == [1, 2]", bindings));
    }

    // A member declared IList<T> with an instance that is not read-only is wrapped at run time; the wrapper is a view too.
    [Fact]
    [Trait("Requirement", "F-2")]
    public void IListThatIsNotReadOnly_IsWrappedAtRunTime()
    {
        var only = new OnlyList<long>(1, 2);
        var collections = new Collections { AsIList = only };
        CelBindings bindings = new CelBindings().Add("c", collections);

        Assert.True(Evaluate<bool>(Environment, "c.asIList == [1, 2]", bindings));
        only.Add(3);
        Assert.True(Evaluate<bool>(Environment, "c.asIList == [1, 2, 3]", bindings));
    }

    // A set is read as a list in the ascending order of CEL, whatever the hash codes say; the copy is made at each read.
    [Fact]
    [Trait("Requirement", "F-2")]
    [Trait("Requirement", "R-5")]
    public void Sets_AreReadSortedAscending()
    {
        var collections = new Collections
        {
            Hash = [30, 10, 20],
            Sorted = new SortedSet<string>(["b", "a"]),
            AsSet = new HashSet<string> { "z", "m", "a" },
            ReadOnlySet = new HashSet<long> { 3, 1, 2 },
            ImmutableSet = [9, 7, 8],
            Doubles = [2.5, -1.5, 0.0],
        };
        CelBindings bindings = new CelBindings().Add("c", collections);

        Assert.True(Evaluate<bool>(Environment, "c.hash == [10, 20, 30] && c.sorted == ['a', 'b'] && c.asSet == ['a', 'm', 'z']", bindings));
        Assert.True(Evaluate<bool>(Environment, "c.readOnlySet == [1, 2, 3] && c.immutableSet == [7, 8, 9] && c.doubles == [-1.5, 0.0, 2.5]", bindings));
    }

    // A set whose elements the type accepts and CEL refuses to order — NaN, null — is a data error of the application:
    // the library throws ArgumentException from CreateSorted, and the evaluation turns it into ApplicationException (3014) at the
    // selection, with the exception as its inner one. The same set without such elements is read.
    [Fact]
    [Trait("Requirement", "F-2")]
    [Trait("Requirement", "F-4")]
    public void SetThatCannotBeOrdered_IsApplicationExceptionAtTheSelection()
    {
        var nan = new Collections { Doubles = [1.0, double.NaN] };
        var withNull = new Collections { Texts = ["a", null] };

        CelError first = EvaluationError(Environment, "c.doubles", new CelBindings().Add("c", nan));
        CelError second = EvaluationError(Environment, "c.texts", new CelBindings().Add("c", withNull));

        AssertError(first, CelErrorCode.ApplicationException, 1);
        AssertError(second, CelErrorCode.ApplicationException, 1);
        Assert.IsType<ArgumentException>(first.Exception);
        Assert.IsType<ArgumentException>(second.Exception);
        Assert.True(Evaluate<bool>(Environment, "c.texts == ['a', 'b']", new CelBindings().Add("c", new Collections { Texts = ["b", "a"] })));
    }

    // Queue, Stack and LinkedList have a size and no index: a copy in the order of enumeration (a Stack — last in, first out).
    [Fact]
    [Trait("Requirement", "F-2")]
    public void OtherCollections_AreCopiesInTheOrderOfEnumeration()
    {
        var queue = new Queue<long>();
        queue.Enqueue(3);
        queue.Enqueue(1);
        queue.Enqueue(2);
        var stack = new Stack<string>();
        stack.Push("a");
        stack.Push("b");
        stack.Push("c");
        var collections = new Collections { Queue = queue, Stack = stack, Linked = new LinkedList<long>([5, 4]) };

        CelBindings bindings = new CelBindings().Add("c", collections);

        Assert.True(Evaluate<bool>(Environment, "c.queue == [3, 1, 2] && c.stack == ['c', 'b', 'a'] && c.linked == [5, 4]", bindings));
    }

    // The order of a Dictionary is the order of its source: the order of enumeration, which does not depend on the hash codes of
    // the keys for a dictionary without removals (the tests of the order of the dictionaries check that on both targets). The same for the other classes
    // that keep an order; for a read-only dictionary declared by its interface and for an IDictionary instance that is a
    // Dictionary the order is the source's too.
    [Fact]
    [Trait("Requirement", "F-2")]
    [Trait("Requirement", "R-5")]
    public void DictionaryOfTheSource_IsEnumeratedInTheOrderOfTheSource()
    {
        var collections = new Collections
        {
            Index = new Dictionary<string, long> { ["b"] = 2, ["a"] = 1, ["c"] = 3 },
            SortedIndex = new SortedDictionary<string, long> { ["c"] = 3, ["a"] = 1, ["b"] = 2 },
            ReadOnlyIndex = new Dictionary<string, long> { ["y"] = 1, ["x"] = 2 },
            AsIDictionary = new Dictionary<string, long> { ["z"] = 1, ["y"] = 2 },
        };
        CelBindings bindings = new CelBindings().Add("c", collections);

        Assert.True(Evaluate<bool>(Environment, "c.index.map(k, k) == ['b', 'a', 'c'] && c.index.b == 2 && c.index.size() == 3", bindings));
        Assert.True(Evaluate<bool>(Environment, "c.sortedIndex.map(k, k) == ['a', 'b', 'c']", bindings));
        Assert.True(Evaluate<bool>(Environment, "c.readOnlyIndex.map(k, k) == ['y', 'x'] && c.asIDictionary.map(k, k) == ['z', 'y']", bindings));
    }

    // A class that implements only IDictionary<K,V> is wrapped at run time and is a class the library does not know the order of:
    // the ascending key, so the result does not depend on how the class orders its entries.
    [Fact]
    [Trait("Requirement", "F-2")]
    [Trait("Requirement", "R-5")]
    public void DictionaryThatIsNotReadOnly_IsWrappedAndEnumeratedByAscendingKey()
    {
        var only = new OnlyDictionary<string, long> { ["z"] = 1, ["y"] = 2, ["x"] = 3 };
        var collections = new Collections { AsIDictionary = only };
        CelBindings bindings = new CelBindings().Add("c", collections);

        Assert.True(Evaluate<bool>(Environment, "c.asIDictionary.map(k, k) == ['x', 'y', 'z'] && c.asIDictionary.z == 1", bindings));
        only["w"] = 4;
        Assert.True(Evaluate<bool>(Environment, "c.asIDictionary.map(k, k) == ['w', 'x', 'y', 'z']", bindings));
    }

    // The keys of other kinds: an integer key is an int key, a Guid is its text, an enumeration is its int.
    [Fact]
    [Trait("Requirement", "F-2")]
    public void DictionaryKeys_AreStringIntegerGuidOrEnumeration()
    {
        var id = new Guid("0f8fad5b-d9cb-469f-a165-70867728950e");
        var collections = new Collections
        {
            ByNumber = new Dictionary<long, string> { [3] = "c", [1] = "a" },
            ByGuid = new Dictionary<Guid, long> { [id] = 7 },
            ByStatus = new Dictionary<Status, long> { [Status.Approved] = 5 },
        };
        CelBindings bindings = new CelBindings().Add("c", collections);

        Assert.True(Evaluate<bool>(Environment, "c.byNumber[3] == 'c' && c.byNumber.map(k, k) == [3, 1]", bindings));
        Assert.True(Evaluate<bool>(Environment, "c.byGuid['0f8fad5b-d9cb-469f-a165-70867728950e'] == 7 && c.byStatus[1] == 5", bindings));
    }

    // Elements and values are read at every depth by the rows of the element type: lists of lists, a dictionary of lists of
    // objects, a list of objects.
    [Fact]
    [Trait("Requirement", "F-2")]
    public void ElementsAndValues_AreReadAtEveryDepth()
    {
        var collections = new Collections
        {
            Nested = [[1, 2], [3]],
            Groups = new Dictionary<string, List<Line>> { ["x"] = [new Line { Sku = "A", Qty = 2 }, new Line { Sku = "B", Qty = 3 }] },
            Lines = [new Line { Sku = "C", Qty = 4 }],
        };
        CelBindings bindings = new CelBindings().Add("c", collections);

        Assert.True(Evaluate<bool>(Environment, "c.nested == [[1, 2], [3]] && c.nested[0].size() == 2", bindings));
        Assert.True(Evaluate<bool>(Environment, "c.groups.x[1].sku == 'B' && c.groups.x.map(l, l.qty) == [2, 3] && c.lines[0].sku == 'C'", bindings));
        Assert.True(Evaluate<bool>(Environment, "type(c.lines[0]) == samples.Line", bindings));
    }

    // An element that is null: the null value where the position allows it (string?, long?), UnexpectedNull as an error value
    // of the element where it does not. The error names the read of the element; the collection itself is read.
    [Fact]
    [Trait("Requirement", "F-2")]
    [Trait("Requirement", "F-4")]
    public void NullElements_FollowTheNullabilityOfTheirPosition()
    {
        var collections = new Collections
        {
            NullableElements = ["a", null],
            OptElements = [1, null],
            NotNullElements = ["a", null!],
        };
        CelBindings bindings = new CelBindings().Add("c", collections);

        Assert.True(Evaluate<bool>(Environment, "c.nullableElements.size() == 2 && c.nullableElements[1] == null && c.optElements[1] == null && c.optElements[0] == 1", bindings));
        Assert.Equal(CelErrorCode.UnexpectedNull, EvaluationError(Environment, "c.notNullElements[1]", bindings).Code);
        Assert.True(Evaluate<bool>(Environment, "c.notNullElements[0] == 'a'", bindings));
    }

    // A collection that is null is UnexpectedNull whatever its annotation, because the CEL types list and map take no null; has() is
    // false for both. The place of the error is the "." of the selection.
    [Fact]
    [Trait("Requirement", "F-2")]
    [Trait("Requirement", "F-4")]
    public void NullCollection_IsUnexpectedNullWhateverItsAnnotation()
    {
        var collections = new Collections { NoList = null, MustList = null! };
        CelBindings bindings = new CelBindings().Add("c", collections);

        Assert.True(Evaluate<bool>(Environment, "!has(c.noList) && !has(c.mustList)", bindings));
        AssertError(EvaluationError(Environment, "c.noList", bindings), CelErrorCode.UnexpectedNull, 1);
        AssertError(EvaluationError(Environment, "c.mustList", bindings), CelErrorCode.UnexpectedNull, 1);
    }

    // An element or a value that is itself a collection and is null is UnexpectedNull in both cases, whether its position is annotated ?
    // (List<List<long>?>) or not (the value of Dictionary<string, List<long>>); the collection that holds it is read.
    [Fact]
    [Trait("Requirement", "F-2")]
    [Trait("Requirement", "F-4")]
    public void NullCollectionAsAnElement_IsUnexpectedNullWhateverItsAnnotation()
    {
        var collections = new Collections
        {
            HoleyNested = [[1], null],
            HoleyValues = new Dictionary<string, List<long>> { ["a"] = [2], ["b"] = null! },
        };
        CelBindings bindings = new CelBindings().Add("c", collections);

        Assert.True(Evaluate<bool>(Environment, "c.holeyNested.size() == 2 && c.holeyNested[0] == [1] && c.holeyValues.a == [2]", bindings));
        Assert.Equal(CelErrorCode.UnexpectedNull, EvaluationError(Environment, "c.holeyNested[1]", bindings).Code);
        Assert.Equal(CelErrorCode.UnexpectedNull, EvaluationError(Environment, "c.holeyValues.b", bindings).Code);
    }

    // ImmutableArray<T> is read as a list without a copy and without a box.
    [Fact]
    [Trait("Requirement", "F-2")]
    public void ImmutableArrayWithAValue_IsAList()
    {
        var collections = new Collections { Immutable = ImmutableArray.Create<long>(1, 2, 3), OptArray = ImmutableArray.Create<long>(4) };
        CelBindings bindings = new CelBindings().Add("c", collections);

        Assert.True(Evaluate<bool>(Environment, "c.immutable.size() == 3 && c.immutable[2] == 3 && has(c.immutable)", bindings));
        Assert.True(Evaluate<bool>(Environment, "c.optArray == [4] && has(c.optArray)", bindings));
    }

    // A default ImmutableArray<T> is an array that was never made: it is an absent collection, UnexpectedNull at the selection, and
    // has() is false; the same for ImmutableArray<T>? that is null and for one that holds a default array.
    [Fact]
    [Trait("Requirement", "F-2")]
    [Trait("Requirement", "F-4")]
    public void DefaultImmutableArray_IsAnAbsentCollection()
    {
        var collections = new Collections { NoArray = default, OptArray = null };
        var holdsDefault = new Collections { OptArray = default(ImmutableArray<long>) };

        Assert.True(Evaluate<bool>(Environment, "!has(c.noArray) && !has(c.optArray)", new CelBindings().Add("c", collections)));
        AssertError(EvaluationError(Environment, "c.noArray", new CelBindings().Add("c", collections)), CelErrorCode.UnexpectedNull, 1);
        AssertError(EvaluationError(Environment, "c.optArray", new CelBindings().Add("c", collections)), CelErrorCode.UnexpectedNull, 1);
        Assert.True(Evaluate<bool>(Environment, "!has(c.optArray)", new CelBindings().Add("c", holdsDefault)));
        AssertError(EvaluationError(Environment, "c.optArray", new CelBindings().Add("c", holdsDefault)), CelErrorCode.UnexpectedNull, 1);
    }

    // A structure that implements IReadOnlyList<T> and is not ImmutableArray<T> is read as a list over its box.
    [Fact]
    [Trait("Requirement", "F-2")]
    public void StructureThatIsAReadOnlyList_IsAList()
    {
        var collections = new Collections { Fixed = new Triple(7, 8, 9) };

        Assert.True(Evaluate<bool>(Environment, "c.fixed == [7, 8, 9] && c.fixed.size() == 3 && has(c.fixed)", new CelBindings().Add("c", collections)));
    }
}
