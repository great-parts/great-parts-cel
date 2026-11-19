using System;
using System.Threading;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Generator.Samples.Orders;
using Xunit;

namespace Great.Parts.Cel.Tests.Examples.Facade;

// The input of an environment: UseInput<TInput>() makes the fields of a described type variables of the top level; a program
// evaluates on an instance with EvaluateInput, with or without bindings of other variables; the fields are read only from the
// input. The type is described by the context of the samples (ShopContext: CEL field names are camel case, total, status,
// lines, …).
[Trait("Requirement", "F-12")]
public sealed class InputTests
{
    private static readonly CelEnvironment Environment = new CelEnvironmentBuilder()
        .AddTypes(ShopContext.Default)
        .UseInput<Order>()
        .AddVariable<double>("threshold")
        .Build()
        .GetEnvironmentOrThrow();

    private static readonly Order Big = new() { Id = "ORD-1", Status = "approved", Total = 1500.0, Lines = [new OrderLine { Sku = "SKU-1", Qty = 2, Price = 750.0 }] };

    [Fact]
    public void FieldsOfTheInput_AreVariables()
    {
        CelProgram<bool> program = Environment.Compile<bool>("total > 1000.0 && status == 'approved' && lines.all(l, l.qty > 0)").GetProgramOrThrow();

        Assert.True(program.EvaluateInput(Big, cancellationToken: CancellationToken.None).GetValueOrThrow());
        Assert.True(program.EvaluateInput(Big, null, null, CancellationToken.None).GetValueOrThrow());
        Assert.False(program.EvaluateInput(new Order { Total = 10.0, Status = "approved" }, cancellationToken: CancellationToken.None).GetValueOrThrow());
    }

    [Fact]
    public void InputAndBindings_Together()
    {
        CelProgram<bool> program = Environment.Compile<bool>("total > threshold").GetProgramOrThrow();

        Assert.True(program.EvaluateInput(Big, new CelBindings().Add("threshold", 1000.0), cancellationToken: CancellationToken.None).GetValueOrThrow());
        Assert.False(program.EvaluateInput(Big, new CelBindings().Add("threshold", 2000.0), cancellationToken: CancellationToken.None).GetValueOrThrow());
    }

    // A binding with the name of a field is not read: the field comes from the input.
    [Fact]
    public void BindingWithTheNameOfAField_IsNotUsed()
    {
        CelProgram<double> program = Environment.Compile<double>("total").GetProgramOrThrow();

        Assert.Equal(1500.0, program.EvaluateInput(Big, new CelBindings().Add("total", 1.0), cancellationToken: CancellationToken.None).GetValueOrThrow());
    }

    // The options without bindings are passed by name; the limit of the call applies.
    [Fact]
    public void OptionsWithoutBindings_ArePassedByName()
    {
        CelEvalResult<bool> result = Environment.Compile<bool>("lines.all(l, l.qty > 0)").GetProgramOrThrow()
            .EvaluateInput(Big, options: new CelEvalOptions { CostLimit = 1 }, cancellationToken: CancellationToken.None);

        Assert.Equal(CelErrorCode.CostLimitExceeded, Assert.Single(result.Errors).Code);
    }

    [Fact]
    public void EvaluateWithoutTheInput_IsUnboundAtTheFirstField()
    {
        CelEvalResult<bool> result = Environment.Compile<bool>("threshold > 0.0 && total > threshold").GetProgramOrThrow()
            .Evaluate(new CelBindings().Add("threshold", 1.0), cancellationToken: CancellationToken.None);

        CelError error = Assert.Single(result.Errors);
        Assert.Equal(CelErrorCode.Unbound, error.Code);
        Assert.Equal("total", error.Subject);
        Assert.Equal(19, error.Location.Offset);
    }

    [Fact]
    public void OneStepWithTheInput_GivesTheSameResult()
    {
        Assert.True(Environment.EvaluateInput<bool>("total > threshold", Big, new CelBindings().Add("threshold", 1.0), cancellationToken: CancellationToken.None).GetValueOrThrow());
        Assert.Equal("ORD-1", Environment.EvaluateInput<string>("id", Big, cancellationToken: CancellationToken.None).GetValueOrThrow());
    }

    [Fact]
    public void ArgumentsOfTheCall_AreChecked()
    {
        CelProgram<double> program = Environment.Compile<double>("total").GetProgramOrThrow();
        CelProgram<long> withoutInput = new CelEnvironmentBuilder().Build().GetEnvironmentOrThrow().Compile<long>("1").GetProgramOrThrow();

        Assert.Throws<ArgumentNullException>("input", () => program.EvaluateInput<Order>(null!, cancellationToken: CancellationToken.None));
        Assert.Throws<ArgumentException>("input", () => program.EvaluateInput(new Invoice(), cancellationToken: CancellationToken.None));
        Assert.Throws<ArgumentException>("input", () => withoutInput.EvaluateInput(Big, cancellationToken: CancellationToken.None));
        Assert.Throws<ArgumentException>("input", () => Environment.EvaluateInput<double>("total", "not an order", cancellationToken: CancellationToken.None));
    }

    [Fact]
    public void SecondInputType_IsInputConflict_AndTheSameTypeTwiceIsOne()
    {
        CelBuildResult conflict = new CelEnvironmentBuilder().AddTypes(ShopContext.Default).AddTypes(BillingContext.Default)
            .UseInput<Order>().UseInput<Invoice>().Build();
        CelBuildResult twice = new CelEnvironmentBuilder().AddTypes(ShopContext.Default).UseInput<Order>().UseInput<Order>().Build();

        Assert.Equal(CelErrorCode.InputConflict, Assert.Single(conflict.Errors).Code);
        Assert.True(twice.IsSuccess);
    }

    [Fact]
    public void InputWithoutADescription_IsTypeNotDeclared()
    {
        CelError error = Assert.Single(new CelEnvironmentBuilder().UseInput<Order>().Build().Errors);

        Assert.Equal(CelErrorCode.TypeNotDeclared, error.Code);
        Assert.Equal(typeof(Order).FullName, error.Subject);
    }

    // A field with the name of another variable: both sources are named.
    [Fact]
    public void FieldWithTheNameOfAVariable_IsDuplicateVariable()
    {
        CelError error = Assert.Single(new CelEnvironmentBuilder().AddTypes(ShopContext.Default)
            .AddVariable<double>("total").UseInput<Order>().Build().Errors);

        Assert.Equal(CelErrorCode.DuplicateVariable, error.Code);
        Assert.Equal("total", error.Subject);
        Assert.Contains("input " + typeof(Order).FullName, error.Message, StringComparison.Ordinal);
    }

    // ToBuilder keeps the input.
    [Fact]
    public void ToBuilder_KeepsTheInput()
    {
        CelEnvironment derived = Environment.ToBuilder().AddVariable<long>("extra").Build().GetEnvironmentOrThrow();

        Assert.Equal(1500.0, derived.Compile<double>("total").GetProgramOrThrow().EvaluateInput(Big, cancellationToken: CancellationToken.None).GetValueOrThrow());
    }
}
