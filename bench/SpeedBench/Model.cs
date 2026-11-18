namespace SpeedBench;

// Application data of the scenarios: ordinary .NET classes with public properties.
// CEL names (order.total, l.qty, ...) are the camelCase / snake_case forms of these properties.

public sealed class Order
{
    public string Id { get; init; } = "";

    public string Status { get; init; } = "";

    public double Total { get; init; }

    public string Currency { get; init; } = "";

    public double? Discount { get; init; }

    public Customer Customer { get; init; } = new();

    public DateTimeOffset Created { get; init; }

    public IReadOnlyList<Line> Lines { get; init; } = [];
}

public sealed class Customer
{
    public string Name { get; init; } = "";

    public string Email { get; init; } = "";
}

public sealed class Line
{
    public string Sku { get; init; } = "";

    public long Qty { get; init; }

    public double Price { get; init; }
}
