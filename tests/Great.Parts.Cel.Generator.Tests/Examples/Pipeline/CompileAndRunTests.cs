using System.Linq;
using System.Threading;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Generator.Tests.Examples.Pipeline;

// The created code is compiled in memory together with its types, loaded in a collectible assembly load context, and read as
// an application reads it: the context is added to an environment, an expression selects a field of an instance. One
// context with the rows that need the compiler to prove them — the value, has(), null, an excluded member, an enumeration.
// Each row of the type mapping read over the sample types is in the tests of the library project; here is only what shows that
// the file is code that works.
public sealed class CompileAndRunTests
{
    private const string Source = """
        using System.Collections.Generic;
        using Great.Parts.Cel.Values;
        namespace Acme.Sales;
        [CelObject(typeof(Order), Name = "acme.Order")]
        public partial class OrderContext : CelTypeContext { }
        public enum Status { Draft, Approved }
        public class OrderLine { public string Sku { get; set; } = ""; public long Qty { get; set; } }
        public class Order
        {
            public string Currency { get; set; } = "";
            public string? Note { get; set; }
            public string Must { get; set; } = null!;
            public List<OrderLine> Lines { get; set; } = new();
            public Status Status { get; set; }
            public System.IO.Stream? Body { get; set; }
            public static Order Sample() => new()
            {
                Currency = "RUB",
                Lines = { new OrderLine { Sku = "A", Qty = 1 }, new OrderLine { Sku = "B", Qty = 2 } },
                Status = Status.Approved,
            };
        }
        """;

    [Fact]
    [Trait("Requirement", "F-2")]
    [Trait("Requirement", "S-1")]
    public void CreatedCode_ReadsFieldsThroughTheDescription()
    {
        using var loaded = new GeneratorExample.Loaded(Source);
        CelEnvironment environment = Environment(loaded);
        CelBindings bindings = Bindings(loaded);

        Assert.True(Evaluate<bool>(environment, "order.currency == 'RUB'", bindings));
        Assert.True(Evaluate<bool>(environment, "order.lines.size() == 2 && order.lines[1].sku == 'B' && order.lines[1].qty == 2", bindings));
        Assert.True(Evaluate<bool>(environment, "order.status == Acme.Sales.Status.Approved", bindings));
        Assert.True(Evaluate<bool>(environment, "type(order) == acme.Order", bindings));
        Assert.Same(loaded.Provider("Acme.Sales.OrderContext"), loaded.Provider("Acme.Sales.OrderContext"));
    }

    // has() is "the member is not null"; a null that the CEL type allows is the null value, and one that it does not allow is
    // UnexpectedNull at the selection.
    [Fact]
    [Trait("Requirement", "F-2")]
    [Trait("Requirement", "F-4")]
    public void NullMembers_AreHasFalseNullOrUnexpectedNull()
    {
        using var loaded = new GeneratorExample.Loaded(Source);
        CelEnvironment environment = Environment(loaded);
        CelBindings bindings = Bindings(loaded);

        Assert.True(Evaluate<bool>(environment, "!has(order.note) && order.note == null", bindings));
        Assert.True(Evaluate<bool>(environment, "has(order.currency) && !has(order.must)", bindings));
        CelEvalResult<CelValue> result = environment.Compile<CelValue>("order.must").GetProgramOrThrow().Evaluate(bindings, cancellationToken: CancellationToken.None);
        CelError error = Assert.Single(result.Errors);
        Assert.Equal(CelErrorCode.UnexpectedNull, error.Code);
        Assert.Equal(CelErrorStage.Evaluate, error.Stage);
        Assert.Equal(5, error.Location.Offset);
    }

    // The excluded member is not a field: the check says why (the reason of the generator), and the member that is not there at
    // all is NoSuchField.
    [Fact]
    [Trait("Requirement", "F-2")]
    [Trait("Requirement", "F-4")]
    public void ExcludedMember_IsFieldNotAccessibleWithTheReason()
    {
        using var loaded = new GeneratorExample.Loaded(Source);
        CelEnvironment environment = Environment(loaded);

        CelCompileResult<CelValue> excluded = environment.Compile<CelValue>("order.body");
        CelCompileResult<CelValue> missing = environment.Compile<CelValue>("order.nothing");

        CelError error = Assert.Single(excluded.Errors);
        Assert.Equal(CelErrorCode.FieldNotAccessible, error.Code);
        Assert.Equal("field 'body' of type 'acme.Order' is not available to expressions: streams are not data", error.Message);
        Assert.Equal(5, error.Location.Offset);
        Assert.Equal(CelErrorCode.NoSuchField, Assert.Single(missing.Errors).Code);
    }

    // An enumeration is described with its constants, in declaration order; the description of the context lists the object
    // types and the enumerations that the closure found, sorted by CEL name.
    [Fact]
    [Trait("Requirement", "F-2")]
    public void Context_ListsTheClosureOfItsRoots()
    {
        using var loaded = new GeneratorExample.Loaded(Source);

        CelTypeProvider provider = loaded.Provider("Acme.Sales.OrderContext");

        Assert.Equal("OrderContext", provider.SourceName);
        Assert.Equal(["Acme.Sales.OrderLine", "acme.Order"], provider.ObjectTypes.Select(type => type.Name));
        CelEnumType status = Assert.Single(provider.EnumTypes);
        Assert.Equal("Acme.Sales.Status", status.Name);
        Assert.Equal(["Draft", "Approved"], status.Constants.Select(constant => constant.Key));
        Assert.Equal([0L, 1L], status.Constants.Select(constant => constant.Value));
    }

    private static CelEnvironment Environment(GeneratorExample.Loaded loaded) =>
        new CelEnvironmentBuilder(CelStandardLibrary.V1)
            .AddTypes(loaded.Provider("Acme.Sales.OrderContext"))
            .AddVariable("order", CelType.ObjectNamed("acme.Order"))
            .Build()
            .GetEnvironmentOrThrow();

    private static CelBindings Bindings(GeneratorExample.Loaded loaded) =>
        new CelBindings().Add("order", loaded.Assembly.GetType("Acme.Sales.Order")!.GetMethod("Sample")!.Invoke(null, null)!);

    private static T Evaluate<T>(CelEnvironment environment, string text, CelBindings bindings) =>
        environment.Compile<T>(text).GetProgramOrThrow().Evaluate(bindings, cancellationToken: CancellationToken.None).GetValueOrThrow();
}
