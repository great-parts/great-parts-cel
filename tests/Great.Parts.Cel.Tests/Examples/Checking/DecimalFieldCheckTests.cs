#if NET11_0_OR_GREATER
using System.Collections.Generic;
using System.Numerics;
using Great.Parts.Cel.Tests.Examples.Library;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Examples.Checking;

// A field of an object type whose CEL type is decimal, in an environment that has the set and in one that has not (the page of the extension
// set decimal, "An environment without the set"; the page of type checking, "decimal without the set"). A type source can describe such
// a field — a generated type, a type made at run time with CelType.Decimal — without the environment having the set that declares
// the type, and the description is valid in every environment: Build accepts it, the other fields of the type work, and the field is
// refused where an expression selects it. The selection of the field, and has() of it, is FieldNotAccessible (2007) at the check: at
// the "." of the selection and at the "(" of has(), with the text "field '<field>' of type '<type>' is not available to expressions:
// the CEL type decimal needs the extension set 'decimal'"; it is so for a field of the type decimal and for a field of a list or a map
// of decimals, and for the field of a type that is itself a field of another. With the set the field is read as a decimal. The
// type is described here by hand with CelObjectTypeBuilder; a member of the .NET type Decimal128 or System.Decimal of a type that
// the generator described is the same field (GeneratedDecimalTests). Rows are those of ExtensionRows; order is a variable of the type
// acme.sales.Order, whose fields are price (decimal), prices (list(decimal)), rates (map(string, decimal)), title (string) and line (the type
// acme.sales.Line with the field price).
[Trait("Requirement", "F-2")]
[Trait("Requirement", "F-5")]
public sealed class DecimalFieldCheckTests
{
    private const string Reason = "is not available to expressions: the CEL type decimal needs the extension set 'decimal'";

    private static readonly CelObjectType Line = new CelObjectTypeBuilder<LineCarrier>("acme.sales.Line")
        .AddField("price", CelType.Decimal, carrier => CelValue.FromDecimal(carrier.Price))
        .Build();

    private static readonly CelObjectType Order = new CelObjectTypeBuilder<OrderCarrier>("acme.sales.Order")
        .AddField("price", CelType.Decimal, carrier => CelValue.FromDecimal(carrier.Price))
        .AddField("prices", CelType.ListOf(CelType.Decimal), carrier => CelValue.FromList(CelList.Create(CelValue.FromDecimal(carrier.Price), CelValue.FromDecimal(DecimalExample.Make(25, -1)))))
        .AddField("rates", CelType.MapOf(CelType.String, CelType.Decimal), carrier => CelValue.FromMap(CelMap.Create([new KeyValuePair<CelValue, CelValue>(CelValue.FromString("a"), CelValue.FromDecimal(carrier.Price))])))
        .AddField("title", CelType.String, carrier => CelValue.FromString(carrier.Title))
        .AddField("line", Line.Type, carrier => CelValue.FromObject(Line, new LineCarrier { Price = carrier.Price }))
        .Build();

    private const string WithoutTheSet = $$"""
        Check | order^.price | FieldNotAccessible | field 'price' of type 'acme.sales.Order' {{Reason}}
        Check | order^.prices | FieldNotAccessible | field 'prices' of type 'acme.sales.Order' {{Reason}}
        Check | order^.rates | FieldNotAccessible | field 'rates' of type 'acme.sales.Order' {{Reason}}
        Check | has^(order.price) | FieldNotAccessible | field 'price' of type 'acme.sales.Order' {{Reason}}
        Check | has^(order.prices) | FieldNotAccessible | field 'prices' of type 'acme.sales.Order' {{Reason}}
        Check | order.line^.price | FieldNotAccessible | field 'price' of type 'acme.sales.Line' {{Reason}}
        Check | order^.price + order^.price | FieldNotAccessible FieldNotAccessible | field 'price' of type 'acme.sales.Order' {{Reason}} ;; field 'price' of type 'acme.sales.Order' {{Reason}}
        Check | order^.missing | NoSuchField | undefined field 'missing' in type 'acme.sales.Order'
        """;

    private const string WithTheSet = """
        string(order.price) => '1.50'
        order.price == 1.5m => true
        order.price > 1m => true
        has(order.price) => true
        type(order.price) == decimal => true
        string(order.prices[0]) => '1.50'
        order.prices == [1.5m, 2.5m] => true
        order.prices[1] > order.prices[0] => true
        string(order.rates['a']) => '1.50'
        string(order.line.price) => '1.50'
        string(order.price + order.line.price) => '3.00'
        order.title == 'a' => true
        """;

    private readonly CelTypeProvider provider = CelTypeProvider.Create("sales", Order, Line);

    private readonly CelBindings bindings = new CelBindings().Add("order", new OrderCarrier { Price = DecimalExample.Make(150, -2), Title = "a" });

    // A description with a decimal field builds without the set, and its other fields compile.
    [Fact]
    [Trait("Requirement", "F-4")]
    public void TypeWithADecimalField_BuildsWithoutTheSet_AndItsOtherFieldsCompile()
    {
        CelEnvironment environment = Environment(withTheSet: false);

        Assert.True(environment.Compile<CelValue>("order.title == 'a' && has(order.title)").IsSuccess);
        Assert.True(environment.Compile<CelValue>("type(order) == acme.sales.Order").IsSuccess);
    }

    [Fact]
    [Trait("Requirement", "F-4")]
    [Trait("Requirement", "F-8")]
    public void SelectionOfADecimalFieldWithoutTheSet_IsFieldNotAccessibleAtTheSelection() =>
        Assert.Empty(ExtensionRows.CheckErrors(Environment(withTheSet: false), WithoutTheSet));

    [Fact]
    public void DecimalFieldWithTheSet_IsReadAsADecimal() => Assert.Empty(ExtensionRows.CheckValues(Environment(withTheSet: true), WithTheSet, bindings));

    private CelEnvironment Environment(bool withTheSet)
    {
        CelEnvironmentBuilder builder = withTheSet ? DecimalExample.Builder() : new CelEnvironmentBuilder(CelStandardLibrary.V1);

        return builder.AddTypes(provider).AddVariable("order", Order.Type).Build().GetEnvironmentOrThrow();
    }

    private sealed class OrderCarrier
    {
        public Decimal128 Price { get; init; }

        public string Title { get; init; } = string.Empty;
    }

    private sealed class LineCarrier
    {
        public Decimal128 Price { get; init; }
    }
}
#endif
