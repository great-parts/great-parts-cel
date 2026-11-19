using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Library.Extensions;

// distinct and the functions of the set sets look their elements up by hash (docs/cost.md, "Hashing in distinct and sets"): the cost is the hashing of
// the elements and the comparison that found each repeat, whatever the length of the list, and the code of the application is called
// in the order of the pairs. The rows are exact: the evaluation succeeds with the cost as the limit and fails with CostLimitExceeded at
// one unit less. A literal costs 0, a list literal of n elements 1 + n, the read of a variable 1.
public sealed class HashedListsCostTests
{
    // "expression | units": the nodes around the call and the formula of the table.
    private const string Rows = """
        [1, 2, 2].distinct() | 12
        ['a', 'b', 'a'].distinct() | 16
        [[1, 2], [1, 2]].distinct() | 21
        [1, 2, 1.0].distinct() | 12
        [1, 1u, 1.0].distinct() | 13
        [9007199254740993, 9007199254740992.0, 9007199254740992].distinct() | 13
        [9007199254740992.0, 9007199254740993].distinct() | 9
        [1, 9007199254740993, 1.0].distinct() | 12
        sets.contains([1], []) | 4
        sets.contains(['aa', 'b'], ['b']) | 15
        """;

    private static readonly BoxType Boxes = new();

    private static readonly Dictionary<string, CelValue> NoBindings = new();

    private static readonly CelEnvironment ListEnvironment = new CelEnvironmentBuilder(CelStandardLibrary.V1)
        .AddExtension(CelExtensions.Lists(4))
        .AddExtension(CelExtensions.Sets(0))
        .AddTypes(new BoxProvider(Boxes))
        .AddVariable("x", CelType.Dyn)
        .AddVariable("p", CelType.Dyn)
        .AddVariable("q", CelType.Dyn)
        .Build()
        .GetEnvironmentOrThrow();

    public static TheoryData<string, string> CostRows => RowData();

    [Theory]
    [Trait("Requirement", "R-3")]
    [MemberData(nameof(CostRows))]
    public static void Expression_CostsItsUnits(string expression, string units)
    {
        ulong cost = ulong.Parse(units, CultureInfo.InvariantCulture);

        Assert.True(Evaluate(expression, NoBindings, cost).IsSuccess, expression);
        Assert.Equal(CelErrorCode.CostLimitExceeded, Assert.Single(Evaluate(expression, NoBindings, cost - 1).Errors).Code);
    }

    // The cost does not depend on the length at which the library changes from scanning the hashes to buckets, nor on the length
    // above 32 at which a pass over the pairs would stop being a constant number of steps for a unit. The list holds the numbers
    // 0 to 9 over and over: ten of them stay, the rest are repeats of one comparison each.
    [Theory]
    [Trait("Requirement", "R-3")]
    [Trait("Requirement", "R-9")]
    [InlineData(10)]
    [InlineData(24)]
    [InlineData(25)]
    [InlineData(32)]
    [InlineData(33)]
    [InlineData(100)]
    public static void Cost_OfListsOfAnyLength_IsTheFormula(int length)
    {
        CelValue repeating = Numbers(length, static index => index % 10);
        CelValue disjoint = Numbers(length, static index => 100 + index);
        var both = new Dictionary<string, CelValue> { ["x"] = repeating, ["p"] = disjoint };

        Assert.Equal((3UL * (ulong)length) - 8, Cost("x.distinct()", both));
        Assert.Equal((4UL * (ulong)length) + 3, Cost("sets.contains(x, x)", both));
        Assert.Equal((3UL * (ulong)length) + 3, Cost("sets.intersects(x, p)", both));
        Assert.Equal((8UL * (ulong)length) + 3, Cost("sets.equivalent(x, x)", both));
        Assert.Equal(CelValue.FromBoolean(false), Evaluate("sets.contains(x, p)", both, null).GetValueOrThrow());
        Assert.True(Evaluate("x.distinct()", both, null).GetValueOrThrow().TryGetList(out CelList? kept));
        Assert.Equal(10, kept.Count);
    }

    // Values that CEL equality calls equal are one element: numbers of three kinds and both zeros, maps written in two orders, and an
    // int and a double that only round to one number, which CEL equality compares as the double. Such a number, of 2^53 or more in
    // absolute value, is not hashed: it is compared with the numbers of its group in order, each pair charged (the rows above).
    [Theory]
    [Trait("Requirement", "R-9")]
    [InlineData("[1, 1u, 1.0, 2, 2.0].distinct().size()", 2)]
    [InlineData("[0.0, -0.0, 0, 0u].distinct().size()", 1)]
    [InlineData("[{'a': 1, 'b': 2}, {'b': 2, 'a': 1}, {'a': 1}].distinct().size()", 2)]
    [InlineData("[9007199254740993, 9007199254740992.0, 9007199254740992].distinct().size()", 2)]
    [InlineData("[9007199254740992.0, 9007199254740993].distinct().size()", 1)]
    public static void Elements_EqualByCel_AreOne(string expression, long size)
    {
        Assert.Equal(CelValue.FromInt64(size), Evaluate(expression, NoBindings, null).GetValueOrThrow());
    }

    // A NaN is equal to nothing: every NaN stays, and none is found; it costs its hashing and nothing else.
    [Fact]
    [Trait("Requirement", "R-9")]
    public static void Nan_IsKeptAndNeverFound_AndCostsItsHashingOnly()
    {
        CelValue nans = List(CelValue.FromDouble(double.NaN), CelValue.FromDouble(double.NaN));
        var bindings = new Dictionary<string, CelValue> { ["x"] = nans };

        Assert.Equal(6UL, Cost("x.distinct()", bindings));
        Assert.Equal(7UL, Cost("sets.contains(x, x)", bindings));
        Assert.Equal(CelValue.FromBoolean(false), Evaluate("sets.contains(x, x)", bindings, null).GetValueOrThrow());
        Assert.Equal(CelValue.FromBoolean(false), Evaluate("sets.intersects(x, x)", bindings, null).GetValueOrThrow());
        Assert.True(Evaluate("x.distinct()", bindings, null).GetValueOrThrow().TryGetList(out CelList? kept));
        Assert.Equal(2, kept.Count);
    }

    // The elements that hold an object of the application are compared with each other in the order of the pairs, each pair charged as
    // the description says; the elements that hold none are compared by hash and cost their hashing and the pair that found them.
    [Fact]
    [Trait("Requirement", "R-9")]
    public static void Objects_AreComparedPairwiseInTheOrderOfThePairs()
    {
        CelValue one = Box(1);
        CelValue two = Box(2);
        CelValue oneAgain = Box(1);
        var bindings = new Dictionary<string, CelValue>
        {
            ["x"] = List(one, two, oneAgain),
            ["p"] = List(two),
            ["q"] = List(one, CelValue.FromInt64(1), oneAgain, CelValue.FromDouble(1.0)),
        };
        Boxes.Calls.Clear();

        // The read of x, the call, the three elements, their hashing, and the two pairs of 3 units.
        Assert.Equal(14UL, Cost("x.distinct()", bindings));
        Assert.Equal("2,1;1,1", string.Join(';', Boxes.Calls));

        // The number 1 and the double 1.0 are compared by hash for 1 unit; the objects are the only pair of the code of the application.
        Boxes.Calls.Clear();
        Assert.Equal(14UL, Cost("q.distinct()", bindings));
        Assert.Equal("1,1", string.Join(';', Boxes.Calls));

        // The first needle, an object, is compared with the objects of the list in order up to the first equal one.
        Boxes.Calls.Clear();
        Assert.Equal(14UL, Cost("sets.contains(x, p)", bindings));
        Assert.Equal("2,1;2,2", string.Join(';', Boxes.Calls));
    }

    // The exception of the application stops the call at the first pair that throws, as the pass over the pairs did, with the error
    // 3014 and the exception itself.
    [Fact]
    [Trait("Requirement", "R-9")]
    public static void ExceptionOfTheApplication_EndsTheCallAtTheFirstPairThatThrows()
    {
        var thrown = new InvalidOperationException("pair 2,1");
        Boxes.Throws = thrown;
        try
        {
            var bindings = new Dictionary<string, CelValue> { ["x"] = List(Box(1), Box(3), Box(2), Box(1)) };
            Boxes.Calls.Clear();

            CelEvalResult<CelValue> result = Evaluate("x.distinct()", bindings, null);

            CelError error = Assert.Single(result.Errors);
            Assert.Equal(CelErrorCode.ApplicationException, error.Code);
            Assert.Same(thrown, error.Exception);
            Assert.Equal("3,1;2,1", string.Join(';', Boxes.Calls));
        }
        finally
        {
            Boxes.Throws = null;
        }
    }

    private static CelValue Box(long id) => CelValue.FromObject(Boxes, new BoxInstance(id));

    private static CelValue List(params CelValue[] items) => CelValue.FromList(CelList.Create(items));

    private static CelValue Numbers(int length, Func<int, long> number)
    {
        var items = new CelValue[length];
        for (int i = 0; i < items.Length; i++)
        {
            items[i] = CelValue.FromInt64(number(i));
        }

        return List(items);
    }

    private static CelEvalResult<CelValue> Evaluate(string expression, Dictionary<string, CelValue> values, ulong? costLimit)
    {
        var bindings = new CelBindings();
        foreach (KeyValuePair<string, CelValue> value in values)
        {
            bindings.Add(value.Key, value.Value);
        }

        return ListEnvironment.Compile<CelValue>(expression).GetProgramOrThrow()
            .Evaluate(bindings, new CelEvalOptions { CostLimit = costLimit }, CancellationToken.None);
    }

    private static ulong Cost(string expression, Dictionary<string, CelValue> values) =>
        Evaluate(expression, values, ulong.MaxValue).Cost ?? throw new InvalidOperationException("The evaluation gave no cost: " + expression);

    private static TheoryData<string, string> RowData()
    {
        TheoryData<string, string> data = [];
        foreach (string row in Rows.ReplaceLineEndings("\n").Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            string[] cells = row.Split(" | ");
            data.Add(cells[0], cells[1]);
        }

        return data;
    }

    private sealed class BoxInstance(long id)
    {
        internal long Id { get; } = id;
    }

    private sealed class BoxProvider(CelObjectType type) : CelTypeProvider("box", [type], []);

    // A description whose equality is its own, charged 3 units, with a log of the pairs it was asked about.
    private sealed class BoxType : CelObjectType
    {
        internal BoxType()
            : base("acme.test.HashedBox", typeof(BoxInstance), [new CelObjectField(0, "id", CelType.Int)])
        {
        }

        internal List<string> Calls { get; } = [];

        internal Exception? Throws { get; set; }

        protected internal override CelValue GetField(object instance, int number) => CelValue.FromInt64(((BoxInstance)instance).Id);

        protected internal override bool HasField(object instance, int number) => true;

        protected internal override long? EqualityCost(object left, object right) => 3;

        protected internal override CelValue CelEquals(object left, object right)
        {
            long a = ((BoxInstance)left).Id;
            long b = ((BoxInstance)right).Id;
            Calls.Add(a.ToString(CultureInfo.InvariantCulture) + "," + b.ToString(CultureInfo.InvariantCulture));
            if (Throws is not null && a == 2 && b == 1)
            {
                throw Throws;
            }

            return CelValue.FromBoolean(a == b);
        }
    }
}
