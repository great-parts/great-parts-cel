using System.Globalization;

namespace SpeedBench;

/// <summary>One case: an expression E1..E9 on an order with the given number of lines.</summary>
public sealed record Scenario(string Id, int Number, int Lines)
{
    public string Expression => Scenarios.Expressions[Number - 1];

    public override string ToString() => Id;
}

public static class Scenarios
{
    // The texts of the expressions E1..E9.
    public static readonly string[] Expressions =
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

    /// <summary>The 12 cases: E1, E5..E9 on 10 lines; E2..E4 on 10 and 100 lines.</summary>
    public static readonly IReadOnlyList<Scenario> All =
    [
        new("E1", 1, 10),
        new("E2/10", 2, 10),
        new("E2/100", 2, 100),
        new("E3/10", 3, 10),
        new("E3/100", 3, 100),
        new("E4/10", 4, 10),
        new("E4/100", 4, 100),
        new("E5", 5, 10),
        new("E6", 6, 10),
        new("E7", 7, 10),
        new("E8", 8, 10),
        new("E9", 9, 10),
    ];

    public static readonly DateTimeOffset Created = new(2026, 10, 13, 12, 0, 0, TimeSpan.Zero);

    /// <summary>The variable now: one hour after the order was created.</summary>
    public static readonly DateTimeOffset Now = Created.AddHours(1);

    public static Scenario Get(string id) =>
        All.FirstOrDefault(s => s.Id == id) ?? throw new ArgumentException("unknown scenario " + id);

    /// <summary>
    /// Cases selected by the environment variable BENCH_CASES (comma-separated ids, for example "E1,E2/10");
    /// all cases when it is not set. run.mjs sets it from the option --cases.
    /// </summary>
    public static IEnumerable<Scenario> Selected()
    {
        var filter = Environment.GetEnvironmentVariable("BENCH_CASES");
        if (string.IsNullOrWhiteSpace(filter))
        {
            return All;
        }

        var ids = filter.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return ids.Select(Get);
    }

    /// <summary>The order of the scenarios as .NET objects, with the given number of lines.</summary>
    public static Order Order(int lines)
    {
        var items = new Line[lines];
        for (var i = 0; i < lines; i++)
        {
            items[i] = new Line
            {
                Sku = "SKU-" + i.ToString("D4", CultureInfo.InvariantCulture),
                Qty = i * 3 % 20 + 1,
                Price = 10.0 + i * 1.5,
            };
        }

        return new Order
        {
            Id = "ORD-0001",
            Status = "approved",
            Total = 1500.0,
            Currency = "RUB",
            Discount = 100.0,
            Customer = new Customer { Name = "Ivan Petrov", Email = "buyer@example.com" },
            Created = Created,
            Lines = items,
        };
    }

    /// <summary>
    /// Expected result of a case as canonical text (<see cref="Canon"/>). Literal values follow the CEL
    /// semantics of each expression on the data above; E4 is computed from the order lines directly.
    /// </summary>
    public static string Expected(Scenario s)
    {
        switch (s.Number)
        {
            case 4:
                var lines = Order(s.Lines).Lines;
                return Canon.Format(lines.Where(l => l.Qty > 10).Select(l => (double)l.Qty * l.Price).ToList());
            case 6:
                return "double:1400";
            case 9:
                return "string:'Order ORD-0001: approved'";
            default:
                return "bool:true";
        }
    }
}
