using System;
using System.Threading;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Generator.Samples.Kinds;
using Great.Parts.Cel.Generator.Samples.Orders;
using Xunit;

namespace Great.Parts.Cel.Tests.Facade;

// What the examples of the input of an environment (InputTests) leave out: a base class and an interface as the input type, a field
// without a value, the input together with the limit of the environment, and the one-step call of a text that does not compile.
[Trait("Requirement", "F-12")]
public sealed class EnvironmentInputTests
{
    // The input type is the class of the description; an instance of a derived class belongs to it and its fields are read.
    [Fact]
    public void InputOfABaseClass_TakesAnInstanceOfADerivedClass()
    {
        CelEnvironment environment = CelEnvironment.Create(builder => builder.AddTypes(ShopContext.Default).UseInput<Contact>());

        CelProgram<string> program = environment.Compile<string>("name").GetProgramOrThrow();

        Assert.Equal("vip", program.EvaluateInput(new VipContact { Name = "vip", Level = 3 }, cancellationToken: CancellationToken.None).GetValueOrThrow());
        Assert.Equal("plain", program.EvaluateInput(new Contact { Name = "plain" }, cancellationToken: CancellationToken.None).GetValueOrThrow());
    }

    // An instance that implements the input interface belongs to it, whatever its class has besides.
    [Fact]
    public void InputOfAnInterface_ReadsTheFieldsOfTheInterface()
    {
        CelEnvironment environment = CelEnvironment.Create(builder => builder.AddTypes(KindsContext.Default).UseInput<IShape>());

        CelEvalResult<double> result = environment.Compile<double>("area").GetProgramOrThrow()
            .EvaluateInput<IShape>(new Circle { Id = 1, Label = "c", Area = 12.5, Radius = 2 }, cancellationToken: CancellationToken.None);

        Assert.Equal(12.5, result.GetValueOrThrow());
        Assert.Throws<ArgumentException>("input", () => environment.Compile<double>("area").GetProgramOrThrow().EvaluateInput(new Item(), cancellationToken: CancellationToken.None));
    }

    // A field that has no value is read as null, and the other fields still are read.
    [Fact]
    public void FieldWithoutAValue_IsNull()
    {
        CelEnvironment environment = CelEnvironment.Create(builder => builder.AddTypes(ShopContext.Default).UseInput<Order>());
        CelProgram<bool> program = environment.Compile<bool>("discount == null && total == 10.0").GetProgramOrThrow();

        Assert.True(program.EvaluateInput(new Order { Total = 10.0, Discount = null }, cancellationToken: CancellationToken.None).GetValueOrThrow());
        Assert.False(program.EvaluateInput(new Order { Total = 10.0, Discount = 1.0 }, cancellationToken: CancellationToken.None).GetValueOrThrow());
    }

    // ToBuilder carries the input and the limit together, and the limit of the new environment is the one that was set.
    [Fact]
    public void ToBuilder_CarriesTheInputAndTheLimitTogether()
    {
        CelEnvironment environment = new CelEnvironmentBuilder()
            .AddTypes(ShopContext.Default)
            .UseInput<Order>()
            .SetDefaultCostLimit(100)
            .Build()
            .GetEnvironmentOrThrow();

        CelEnvironment derived = environment.ToBuilder().AddVariable<long>("extra").Build().GetEnvironmentOrThrow();

        Assert.Equal(100UL, derived.DefaultCostLimit);
        Assert.Equal(typeof(Order), derived.InputType?.ClrType);
        CelEvalResult<double> result = derived.Compile<double>("total + 1.0").GetProgramOrThrow().EvaluateInput(new Order { Total = 2.0 }, cancellationToken: CancellationToken.None);
        Assert.Equal(3.0, result.GetValueOrThrow());
        Assert.Equal(2UL, result.Cost);
    }

    // The limit that is set again replaces the one before; it is no conflict.
    [Fact]
    public void SetDefaultCostLimit_Again_ReplacesTheLimit()
    {
        CelEnvironment environment = new CelEnvironmentBuilder().SetDefaultCostLimit(1).SetDefaultCostLimit(5).Build().GetEnvironmentOrThrow();

        Assert.Equal(5UL, environment.DefaultCostLimit);
    }

    // The one-step call whose text does not compile gives the compilation errors and no cost.
    [Fact]
    public void OneStepCall_WithACompilationError_GivesTheErrorsAndNoCost()
    {
        CelEnvironment environment = new CelEnvironmentBuilder().AddTypes(ShopContext.Default).UseInput<Order>().SetDefaultCostLimit(100).Build().GetEnvironmentOrThrow();

        CelEvalResult<bool> result = environment.EvaluateInput<bool>("total >", new Order(), cancellationToken: CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(CelErrorStage.Parse, Assert.Single(result.Errors).Stage);
        Assert.Null(result.Cost);
    }

    [Fact]
    public void InputOfTheOneStepCall_IsCheckedBeforeTheText()
    {
        CelEnvironment withoutInput = CelEnvironment.Create(_ => { });

        Assert.Throws<ArgumentException>("input", () => withoutInput.EvaluateInput<long>("1 +", new Order(), cancellationToken: CancellationToken.None));
        Assert.Throws<ArgumentNullException>("input", () => withoutInput.EvaluateInput<long>("1", null!, cancellationToken: CancellationToken.None));
        Assert.Throws<ArgumentNullException>("expression", () => withoutInput.EvaluateInput<long>(null!, new Order(), cancellationToken: CancellationToken.None));
        Assert.Null(withoutInput.InputType);
    }
}
