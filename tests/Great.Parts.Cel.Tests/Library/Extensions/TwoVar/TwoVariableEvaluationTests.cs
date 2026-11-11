using System;
using System.Linq;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Values;
using Xunit;
using static Great.Parts.Cel.Tests.Library.Extensions.TwoVar.TwoVarEnvironment;

namespace Great.Parts.Cel.Tests.Library.Extensions.TwoVar;

// The evaluation of the comprehensions of two variables: the order in which the entries of a map are visited, the short circuit of
// all and exists and its absence in existsOne, the filter of the transformations, the maps that grow past the size at which they
// get an index, and what an error value does to the accumulator. The variables are i = 1, j = 2, ints = [10, 20, 30],
// counts = {'a': 1, 'b': 2} and twenty = [1, ..., 20]. Each row of the first table is an expression that is true.
public sealed class TwoVariableEvaluationTests
{
    private const string Holds = """
        ints.all(i, v, v > 5 * i)
        !ints.all(i, v, v > 15 * i)
        ints.exists(i, v, v == 20 && i == 1)
        !ints.exists(i, v, v == 20 && i == 2)
        ints.existsOne(i, v, v > 25)
        !ints.existsOne(i, v, v > 15)
        !ints.existsOne(i, v, v > 100)
        ints.exists_one(i, v, i == 0)
        !ints.exists_one(i, v, v > 0)
        counts.all(k, v, v > 0 && k != '')
        counts.exists(k, v, k == 'b' && v == 2)
        [].all(i, v, false)
        ![].exists(i, v, true)
        ![].existsOne(i, v, true)
        counts.transformList(k, v, k) == ['a', 'b']
        counts.transformList(k, v, v) == [1, 2]
        {'z': 1, 'a': 2, 'm': 3}.transformList(k, v, k) == ['z', 'a', 'm']
        ints.transformList(i, v, i) == [0, 1, 2]
        ints.transformList(i, v, v + i) == [10, 21, 32]
        ints.transformList(i, v, v > 10, v / 10) == [2, 3]
        ints.transformList(i, v, v > 100, v).size() == 0
        [].transformList(i, v, v).size() == 0
        ints.transformMap(i, v, v) == {0: 10, 1: 20, 2: 30}
        counts.transformMap(k, v, v * 5) == {'a': 5, 'b': 10}
        ints.transformMap(i, v, i != 1, v) == {0: 10, 2: 30}
        [].transformMap(i, v, v).size() == 0
        counts.transformMapEntry(k, v, {v: k}) == {1: 'a', 2: 'b'}
        ints.transformMapEntry(i, v, v > 10, {i: v}) == {1: 20, 2: 30}
        ints.transformMapEntry(i, v, {}).size() == 0
        ints.transformMapEntry(i, v, {i: v, i + 10: v}) == {0: 10, 10: 10, 1: 20, 11: 20, 2: 30, 12: 30}
        counts.transformMap(k, v, v).transformList(k, v, k) == ['a', 'b']
        {'z': 1, 'a': 2}.transformMap(k, v, v).transformList(k, v, k) == ['z', 'a']
        {'z': 1, 'a': 2}.transformMapEntry(k, v, {k: v}).transformList(k, v, k) == ['z', 'a']
        counts.transformMapEntry(k, v, {k: v}) == counts
        twenty.transformMap(i, v, v * 2).size() == 20
        twenty.transformMap(i, v, v * 2)[0] == 2
        twenty.transformMap(i, v, v * 2)[7] == 16
        twenty.transformMap(i, v, v * 2)[8] == 18
        twenty.transformMap(i, v, v * 2)[16] == 34
        twenty.transformMap(i, v, v * 2)[19] == 40
        twenty.transformMap(i, v, v).transformList(k, v, k) == twenty.transformList(i, v, i)
        twenty.transformMapEntry(i, v, {v: i}).size() == 20
        twenty.transformMapEntry(i, v, {v: i})[20] == 19
        twenty.transformMapEntry(i, v, {v: i})[9] == 8
        twenty.transformMapEntry(i, v, {v: i}).transformMapEntry(k, v, {k + 100: v}).size() == 20
        twenty.transformMap(i, v, v % 3 == 0, v).size() == 6
        ints.transformList(i, v, ints.transformList(j, w, j * i)) == [[0, 0, 0], [0, 1, 2], [0, 2, 4]]
        ints.all(i, v, ints.exists(j, w, w == v && j == i))
        ints.all(i, v, true) && i == 1 && j == 2
        dyn(ints).transformList(i, v, i) == [0, 1, 2]
        dyn(counts).all(k, v, v > 0)
        dyn(counts).transformMap(k, v, v) == counts
        ![5, 0].all(i, v, 10 / v > 100)
        [0, 5].exists(i, v, 10 / v > 1)
        """;

    public static TheoryData<string> HoldingCases => Lines(Holds);

    [Theory]
    [MemberData(nameof(HoldingCases))]
    public static void Expression_IsTrue(string expression)
    {
        CelEvalResult<CelValue> result = Evaluate(expression);

        Assert.True(result.IsSuccess, expression + ": " + string.Join("; ", result.Errors));
        Assert.True(result.GetValueOrThrow().TryGetBoolean(out bool value) && value, expression);
    }

    // "expression | code | the text at which the error is": an error value of the predicate or of the transformation is the value of
    // the step, so the accumulator keeps it to the end: all and existsOne visit every element they are given, and the later steps
    // do not give the accumulator back.
    private const string Errors = """
        [5, 0].existsOne(i, v, 10 / v > 1) | DivisionByZero | /
        [0, 5].all(i, v, 10 / v > 1) | DivisionByZero | /
        [1, 0].transformList(i, v, 10 / v) | DivisionByZero | /
        [0, 1].transformList(i, v, 10 / v) | DivisionByZero | /
        [1, 0].transformList(i, v, 10 / v > 1, v) | DivisionByZero | /
        [1, 0].transformMap(i, v, 10 / v) | DivisionByZero | /
        [0, 1].transformMap(i, v, 10 / v) | DivisionByZero | /
        [1, 0].transformMap(i, v, 10 / v > 1, v) | DivisionByZero | /
        [1, 0].transformMapEntry(i, v, {i: 10 / v}) | DivisionByZero | /
        [0, 1].transformMapEntry(i, v, {i: 10 / v}) | DivisionByZero | /
        """;

    public static TheoryData<string, string, string> ErrorCases => Triples(Errors);

    [Theory]
    [MemberData(nameof(ErrorCases))]
    public static void ErrorValue_BecomesTheValueOfTheStepAndStays(string expression, string code, string place)
    {
        ArgumentNullException.ThrowIfNull(expression);
        ArgumentNullException.ThrowIfNull(place);

        CelError error = Assert.Single(Evaluate(expression).Errors);

        Assert.Equal(code, error.Code.ToString());
        Assert.Equal(expression.IndexOf(place, StringComparison.Ordinal), error.Location.Offset);
    }

    // A key that CEL equality finds equal to one in the result is DuplicateMapKey at the call of the macro, whatever the kind of the
    // two keys: 1u after 1 is a repeat, and the error names the later key. The error stays: the third step does not undo it.
    [Fact]
    public static void KeyOfAnotherKindThatIsEqual_IsDuplicateMapKeyAtTheMacro()
    {
        const string Text = "ints.transformMapEntry(i, v, i == 0 ? dyn({1: 0}) : dyn({1u: 1}))";

        CelError error = Assert.Single(Evaluate(Text).Errors);

        Assert.Equal(CelErrorCode.DuplicateMapKey, error.Code);
        Assert.Equal("duplicate map key: 1u", error.Message);
        Assert.Equal(Text.IndexOf('(', StringComparison.Ordinal), error.Location.Offset);
    }

    // A range that is no list and no map at run time is EvalInvalidComprehensionRange at the range, whatever the macro.
    [Theory]
    [InlineData("d.all(i, v, true)")]
    [InlineData("d.transformList(i, v, v)")]
    [InlineData("d.transformMap(i, v, v)")]
    [InlineData("d.transformMapEntry(i, v, {i: v})")]
    public static void RangeThatIsNoCollection_IsEvalInvalidComprehensionRangeAtTheRange(string expression)
    {
        ArgumentNullException.ThrowIfNull(expression);
        CelBindings bindings = new CelBindings().Add("d", 5L);

        CelError error = Assert.Single(Evaluate(expression, bindings).Errors);

        Assert.Equal(CelErrorCode.EvalInvalidComprehensionRange, error.Code);
        Assert.Equal(0, error.Location.Offset);
    }

    // The pairs are charged before they are put in, so a limit stops a result that is growing: a limit that pays for the range, the
    // empty map and two steps is exceeded in the third.
    [Fact]
    public static void LimitInTheMiddleOfTheLoop_StopsTheGrowthOfTheResult()
    {
        const string Text = "[7, 8, 9].transformMap(i, v, v)";

        // The list 4, the empty map 1, and for each step 1, the read of v 1 and the pair 2.
        Assert.True(Evaluate(Text, null, 4 + 1 + (3 * 4)).IsSuccess);
        Assert.Equal(CelErrorCode.CostLimitExceeded, Assert.Single(Evaluate(Text, null, 4 + 1 + (3 * 4) - 1).Errors).Code);
        Assert.Equal(CelErrorCode.CostLimitExceeded, Assert.Single(Evaluate(Text, null, 4 + 1 + (2 * 4) + 1).Errors).Code);
    }

    // A JSON object as the range: the key of the property and its value, in the order of the properties; a number is a double.
    [Fact]
    public static void JsonObject_GivesItsPropertiesAsKeysAndValues()
    {
        using System.Text.Json.JsonDocument document = System.Text.Json.JsonDocument.Parse("""{"x": 5, "y": 7}""");
        CelBindings bindings = new CelBindings().Add("d", document.RootElement.Clone());

        Assert.True(IsTrue("d.transformList(k, v, k) == ['x', 'y']", bindings));
        Assert.True(IsTrue("d.transformList(k, v, v) == [5.0, 7.0]", bindings));
        Assert.True(IsTrue("d.all(k, v, v > 4.0)", bindings));
        Assert.True(IsTrue("d.transformMap(k, v, v) == {'x': 5.0, 'y': 7.0}", bindings));
    }

    // A JSON array as the range: the index and the element.
    [Fact]
    public static void JsonArray_GivesItsIndexesAndElements()
    {
        using System.Text.Json.JsonDocument document = System.Text.Json.JsonDocument.Parse("[5, 7]");
        CelBindings bindings = new CelBindings().Add("d", document.RootElement.Clone());

        Assert.True(IsTrue("d.transformList(i, v, i) == [0, 1]", bindings));
        Assert.True(IsTrue("d.transformMap(i, v, v) == {0: 5.0, 1: 7.0}", bindings));
        Assert.True(IsTrue("d.existsOne(i, v, v > 6.0)", bindings));
    }

    private static bool IsTrue(string expression, CelBindings bindings)
    {
        CelEvalResult<CelValue> result = Evaluate(expression, bindings);
        Assert.True(result.IsSuccess, expression + ": " + string.Join("; ", result.Errors));
        return result.GetValueOrThrow().TryGetBoolean(out bool value) && value;
    }

    private static TheoryData<string> Lines(string rows)
    {
        TheoryData<string> data = [];
        foreach (string row in rows.ReplaceLineEndings("\n").Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            data.Add(row.Trim());
        }

        return data;
    }

    private static TheoryData<string, string, string> Triples(string rows)
    {
        TheoryData<string, string, string> data = [];
        foreach (string[] cells in Cells(rows))
        {
            data.Add(cells[0], cells[1], cells[2]);
        }

        return data;
    }

    private static string[][] Cells(string rows) =>
        [.. rows.ReplaceLineEndings("\n").Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(row => row.Split(" | ").Select(cell => cell.Trim()).ToArray())];
}
