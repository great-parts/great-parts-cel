using System;
using System.Globalization;
using System.Threading;
using Great.Parts.Cel.Generator.Samples.Orders;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Facade;

// The reserve of the built-in limit of the simple entry: the cost of each of the scenarios E1-E9, on orders of 10 and 100
// lines, in an environment that has the limit, times 100 is at most CelExpression.DefaultCostLimit. The texts and the data are those
// of the scenarios of the speed measurements; the order is read through the type context of the generator samples.
// No other library and no result of another library is used.
[Trait("Requirement", "U-7")]
public sealed class SimpleEntryBuiltInLimitTests
{
    private static readonly string[] Expressions =
    [
        "order.total > 1000.0 && order.status == 'approved'",
        "order.lines.all(l, l.qty > 0 && l.price >= 0.0)",
        "order.lines.exists(l, l.sku.startsWith('SKU-'))",
        "order.lines.filter(l, l.qty > 10).map(l, double(l.qty) * l.price)",
        "order.customer.email.matches('^[^@]+@[^@]+[.][a-z]+$')",
        "has(order.discount) ? order.total - order.discount : order.total",
        "order.created + duration('72h') > now",
        "size(order.lines) <= 100 && order.currency in ['RUB', 'USD', 'EUR']",
        "'Order ' + order.id + ': ' + order.status",
    ];

    private static readonly DateTimeOffset Created = new(2026, 10, 13, 12, 0, 0, TimeSpan.Zero);

    private static readonly CelEnvironment Environment = new CelEnvironmentBuilder()
        .AddTypes(ShopContext.Default)
        .AddVariable<Order>("order")
        .AddVariable<DateTimeOffset>("now")
        .SetDefaultCostLimit(CelExpression.DefaultCostLimit)
        .Build()
        .GetEnvironmentOrThrow();

    public static TheoryData<string, int, int> Cases() => new()
    {
        { "E1", 1, 10 },
        { "E2/10", 2, 10 },
        { "E2/100", 2, 100 },
        { "E3/10", 3, 10 },
        { "E3/100", 3, 100 },
        { "E4/10", 4, 10 },
        { "E4/100", 4, 100 },
        { "E5", 5, 10 },
        { "E6", 6, 10 },
        { "E7", 7, 10 },
        { "E8", 8, 10 },
        { "E9", 9, 10 },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public void MarginOverP3(string scenario, int number, int lines)
    {
        CelProgram<CelValue> program = Environment.Compile<CelValue>(Expressions[number - 1]).GetProgramOrThrow();
        CelBindings bindings = new CelBindings().Add("order", OrderOf(lines)).Add("now", Created.AddHours(1));

        CelEvalResult<CelValue> result = program.Evaluate(bindings, cancellationToken: CancellationToken.None);
        _ = result.GetValueOrThrow();
        ulong cost = result.Cost!.Value;

        TestContext.Current.TestOutputHelper?.WriteLine(string.Create(CultureInfo.InvariantCulture, $"{scenario}: cost {cost}, x100 = {cost * 100UL}"));
        Assert.True(
            cost * 100UL <= CelExpression.DefaultCostLimit,
            string.Create(CultureInfo.InvariantCulture, $"{scenario}: cost {cost} x 100 > {CelExpression.DefaultCostLimit}"));
    }

    // The order of the scenarios with the given number of lines.
    private static Order OrderOf(int lines)
    {
        var items = new OrderLine[lines];
        for (int i = 0; i < lines; i++)
        {
            items[i] = new OrderLine
            {
                Sku = "SKU-" + i.ToString("D4", CultureInfo.InvariantCulture),
                Qty = (i * 3 % 20) + 1,
                Price = 10.0 + (i * 1.5),
            };
        }

        return new Order
        {
            Id = "ORD-0001",
            Status = "approved",
            Total = 1500.0,
            Currency = "RUB",
            Discount = 100.0,
            Customer = new Customer { Email = "buyer@example.com" },
            Created = Created,
            Lines = items,
        };
    }
}
