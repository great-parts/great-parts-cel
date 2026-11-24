using Great.Parts.Cel;
using Great.Parts.Cel.Values;

// The type context lists the .NET types that an expression may read; the source generator of the package completes it at build time.
// UseInput makes the properties of Order variables of the environment: `total`, not `order.total`.
CelEnvironment environment = CelEnvironment.Create(builder => builder
    .AddTypes(ShopTypes.Default)
    .UseInput<Order>()
    .AddVariable<double>("limit"));

CelProgram<bool> needsReview = environment
    .Compile<bool>("total > limit && lines.exists(l, l.quantity > 10) && customer.country != 'DE'")
    .GetProgramOrThrow();

CelBindings bindings = new CelBindings().Add("limit", 1000.0);

Order small = new("A-1", 120.0, new Customer("FR"), [new OrderLine("pen", 3)]);
Order large = new("A-2", 4500.0, new Customer("FR"), [new OrderLine("pen", 40), new OrderLine("ink", 2)]);
Console.WriteLine(needsReview.EvaluateInput(small, bindings).GetValueOrThrow()); // Output: False
Console.WriteLine(needsReview.EvaluateInput(large, bindings).GetValueOrThrow()); // Output: True

[CelObject(typeof(Order))]
internal sealed partial class ShopTypes : CelTypeContext;

public sealed record Customer(string Country);

public sealed record OrderLine(string Sku, long Quantity);

public sealed record Order(string Id, double Total, Customer Customer, IReadOnlyList<OrderLine> Lines);
