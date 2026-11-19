using System;
using Great.Parts.Cel.Generator.Samples.Orders;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Great.Parts.Cel.DependencyInjection.Tests.Options;

// ValidateCel: the fields of the options type are the variables; false, a compilation error and an evaluation error fail the
// validation with the message, the last two with the code; the check applies to the options of its name and to the chosen environment.
[Trait("Requirement", "F-17")]
public sealed class ValidateCelTests
{
    private const string Message = "The city is not Paris";

    [Fact]
    public void TrueExpression_PassesTheValidation()
    {
        using ServiceProvider provider = Provider("city == 'Paris'", city: "Paris");

        Assert.Equal("Paris", provider.GetRequiredService<IOptions<Address>>().Value.City);
    }

    [Fact]
    public void FalseExpression_FailsWithTheMessage()
    {
        using ServiceProvider provider = Provider("city == 'Paris'", city: "Lyon");

        OptionsValidationException exception = Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<Address>>().Value);

        Assert.Equal(Message, Assert.Single(exception.Failures));
    }

    [Theory]
    [InlineData("town == 'Paris'", "UndeclaredReference")]
    [InlineData("1 / size(street) == 1", "DivisionByZero")]
    public static void CompilationOrEvaluationError_FailsWithTheMessageAndTheCode(string expression, string code)
    {
        using ServiceProvider provider = Provider(expression, city: "Paris");

        OptionsValidationException exception = Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<Address>>().Value);

        Assert.StartsWith($"{Message} ({code}: ", Assert.Single(exception.Failures), StringComparison.Ordinal);
    }

    // The options type is the input of a derived environment, so a type source must describe it.
    [Fact]
    public void OptionsTypeWithoutADescription_FailsWithTypeNotDeclared()
    {
        using ServiceProvider provider = new ServiceCollection()
            .AddCel(_ => { })
            .AddOptions<Address>()
            .ValidateCel("city == 'Paris'", Message)
            .Services
            .BuildServiceProvider();

        OptionsValidationException exception = Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<Address>>().Value);

        Assert.StartsWith($"{Message} (TypeNotDeclared: ", Assert.Single(exception.Failures), StringComparison.Ordinal);
    }

    // The check belongs to the named options of its builder: the options of the default name are not checked.
    [Fact]
    public void Check_AppliesOnlyToTheOptionsOfItsName()
    {
        using ServiceProvider provider = new ServiceCollection()
            .AddCel(builder => builder.AddTypes(ShopContext.Default))
            .AddOptions<Address>("shipping")
            .Configure(address => address.City = "Lyon")
            .ValidateCel("city == 'Paris'", Message)
            .Services
            .BuildServiceProvider();
        IOptionsMonitor<Address> monitor = provider.GetRequiredService<IOptionsMonitor<Address>>();

        Assert.Equal(string.Empty, monitor.Get(Microsoft.Extensions.Options.Options.DefaultName).City);
        Assert.Throws<OptionsValidationException>(() => monitor.Get("shipping"));
    }

    // With a name the check uses the environment registered with AddKeyedCel under it; no unnamed environment is needed.
    [Fact]
    public void EnvironmentName_SelectsTheKeyedEnvironment()
    {
        using ServiceProvider provider = new ServiceCollection()
            .AddKeyedCel("billing", builder => builder.AddTypes(BillingContext.Default))
            .AddOptions<Address>()
            .Configure(address => address.City = "Paris")
            .ValidateCel("city == 'Paris'", Message, "billing")
            .Services
            .BuildServiceProvider();

        Assert.Equal("Paris", provider.GetRequiredService<IOptions<Address>>().Value.City);
    }

    [Fact]
    public void ValidateCel_ChecksItsArguments()
    {
        OptionsBuilder<Address> builder = new ServiceCollection().AddOptions<Address>();

        Assert.Throws<ArgumentNullException>("builder", () => CelOptionsBuilderExtensions.ValidateCel<Address>(null!, "true", Message));
        Assert.Throws<ArgumentNullException>("expression", () => builder.ValidateCel(null!, Message));
        Assert.Throws<ArgumentNullException>("failureMessage", () => builder.ValidateCel("true", null!));
        Assert.Throws<ArgumentException>("environmentName", () => builder.ValidateCel("true", Message, string.Empty));
    }

    private static ServiceProvider Provider(string expression, string city) =>
        new ServiceCollection()
            .AddCel(builder => builder.AddTypes(ShopContext.Default))
            .AddOptions<Address>()
            .Configure(address =>
            {
                address.City = city;
                address.Street = string.Empty;
            })
            .ValidateCel(expression, Message)
            .Services
            .BuildServiceProvider();
}
