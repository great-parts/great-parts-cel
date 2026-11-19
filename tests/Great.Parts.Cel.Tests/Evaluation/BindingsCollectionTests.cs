using System;
using System.Collections.Generic;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Evaluation;

// What the examples of the indexer (BindingIndexerTests) leave out: the checks of the arguments of the indexer and of Clear, and the
// enumerator that the collection initializer needs.
[Trait("Requirement", "F-12")]
public sealed class BindingsCollectionTests
{
    [Fact]
    public void Indexer_ChecksTheName()
    {
        var bindings = new CelBindings().Add("x", 1L);

        Assert.Throws<ArgumentNullException>("name", () => bindings[null!]);
        Assert.Throws<ArgumentNullException>("name", () => bindings[null!] = 1L);
        Assert.Throws<ArgumentException>("name", () => bindings[string.Empty] = 1L);
        Assert.Throws<ArgumentException>("name", () => bindings["missing"]);
    }

    [Fact]
    public void Indexer_BindsNull_AndTheGetterGivesNull()
    {
        var bindings = new CelBindings { ["x"] = null };

        Assert.Null(bindings["x"]);
        Assert.Equal(CelValue.Null, Assert.Single(bindings).Value);
    }

    // Clear on empty bindings is nothing, and a name that was cleared is not bound.
    [Fact]
    public void Clear_OnEmptyBindings_IsNothing_AndAClearedNameIsNotBound()
    {
        var bindings = new CelBindings();
        bindings.Clear();
        bindings["x"] = 1L;
        bindings.Clear();

        Assert.Empty(bindings);
        Assert.Throws<ArgumentException>("name", () => bindings["x"]);
    }

    // The enumerator lists the pairs of the names and their values, as the evaluation reads them.
    [Fact]
    public void Enumerator_ListsTheBoundPairs()
    {
        var bindings = new CelBindings().Add("a", 1L).Add("b", "text");
        bindings["c"] = true;

        var pairs = new Dictionary<string, CelValue>();
        foreach (KeyValuePair<string, CelValue> pair in bindings)
        {
            pairs.Add(pair.Key, pair.Value);
        }

        Assert.Equal(3, pairs.Count);
        Assert.Equal(CelValue.FromInt64(1), pairs["a"]);
        Assert.Equal(CelValue.FromString("text"), pairs["b"]);
        Assert.Equal(CelValue.FromBoolean(true), pairs["c"]);
    }

    [Fact]
    public void Enumerator_AfterAChange_ThrowsOnTheNextMove()
    {
        var bindings = new CelBindings().Add("a", 1L).Add("b", 2L);

        Assert.Throws<InvalidOperationException>(() =>
        {
            foreach (KeyValuePair<string, CelValue> pair in bindings)
            {
                bindings.Clear();
                _ = pair;
            }
        });
    }
}
