using BenchmarkDotNet.Attributes;
using Great.Parts.Cel;
using Great.Parts.Cel.Values;
using SpeedBench;

namespace ThresholdBench;

/// <summary>
/// Two diagnostics of the simple entry <c>CelExpression.Evaluate</c>, without a verdict. They state where the time of the variant
/// of the rig goes, which is the same call as the one of the rig (the anonymous object with the scalars of the order, the customer
/// and the lines as properties, the classes not turned into <c>CelMap</c> values):
/// <list type="bullet">
/// <item><see cref="Evaluate"/>: the call on an anonymous object that was built before the measurement, with the text of the
/// variant with the input of the environment (the text of the scenario without the prefix <c>order.</c>);</item>
/// <item><see cref="Data"/>: the building of the data of the call, as the variant of the rig builds them.</item>
/// </list>
/// The anonymous type is registered by the source generator at the call in <see cref="Setup"/>, as in the rig.
/// </summary>
[Config(typeof(ThresholdConfig))]
public class SimpleEntryBenchmarks
{
    private Func<CelValue> _evaluate = null!;
    private Order _order = null!;
    private DateTimeOffset _now;

    /// <summary>The scalars only (scenario E1) and a macro over the 10 lines of the order (scenario E2).</summary>
    [Params("scalars", "lines")]
    public string Text { get; set; } = string.Empty;

    [GlobalSetup]
    public void Setup()
    {
        _order = OrderData.Make(10);
        _now = new DateTimeOffset(2026, 10, 13, 13, 0, 0, TimeSpan.Zero);
        string text = Text == "scalars"
            ? "total > 1000.0 && status == 'approved'"
            : "lines.all(l, l.qty > 0 && l.price >= 0.0)";
        var data = new
        {
            id = _order.Id,
            status = _order.Status,
            total = _order.Total,
            currency = _order.Currency,
            discount = _order.Discount,
            customer = _order.Customer,
            created = _order.Created,
            lines = _order.Lines,
            now = _now,
        };
        _evaluate = () => CelExpression.Evaluate<CelValue>(text, data);
        if (!_evaluate().TryGetBoolean(out bool result) || !result)
        {
            throw new InvalidOperationException("the call gives a wrong result for " + Text);
        }
    }

    /// <summary>The call on the data that were built before the measurement.</summary>
    [Benchmark(Baseline = true)]
    public CelValue Evaluate() => _evaluate();

    /// <summary>The building of the data of one call.</summary>
    [Benchmark]
    public object Data() => new
    {
        id = _order.Id,
        status = _order.Status,
        total = _order.Total,
        currency = _order.Currency,
        discount = _order.Discount,
        customer = _order.Customer,
        created = _order.Created,
        lines = _order.Lines,
        now = _now,
    };
}

/// <summary>
/// <c>CelMap.Create</c> on the row of an order, a diagnostic without a verdict: a map of three entries made with a collection
/// expression, the form that the variant of the rig used to turn a line into data.
/// </summary>
[Config(typeof(ThresholdConfig))]
public class CelMapBenchmarks
{
    private Line _line = null!;

    [GlobalSetup]
    public void Setup()
    {
        _line = OrderData.Make(1).Lines[0];
        if (LineMap().Count != 3)
        {
            throw new InvalidOperationException("the map of a line has not three entries");
        }
    }

    [Benchmark]
    public CelMap LineMap() => CelMap.Create(
    [
        Entry("sku", CelValue.FromString(_line.Sku)),
        Entry("qty", CelValue.FromInt64(_line.Qty)),
        Entry("price", CelValue.FromDouble(_line.Price)),
    ]);

    private static KeyValuePair<CelValue, CelValue> Entry(string key, CelValue value) => new(CelValue.FromString(key), value);
}

// An order of the rig (the same values as Scenarios.Order of SpeedBench) with the given number of lines.
internal static class OrderData
{
    internal static Order Make(int lines)
    {
        var items = new Line[lines];
        for (int i = 0; i < lines; i++)
        {
            items[i] = new Line { Sku = "SKU-" + i.ToString("D4", System.Globalization.CultureInfo.InvariantCulture), Qty = i * 3 % 20 + 1, Price = 10.0 + i * 1.5 };
        }

        return new Order
        {
            Id = "ORD-0001",
            Status = "approved",
            Total = 1500.0,
            Currency = "RUB",
            Discount = 100.0,
            Customer = new Customer { Name = "Ivan Petrov", Email = "buyer@example.com" },
            Created = new DateTimeOffset(2026, 10, 13, 12, 0, 0, TimeSpan.Zero),
            Lines = items,
        };
    }
}
