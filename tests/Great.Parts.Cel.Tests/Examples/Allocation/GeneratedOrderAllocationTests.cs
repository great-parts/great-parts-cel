using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using Great.Parts.Cel.Evaluation;
using Great.Parts.Cel.Generator.Samples.Orders;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Examples.Allocation;

// The allocation test of the evaluation on the type `order` of the samples, whose descriptions the generator wrote
// ("Allocation on the types of the generator" of the page of the tests): the same scenarios E1-E9, the same data and the same
// measurement as `Allocation/EvaluationAllocationTests`, which describes the order by `CelObjectTypeBuilder<TCarrier>`. The binding is
// `CelValue.FromObject(orderType, order)` with the description taken from ShopContext, and `lines` is read by the created code, so a
// scenario that selects `order.lines` allocates the view of the list in every evaluation: its budget is the budget of the other test plus
// the size of that view, one `CelList` object, the same number in each scenario. The other reads — `total`, `status`, `currency`, `id`, a
// field of a line of a sealed class — allocate nothing, so the scenarios that do not read `lines` keep the budget 0. The budgets are those of
// the other test and `ViewBytes`; the numbers come from the measurement in the three ways of the run (the default; TieredCompilation off; call
// counting and on-stack replacement off) and under Native AOT, the greatest seen, and a budget of 0 holds in every configuration.
public sealed class GeneratedOrderAllocationTests
{
    // The bytes that the public call adds to Run: the frame with its slots and argument stack, and the result object; they do not grow
    // with the data. The greatest measured in the test without the generator is 440, in the scenarios with a comprehension.
    private const long FacadeBytes = 512;

    private const int Counted = 3;

    // The view of the list that the created code makes for `lines`: one `CelList` object. The constant is the bound of the page of the
    // created code (at most one object of at most 64 bytes for a read of a member); the exact size of the view is not part of the contract.
    private const ulong ViewBytes = 64;

    // The moment of every order of the scenarios.
    private static readonly DateTimeOffset Created = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);

    private static readonly CelObjectType OrderType = ShopContext.Default.ObjectTypes.Single(type => type.Name == "shop.Order");

    private static readonly CelEnvironment Environment = new CelEnvironmentBuilder(CelStandardLibrary.V1)
        .AddTypes(ShopContext.Default)
        .AddVariable<Order>("order")
        .AddVariable("now", CelType.Timestamp)
        .Build()
        .GetEnvironmentOrThrow();

    private static readonly Dictionary<string, Scenario> Table = Scenarios();

    public static TheoryData<string> Names
    {
        get
        {
            var data = new TheoryData<string>();
            foreach (string name in Table.Keys)
            {
                data.Add(name);
            }

            return data;
        }
    }

    [Theory]
    [Trait("Requirement", "P-2")]
    [MemberData(nameof(Names))]
    public static void WithinBudget(string scenario)
    {
        Scenario row = Table[scenario];
        Fixture fixture = Fixture.Create(row.Lines, row.Discount, row.LastSkuOnly);

        ulong budget = row.Budget(fixture);
        (long[] runs, long facade, long run) = row.Measure(fixture);

        TestContext.Current.TestOutputHelper?.WriteLine(string.Create(
            CultureInfo.InvariantCulture,
            $"{scenario}: {runs[0]} {runs[1]} {runs[2]} bytes, facade {facade} bytes"));
        Assert.True((ulong)run <= budget, string.Create(CultureInfo.InvariantCulture, $"{scenario}: {run} bytes, the budget is {budget}"));
        Assert.True(facade <= run + FacadeBytes, string.Create(CultureInfo.InvariantCulture, $"{scenario}: the public call allocates {facade} bytes, Run {run}"));
    }

    private static Dictionary<string, Scenario> Scenarios()
    {
        var table = new Dictionary<string, Scenario>(StringComparer.Ordinal);

        // E1: reads of fields of the object (a string, a double), a comparison and a conjunction of values that exist: no new value, and
        // `lines` is not read.
        Add(table, "E1", 10, Scenario.Of<bool>("order.total > 1000.0 && order.status == 'approved'", _ => true, _ => 0));

        // E2: `order.lines` is read once, which allocates the view; the comprehension reads fields of the lines, bare instances of a sealed
        // class that the index describes by their type, and compares: nothing else is created.
        Add(table, "E2, 10 lines", 10, Scenario.Of<bool>("order.lines.all(l, l.qty > 0 && l.price >= 0.0)", _ => true, _ => ViewBytes));
        Add(table, "E2, 100 lines", 100, Scenario.Of<bool>("order.lines.all(l, l.qty > 0 && l.price >= 0.0)", _ => true, _ => ViewBytes));

        // E3: the same for exists with a string function that searches in place.
        Add(table, "E3, 10 lines", 10, Scenario.Of<bool>("order.lines.exists(l, l.sku.startsWith('SKU-'))", _ => true, _ => ViewBytes));
        Add(table, "E3, 100 lines", 100, Scenario.Of<bool>("order.lines.exists(l, l.sku.startsWith('SKU-'))", _ => true, _ => ViewBytes));
        Add(table, "E3, last line, 10 lines", 10, Scenario.Of<bool>("order.lines.exists(l, l.sku.startsWith('SKU-'))", _ => true, _ => ViewBytes), lastSkuOnly: true);
        Add(table, "E3, last line, 100 lines", 100, Scenario.Of<bool>("order.lines.exists(l, l.sku.startsWith('SKU-'))", _ => true, _ => ViewBytes), lastSkuOnly: true);

        // E4: the view of the list, and the two lists that filter and map create, with the arrays that grow while they are built: a
        // function of the lines that pass.
        Add(table, "E4, 10 lines", 10, Scenario.Of<CelValue>("order.lines.filter(l, l.qty > 10).map(l, double(l.qty) * l.price)", Mapped, E4Budget));
        Add(table, "E4, 100 lines", 100, Scenario.Of<CelValue>("order.lines.filter(l, l.qty > 10).map(l, double(l.qty) * l.price)", Mapped, E4Budget));

        // E5: matches with a pattern that is a literal. The literal is prepared when the program is compiled, so the evaluation reads the
        // email, a field of the object of a sealed class that the created code reads as it is, and runs the engine on it: the arrays of the engine are
        // rented from the pool and come back, and the result is a bool. `lines` is not read, so the budget is 0 bytes.
        Add(table, "E5, 10 lines", 10, Scenario.Of<bool>("order.customer.email.matches('^[^@]+@[^@]+[.][a-z]+$')", _ => true, _ => 0));
        Add(table, "E5, 100 lines", 100, Scenario.Of<bool>("order.customer.email.matches('^[^@]+@[^@]+[.][a-z]+$')", _ => true, _ => 0));

        // E5p: the same with the pattern read from a field of the order; the first evaluation, which the measurement does not count, puts
        // the pattern in the cache of the environment, and a hit in the cache allocates nothing.
        Add(table, "E5p, 10 lines", 10, Scenario.Of<bool>("order.customer.email.matches(order.policy)", _ => true, _ => 0));
        Add(table, "E5p, 100 lines", 100, Scenario.Of<bool>("order.customer.email.matches(order.policy)", _ => true, _ => 0));

        // E6: a presence test and subtraction of doubles: no new value, with the discount and without it. `has(order.discount)` is the
        // presence of a `double?` that the created code reads without a box.
        Add(table, "E6", 10, Scenario.Of<double>("has(order.discount) ? order.total - order.discount : order.total", _ => 1400.0, _ => 0));
        Add(table, "E6, without the discount", 10, Scenario.Of<double>("has(order.discount) ? order.total - order.discount : order.total", _ => 1500.0, _ => 0), discount: false);

        // E7: the moment of the order is in the payload of the value (`DateTimeOffset` is read without allocation) and the constant
        // duration('72h') is folded at planning, so the sum, the comparison and the read of a field of time create nothing.
        Add(table, "E7, 10 lines", 10, Scenario.Of<bool>("order.created + duration('72h') > now", _ => true, _ => 0));
        Add(table, "E7, 100 lines", 100, Scenario.Of<bool>("order.created + duration('72h') > now", _ => true, _ => 0));

        // E8: the view of `lines` for its size, and the list of the literal of constants, which the plan builds at each evaluation.
        Add(table, "E8, 10 lines", 10, Scenario.Of<bool>("size(order.lines) <= 100 && order.currency in ['RUB', 'USD', 'EUR']", _ => true, _ => E8Budget + ViewBytes));
        Add(table, "E8, 100 lines", 100, Scenario.Of<bool>("size(order.lines) <= 100 && order.currency in ['RUB', 'USD', 'EUR']", _ => true, _ => E8Budget + ViewBytes));

        // E9: the three strings of the concatenations, one for each `+`; the fields are read without allocation.
        Add(table, "E9", 10, Scenario.Of<string>("'Order ' + order.id + ': ' + order.status", _ => "Order ORD-0001: approved", _ => E9Budget));

        return table;
    }

    // The new list of the literal `['RUB', 'USD', 'EUR']`: an array of 3 values and the object of the list over it, which is
    // smaller than the accumulator of a comprehension.
    private static readonly ulong E8Budget = ArrayBytes(3) + LiteralListObjectBytes;

    // The three strings of `'Order ' + order.id`, `… + ': '` and `… + order.status`: 14, 16 and 24 code units.
    private static readonly ulong E9Budget = StringBytes(14) + StringBytes(16) + StringBytes(24);

    // The bytes of an array of n values of CelValue, 32 bytes each, with the header and the length of the array.
    private const ulong ValueBytes = 32;

    private const ulong ArrayHeaderBytes = 24;

    // The accumulator of a comprehension and the list that its result turns it into, two small objects.
    private const ulong ListObjectBytes = 56;

    // The object of the list that a literal builds over its array.
    private const ulong LiteralListObjectBytes = 24;

    private static ulong ArrayBytes(int length) => ArrayHeaderBytes + (ValueBytes * (ulong)length);

    // A string of n code units: the header and the length, 2 bytes per code unit and the terminator, to a multiple of 8.
    private static ulong StringBytes(int length) => (ulong)((22 + (2 * length) + 7) & ~7);

    // The list that a comprehension builds of count elements out of a range of range elements: the accumulator grows by doubling
    // from 4 up to the range and allocates a new array each time, its array is trimmed once when it has room left, and the two objects.
    private static ulong BuilderBytes(int range, int count)
    {
        ulong bytes = ListObjectBytes;
        int capacity = 0;
        while (capacity < count)
        {
            capacity = Math.Min(Math.Max(capacity * 2, 4), range);
            bytes += ArrayBytes(capacity);
        }

        return count < capacity ? bytes + ArrayBytes(count) : bytes;
    }

    // E4: the view of `lines`, then filter over the lines builds the list of the lines that pass, and map over that list builds the list
    // of the products.
    private static ulong E4Budget(Fixture fixture) =>
        ViewBytes + BuilderBytes(fixture.Order.Lines.Length, fixture.Passing) + BuilderBytes(fixture.Passing, fixture.Passing);

    private static void Add(Dictionary<string, Scenario> table, string name, int lines, Scenario scenario, bool discount = true, bool lastSkuOnly = false) =>
        table.Add(name, scenario with { Lines = lines, Discount = discount, LastSkuOnly = lastSkuOnly });

    // The expected result of E4: the products of the lines with a quantity over 10, in the order of the lines.
    private static bool Mapped(Fixture fixture, CelValue value)
    {
        if (!value.TryGetList(out CelList? list) || list.Count != fixture.Passing)
        {
            return false;
        }

        int index = 0;
        foreach (OrderLine line in fixture.Order.Lines)
        {
            if (line.Qty > 10)
            {
                if (!list[index++].TryGetDouble(out double product) || product != line.Qty * line.Price)
                {
                    return false;
                }
            }
        }

        return true;
    }

    // The smallest bytes of Run in three evaluations after the first, and those of the public call.
    private static (long[] Runs, long Facade, long Run) MeasureProgram<T>(string expression, Fixture fixture, Func<Fixture, T, bool> accepts)
    {
        CelProgram<T> program = Environment.Compile<T>(expression).GetProgramOrThrow();
        Plan plan = program.Plan;
        IReadOnlyDictionary<string, CelValue> values = fixture.Bindings.Values;
        _ = plan.Run(plan.CreateFrame(values, null, CancellationToken.None));
        _ = program.Evaluate(fixture.Bindings, cancellationToken: CancellationToken.None);

        long[] runs = new long[Counted];
        for (int i = 0; i < Counted; i++)
        {
            Frame frame = plan.CreateFrame(values, null, CancellationToken.None);
            long before = GC.GetAllocatedBytesForCurrentThread();
            CelValue value = plan.Run(frame);
            runs[i] = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.False(value.Kind == CelValueKind.Error, expression + ": " + value);
        }

        long facade = long.MaxValue;
        for (int i = 0; i < Counted; i++)
        {
            long before = GC.GetAllocatedBytesForCurrentThread();
            CelEvalResult<T> result = program.Evaluate(fixture.Bindings, cancellationToken: CancellationToken.None);
            facade = Math.Min(facade, GC.GetAllocatedBytesForCurrentThread() - before);
            Assert.True(result.IsSuccess && accepts(fixture, result.GetValueOrThrow()), expression);
        }

        return (runs, facade, Math.Min(runs[0], Math.Min(runs[1], runs[2])));
    }

    private sealed record Scenario(
        Func<Fixture, (long[] Runs, long Facade, long Run)> Measure,
        Func<Fixture, ulong> Budget,
        int Lines = 10,
        bool Discount = true,
        bool LastSkuOnly = false)
    {
        // A scenario that expects the value that `accepts` takes, with the budget in bytes of the fixture.
        internal static Scenario Of<T>(string expression, Func<Fixture, T> expected, Func<Fixture, ulong> budget) =>
            Of<T>(expression, (fixture, value) => EqualityComparer<T>.Default.Equals(value, expected(fixture)), budget);

        internal static Scenario Of<T>(string expression, Func<Fixture, T, bool> accepts, Func<Fixture, ulong> budget) =>
            new(fixture => MeasureProgram(expression, fixture, accepts), budget);
    }

    // The order of one scenario with its bindings: an object of the sample classes, bound as a pair with the description of the context.
    private sealed class Fixture
    {
        private Fixture(Order order, CelBindings bindings, int passing)
        {
            Order = order;
            Bindings = bindings;
            Passing = passing;
        }

        internal Order Order { get; }

        internal CelBindings Bindings { get; }

        // The number of lines with a quantity over 10, which E4 keeps.
        internal int Passing { get; }

        internal static Fixture Create(int lines, bool discount, bool lastSkuOnly)
        {
            var items = new OrderLine[lines];
            int passing = 0;
            for (int i = 0; i < lines; i++)
            {
                string number = i.ToString("D4", CultureInfo.InvariantCulture);
                items[i] = new OrderLine
                {
                    Sku = (lastSkuOnly && i != lines - 1 ? "ITEM-" : "SKU-") + number,
                    Qty = (i * 3 % 20) + 1,
                    Price = 10 + (1.5 * i),
                };
                passing += items[i].Qty > 10 ? 1 : 0;
            }

            var order = new Order
            {
                Id = "ORD-0001",
                Status = "approved",
                Currency = "RUB",
                Total = 1500,
                Discount = discount ? 100 : null,
                Created = Created,
                Lines = items,
                Customer = new Customer { Email = "alice@example.com" },
                Policy = "^[^@]+@[^@]+[.][a-z]+$",
            };
            // The variable now is one hour after the moment of the order.
            CelBindings bindings = new CelBindings()
                .Add("order", CelValue.FromObject(OrderType, order))
                .Add("now", CelTimestamp.FromDateTimeOffset(Created.AddHours(1)));
            return new Fixture(order, bindings, passing);
        }
    }
}
