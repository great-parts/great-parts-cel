using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Great.Parts.Cel.DependencyInjection;
using Great.Parts.Cel.Generator.Samples.Orders;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Great.Parts.Cel.AspNetCore.Tests.Authorization;

// Authorization policies with CEL expressions, through IAuthorizationService: the variable user, the variable resource, and the
// denial with a reason for false and for every error.
[Trait("Requirement", "F-17")]
public sealed class RequireCelTests
{
    private const string Policy = "rule";

    private static readonly ClaimsPrincipal Manager = new(new ClaimsIdentity(
        [new Claim("role", "manager"), new Claim("role", "sales"), new Claim("department", "sales")],
        "test"));

    [Fact]
    public async Task TrueExpression_AllowsAccess()
    {
        using ServiceProvider provider = Provider(policy => policy.RequireCel("'manager' in user.role && user.department == ['sales']"));

        AuthorizationResult result = await Authorize(provider, null).ConfigureAwait(true);

        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData("'admin' in user.role", "The CEL expression of the requirement gave false.")]
    [InlineData("user.role == 'manager'", "The CEL expression of the requirement failed with the error NoMatchingOverload.")]
    [InlineData("user.team == ['a']", "The CEL expression of the requirement failed with the error EvalNoSuchField.")]
    public async Task FalseOrError_DeniesWithAReason(string expression, string reason)
    {
        using ServiceProvider provider = Provider(policy => policy.RequireCel(expression));

        AuthorizationResult result = await Authorize(provider, null).ConfigureAwait(true);

        Assert.False(result.Succeeded);
        Assert.Equal(reason, Assert.Single(result.Failure!.FailureReasons).Message);
    }

    [Fact]
    public async Task ResourceOfTheType_IsTheVariableResource()
    {
        using ServiceProvider provider = Provider(policy => policy.RequireCel<Address>("resource.city == 'Paris' && 'sales' in user.role"));

        AuthorizationResult allowed = await Authorize(provider, new Address { City = "Paris" }).ConfigureAwait(true);
        AuthorizationResult denied = await Authorize(provider, new Address { City = "Lyon" }).ConfigureAwait(true);

        Assert.True(allowed.Succeeded);
        Assert.False(denied.Succeeded);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Paris")]
    public async Task ResourceOfAnotherTypeOrNone_DeniesAccess(object? resource)
    {
        using ServiceProvider provider = Provider(policy => policy.RequireCel<Address>("true"));

        AuthorizationResult result = await Authorize(provider, resource).ConfigureAwait(true);

        Assert.False(result.Succeeded);
        Assert.Equal("The resource of the authorization is not an instance of Address.", Assert.Single(result.Failure!.FailureReasons).Message);
    }

    // The handler declares user in an environment derived from the registered one, so a variable user there is a build error that denies.
    [Fact]
    public async Task RegisteredVariableNamedUser_DeniesWithTheBuildError()
    {
        using ServiceProvider provider = Provider(policy => policy.RequireCel("true"), environment => environment.AddVariable<string>("user"));

        AuthorizationResult result = await Authorize(provider, null).ConfigureAwait(true);

        Assert.Equal(
            "The CEL expression of the requirement failed with the error DuplicateVariable.",
            Assert.Single(result.Failure!.FailureReasons).Message);
    }

    [Fact]
    public async Task EnvironmentName_SelectsTheKeyedEnvironment()
    {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddKeyedCel("access", environment => environment.AddFunction("level", (string role) => role == "manager" ? 2L : 1L));
        services.AddCelAuthorization("access");
        services.AddAuthorizationCore(options => options.AddPolicy(Policy, policy => policy.RequireCel("level(user.role[0]) == 2")));
        using ServiceProvider provider = services.BuildServiceProvider();

        AuthorizationResult result = await Authorize(provider, null).ConfigureAwait(true);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Requirement_TellsItsExpressionAndResourceType()
    {
        AuthorizationPolicy policy = new AuthorizationPolicyBuilder().RequireCel("true").RequireCel<Address>("false").Build();

        CelAuthorizationRequirement[] requirements = [.. policy.Requirements.OfType<CelAuthorizationRequirement>()];

        Assert.Equal("true", requirements[0].Expression);
        Assert.Null(requirements[0].ResourceType);
        Assert.Equal("false", requirements[1].Expression);
        Assert.Equal(typeof(Address), requirements[1].ResourceType);
    }

    [Fact]
    public void AddCelAuthorization_Twice_AddsNoServices()
    {
        ServiceCollection services = new();
        services.AddCelAuthorization();
        int count = services.Count;

        services.AddCelAuthorization("other");

        Assert.Equal(count, services.Count);
    }

    [Fact]
    public void Methods_CheckTheirArguments()
    {
        AuthorizationPolicyBuilder builder = new();

        Assert.Throws<ArgumentNullException>("services", () => CelAuthorizationExtensions.AddCelAuthorization(null!));
        Assert.Throws<ArgumentException>("environmentName", () => new ServiceCollection().AddCelAuthorization(string.Empty));
        Assert.Throws<ArgumentNullException>("builder", () => CelAuthorizationExtensions.RequireCel(null!, "true"));
        Assert.Throws<ArgumentNullException>("expression", () => builder.RequireCel(null!));
        Assert.Throws<ArgumentNullException>("builder", () => CelAuthorizationExtensions.RequireCel<Address>(null!, "true"));
        Assert.Throws<ArgumentNullException>("expression", () => builder.RequireCel<Address>(null!));
    }

    private static ServiceProvider Provider(Action<AuthorizationPolicyBuilder> policy, Action<CelEnvironmentBuilder>? configure = null)
    {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddCel(environment =>
        {
            environment.AddTypes(ShopContext.Default);
            configure?.Invoke(environment);
        });
        services.AddCelAuthorization();
        services.AddAuthorizationCore(options => options.AddPolicy(Policy, policy));
        return services.BuildServiceProvider();
    }

    private static Task<AuthorizationResult> Authorize(ServiceProvider provider, object? resource) =>
        provider.GetRequiredService<IAuthorizationService>().AuthorizeAsync(Manager, resource, Policy);
}
