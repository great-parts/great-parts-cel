using System;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Values;
using Xunit;
using static Great.Parts.Cel.Tests.Library.Extensions.Lists.ListsEnvironment;

namespace Great.Parts.Cel.Tests.Library.Extensions.Lists;

// The functions of the set lists on what the cases of the specification and the examples leave: each of the eight comparable types
// sorted, with equal keys and above the twelve elements at which the sort changes its way, the errors of slice and flatten with the
// details that the set gives them, the order of the comparisons of distinct, and values that only bindings can hold. Each row of the
// first table is an expression that is true.
public sealed class ListsFunctionTests
{
    private const string Holds = """
        [1, 2, 3, 4].slice(1, 3) == [2, 3]
        [1, 2, 3].slice(0, 3) == [1, 2, 3]
        [1, 2, 3].slice(2, 2) == []
        [1, 2, 3].slice(3, 3) == []
        ['a', 'b'].slice(1, 2) == ['b']
        [[1, [2]], [3]].flatten() == [1, [2], 3]
        [[1, [2]], [3]].flatten(2) == [1, 2, 3]
        [[1, [2]], [3]].flatten(7) == [1, 2, 3]
        [[1, [2]], [3]].flatten(0) == [[1, [2]], [3]]
        [[], [[]]].flatten() == [[]]
        [[1], [], [2, 3]].flatten() == [1, 2, 3]
        dyn([1, 2]).flatten() == [1, 2]
        dyn([1, [2]]).flatten() == [1, 2]
        [1].reverse() == [1]
        [].reverse() == []
        [[1], [2]].reverse() == [[2], [1]]
        [1, 1.0, 1u, 2, 2.0].distinct() == [1, 2]
        ['a', 'b', 'a'].distinct() == ['a', 'b']
        [[1], [1], [2]].distinct() == [[1], [2]]
        [{'a': 1}, {'a': 1}].distinct() == [{'a': 1}]
        [1, 'a', 1, 'a', null, null].distinct() == [1, 'a', null]
        [3, 3, 3].distinct() == [3]
        [double('NaN'), double('NaN')].distinct().size() == 2
        [3u, 1u, 2u].sort() == [1u, 2u, 3u]
        [2.5, -1.5, 0.0, -2.5].sort() == [-2.5, -1.5, 0.0, 2.5]
        [true, false, true, false].sort() == [false, false, true, true]
        ['b', 'a', 'ab', ''].sort() == ['', 'a', 'ab', 'b']
        [b'b', b'a', b'ab', b''].sort() == [b'', b'a', b'ab', b'b']
        ['é', 'z', 'a'].sort() == ['a', 'z', 'é']
        ['😀', 'Ａ', 'b'].sort() == ['b', 'Ａ', '😀']
        [b'\xff', b'\x01'].sort() == [b'\x01', b'\xff']
        [duration('2s'), duration('1s'), duration('1500ms')].sort() == [duration('1s'), duration('1500ms'), duration('2s')]
        [timestamp('2020-01-02T00:00:00Z'), timestamp('2020-01-01T00:00:00Z')].sort() == [timestamp('2020-01-01T00:00:00Z'), timestamp('2020-01-02T00:00:00Z')]
        [9007199254740993, 9007199254740992, -1].sort() == [-1, 9007199254740992, 9007199254740993]
        [18446744073709551615u, 9223372036854775808u, 1u].sort() == [1u, 9223372036854775808u, 18446744073709551615u]
        [double('inf'), 1.0, -double('inf')].sort() == [-double('inf'), 1.0, double('inf')]
        [[2, 'a'], [1, 'b'], [2, 'c'], [1, 'd']].sortBy(e, e[0]) == [[1, 'b'], [1, 'd'], [2, 'a'], [2, 'c']]
        ['bb', 'a', 'cc'].sortBy(e, size(e)) == ['a', 'bb', 'cc']
        ['x', 'y'].sortBy(e, e == 'x') == ['y', 'x']
        [1, 2, 3].sortBy(e, uint(4 - e)) == [3, 2, 1]
        [1, 2, 3].sortBy(e, double(4 - e)) == [3, 2, 1]
        [1, 2, 3].sortBy(e, duration(string(4 - e) + 's')) == [3, 2, 1]
        [1, 2, 3].sortBy(e, timestamp(1000 - e)) == [3, 2, 1]
        [1, 2, 3].sortBy(e, bytes(string(4 - e))) == [3, 2, 1]
        lists.range(20).reverse().sort() == lists.range(20)
        lists.range(13).sortBy(e, 12 - e) == lists.range(13).reverse()
        lists.range(20).sortBy(e, e % 3) == [0, 3, 6, 9, 12, 15, 18, 1, 4, 7, 10, 13, 16, 19, 2, 5, 8, 11, 14, 17]
        lists.range(40).sortBy(e, e % 2 == 0) == lists.range(40).filter(e, e % 2 != 0) + lists.range(40).filter(e, e % 2 == 0)
        lists.range(1000).reverse().sort() == lists.range(1000)
        lists.range(300).sortBy(e, string(e % 7)).slice(0, 3) == [0, 7, 14]
        [1, 2, 3].sort().sort() == [1, 2, 3]
        """;

    // "expression | code | message"
    private const string Errors = """
        [1, 2, 3].slice(-1, 2) | InvalidArgument | invalid argument to 'slice': cannot slice with a negative index -1
        [1, 2, 3].slice(1, -2) | InvalidArgument | invalid argument to 'slice': cannot slice with a negative index -2
        [1, 2, 3].slice(-3, -2) | InvalidArgument | invalid argument to 'slice': cannot slice with a negative index -3
        [1, 2, 3].slice(2, 1) | InvalidArgument | invalid argument to 'slice': cannot slice: start index 2 must be less than or equal to the end index
        [1, 2, 3].slice(9, 4) | InvalidArgument | invalid argument to 'slice': cannot slice: start index 9 must be less than or equal to the end index
        [1, 2, 3].slice(0, 4) | InvalidArgument | invalid argument to 'slice': cannot slice: end index 4 is above the list length 3
        [].slice(0, 1) | InvalidArgument | invalid argument to 'slice': cannot slice: end index 1 is above the list length 0
        [1, 2, 3].slice(1, 9223372036854775807) | InvalidArgument | invalid argument to 'slice': cannot slice: end index 9223372036854775807 is above the list length 3
        [[1]].flatten(-1) | InvalidArgument | invalid argument to 'flatten': level must be non-negative
        """;

    // Lists whose elements fit no one of the eight types, or not all one type: the planner finds no overload for the values, as the
    // elements are checked at depth, and the sorts never see them.
    private const string NoMatching = """
        [1, 'a'].sort()
        dyn([[1, 2, 3]]).sort()
        dyn([1, 1u]).sort()
        dyn([1, 1.0]).sort()
        [[1], ['a']].sortBy(e, e[0])
        [[1], [1.5]].sortBy(e, e[0])
        """;

    // The elements of an application list that fails when it is read: the second.
    private static readonly CelValue Failing = CelValue.FromList(CelList.CreateView(
        new[] { 1, 2, 3 },
        static number => number == 2 ? throw new InvalidOperationException("boom") : CelValue.FromInt64(number)));

    public static TheoryData<string> HoldingCases => HoldingData();

    public static TheoryData<string, string, string> ErrorCases => ErrorData();

    public static TheoryData<string> NoMatchingCases => RowsOf(NoMatching);

    [Theory]
    [MemberData(nameof(HoldingCases))]
    public static void Expression_IsTrue(string expression)
    {
        CelEvalResult<CelValue> result = Evaluate(expression);

        Assert.True(result.IsSuccess, expression + ": " + string.Join("; ", result.Errors));
        Assert.True(result.GetValueOrThrow().TryGetBoolean(out bool value) && value, expression);
    }

    [Theory]
    [MemberData(nameof(ErrorCases))]
    public static void Error_HasItsCodeAndItsMessage(string expression, string code, string message)
    {
        CelError error = Assert.Single(Evaluate(expression).Errors);

        Assert.Equal((Enum.Parse<CelErrorCode>(code), message), (error.Code, error.Message));
    }

    [Theory]
    [MemberData(nameof(NoMatchingCases))]
    public static void MixedKinds_FitNoOverload(string expression)
    {
        Assert.Equal(CelErrorCode.EvalNoMatchingOverload, Assert.Single(Evaluate(expression).Errors).Code);
    }

    // The checks of slice come in order, and the error of a bad argument comes with the unit of the call alone: the list literal is 4 and
    // the call 1, whatever the indexes.
    [Fact]
    public static void Slice_RefusesBadIndexesWithTheUnitOfTheCall()
    {
        Assert.Equal(CelErrorCode.InvalidArgument, Assert.Single(Evaluate("[1, 2, 3].slice(2, 1)", null, 5).Errors).Code);
        Assert.Equal(CelErrorCode.InvalidArgument, Assert.Single(Evaluate("[1, 2, 3].slice(0, 4)", null, 5).Errors).Code);
        Assert.Equal(CelErrorCode.InvalidArgument, Assert.Single(Evaluate("[1, 2, 3].slice(-1, 2)", null, 5).Errors).Code);
        Assert.Equal(CelErrorCode.CostLimitExceeded, Assert.Single(Evaluate("[1, 2, 3].slice(2, 1)", null, 4).Errors).Code);
    }

    // A NaN is unordered with everything, so the sort of up to 12 elements leaves what an insertion sort leaves: 3.0, 1.0, NaN, 2.0
    // becomes 1.0, 3.0, NaN, 2.0, and it is no error.
    [Fact]
    public static void SortWithANaN_LeavesTheOrderOfTheInsertionSort()
    {
        CelValue sorted = Evaluate("[3.0, 1.0, double('NaN'), 2.0].sort()").GetValueOrThrow();

        Assert.True(sorted.TryGetList(out CelList? list));
        Assert.Equal(4, list.Count);
        Assert.Equal(1.0, Real(list[0]));
        Assert.Equal(3.0, Real(list[1]));
        Assert.True(double.IsNaN(Real(list[2])));
        Assert.Equal(2.0, Real(list[3]));
    }

    // Above 12 elements the sort merges: a NaN is no error there either, and the sort keeps every element, the NaNs among them.
    [Fact]
    public static void SortOfMoreThanTwelveWithANaN_KeepsEveryElement()
    {
        const string Expression = "[5.0, double('NaN'), 4.0, 3.0, 2.0, double('NaN'), 1.0, 0.0, 9.0, 8.0, 7.0, 6.0, 10.0, 11.0].sort()";

        Assert.True(Evaluate(Expression).GetValueOrThrow().TryGetList(out CelList? sorted));
        Assert.Equal(14, sorted.Count);
        int notNumbers = 0;
        double sum = 0;
        for (int i = 0; i < sorted.Count; i++)
        {
            double element = Real(sorted[i]);
            if (double.IsNaN(element))
            {
                notNumbers++;
            }
            else
            {
                sum += element;
            }
        }

        Assert.Equal(2, notNumbers);
        Assert.Equal(66.0, sum);
    }

    // A list of the application is read element by element, and an element that fails is the error of the call.
    [Theory]
    [InlineData("x.reverse()")]
    [InlineData("x.distinct()")]
    [InlineData("x.slice(0, 3)")]
    [InlineData("x.sort()")]
    [InlineData("x.flatten()")]
    [InlineData("[1, 2, 3].sortBy(e, x[1])")]
    public static void ElementThatFails_IsTheErrorOfTheCall(string expression)
    {
        CelEvalResult<CelValue> result = Evaluate(expression, new CelBindings().Add("x", Failing));

        Assert.Equal(CelErrorCode.ApplicationException, Assert.Single(result.Errors).Code);
    }

    // flatten walks the lists with a stack of its own: a list nested a hundred thousand levels, which only bindings can build, is
    // flattened without recursion, and a depth that the nesting does not reach flattens as far as it goes.
    [Fact]
    public static void Flatten_WalksDeepNestingWithoutRecursion()
    {
        CelValue deep = ListOf(CelValue.FromInt64(7));
        for (int i = 0; i < 100_000; i++)
        {
            deep = ListOf(deep);
        }

        CelBindings bindings = new CelBindings().Add("x", deep);

        CelValue flat = Evaluate("x.flatten(1000000)", bindings).GetValueOrThrow();
        CelValue one = Evaluate("x.flatten(1)", bindings).GetValueOrThrow();

        Assert.True(flat.TryGetList(out CelList? flatList));
        Assert.True(Assert.Single(flatList).TryGetInt64(out long seven));
        Assert.Equal(7, seven);
        Assert.True(one.TryGetList(out CelList? oneList));
        Assert.Equal(CelValueKind.List, Assert.Single(oneList).Kind);
    }

    private static double Real(CelValue value)
    {
        Assert.True(value.TryGetDouble(out double real), value.Kind.ToString());
        return real;
    }

    public static TheoryData<string> HoldingData() => RowsOf(Holds);

    private static TheoryData<string> RowsOf(string table)
    {
        TheoryData<string> data = [];
        foreach (string line in Lines(table))
        {
            data.Add(line);
        }

        return data;
    }

    public static TheoryData<string, string, string> ErrorData()
    {
        TheoryData<string, string, string> data = [];
        foreach (string line in Lines(Errors))
        {
            string[] cells = line.Split(" | ");
            data.Add(cells[0], cells[1], cells[2]);
        }

        return data;
    }

    private static string[] Lines(string table) => table.ReplaceLineEndings("\n").Split('\n', StringSplitOptions.RemoveEmptyEntries);
}
