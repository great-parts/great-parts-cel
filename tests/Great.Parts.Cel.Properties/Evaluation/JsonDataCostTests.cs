using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using Xunit;
using static Great.Parts.Cel.Properties.Evaluation.CostRows;

namespace Great.Parts.Cel.Properties.Evaluation;

// The memory of the rows of a JSON document that the first release measured only through `size` and `in` of an object
// (docs/design/tests.md, "Cost and memory of the extension sets"): the lookup of a key, the index of an array, a string, the
// traversal of an array and of an object, `in` on an array and the equality of two objects. The bytes that an evaluation allocates
// beyond its frame are at most the units it charges times B, on the sizes of CostRows, through Compile and through the planner without
// type checking. The variables are of type dyn, so that the check of a binding charges nothing.
public sealed class JsonDataCostTests
{
    // The sizes of the rows that read a property or an element: a document of none has none to read.
    private static readonly int[] NonEmpty = [1, 10, 100, 1000];

    // The sizes of the rows whose units grow with the square of the size: every property of one object is looked up in the other.
    private static readonly int[] Quadratic = [0, 1, 10, 100, 500];

    // The documents of the rows, by their kind and size, parsed once and kept to the end of the run.
    private static readonly ConcurrentDictionary<string, JsonDocument> Documents = new(StringComparer.Ordinal);

    private static readonly CostTable Table = new CostTable(Dynamic(new CelEnvironmentBuilder(CelStandardLibrary.V1)))
        // An object of the size with the names k0, k1, … and the value 1.
        .Add("json, key lookup in an object", size => One("x.k0", ObjectOf(size)), sizes: NonEmpty)
        .Add("json, has() on an object", size => One("has(x.k0)", ObjectOf(size)), sizes: NonEmpty)
        .Add("json, index of an object by a name", size => One("x['k0']", ObjectOf(size)), sizes: NonEmpty)
        .Add("json, traversal of an object", size => One("x.all(k, k != 'zz')", ObjectOf(size)))
        .Add("json, equality of two objects", size => Both("x == p", ObjectOf(size), ObjectOf(size)), sizes: Quadratic)

        // A string of the size as the value of the property.
        .Add("json, a string of the size", size => One("x.k0", Document("string", size, count => "{\"k0\": \"" + new string('a', count) + "\"}")))

        // An array of the size with the numbers 0, 1, ….
        .Add("json, index of an array", size => One("x[0]", ArrayOf(size)), sizes: NonEmpty)
        .Add("json, size of an array", size => One("size(x)", ArrayOf(size)))
        .Add("json, traversal of an array", size => One("x.all(v, v >= 0)", ArrayOf(size)))
        .Add("json, in on an array", size => One("-1 in x", ArrayOf(size)));

    public static TheoryData<string> RowNames() => Table.Names();

    [Theory]
    [Trait("Requirement", "R-3")]
    [MemberData(nameof(RowNames))]
    public static void Row_AllocatesWithinCost(string row) => Table.Check(row);

    private static JsonElement ObjectOf(int size) =>
        Document("object", size, count => "{" + string.Join(", ", Enumerable.Range(0, count).Select(index => "\"k" + index.ToString(CultureInfo.InvariantCulture) + "\": 1")) + "}");

    private static JsonElement ArrayOf(int size) =>
        Document("array", size, count => "[" + string.Join(", ", Enumerable.Range(0, count).Select(index => index.ToString(CultureInfo.InvariantCulture))) + "]");

    private static JsonElement Document(string kind, int size, Func<int, string> text) =>
        Documents.GetOrAdd(kind + ":" + size.ToString(CultureInfo.InvariantCulture), static (_, make) => JsonDocument.Parse(make()), () => text(size)).RootElement;

    private static (string, CelBindings) One(string text, JsonElement x)
    {
        var bindings = new CelBindings();
        bindings.Add("x", x);
        return (text, bindings);
    }

    private static (string, CelBindings) Both(string text, JsonElement x, JsonElement p)
    {
        var bindings = new CelBindings();
        bindings.Add("x", x);
        bindings.Add("p", p);
        return (text, bindings);
    }
}
