using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Great.Parts.Cel.DependencyInjection;
using Great.Parts.Cel.Generator.Samples.Orders;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Great.Parts.Cel.AspNetCore.Tests.Authorization;

// The authorization handler with a function of the application that waits: it evaluates asynchronously, so the expression may call it;
// the validation of options is synchronous and cannot.
[Trait("Requirement", "F-17")]
[Trait("Requirement", "F-23")]
public sealed class AsyncPolicyTests
{
    private const string Policy = "rule";

    private static readonly ClaimsPrincipal Manager = new(new ClaimsIdentity([new Claim("role", "manager")], "test"));

    [Theory]
    [InlineData("isMember(user.role[0], 'managers')", true)]
    [InlineData("isMember(user.role[0], 'owners')", false)]
    public async Task Policy_WithAnAsynchronousFunction_DecidesByItsResult(string expression, bool allowed)
    {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddCel(AsyncAdapterSupport.Configure);
        services.AddCelAuthorization();
        services.AddAuthorizationCore(options => options.AddPolicy(Policy, policy => policy.RequireCel(expression)));
        using ServiceProvider provider = services.BuildServiceProvider();

        AuthorizationResult result = await provider.GetRequiredService<IAuthorizationService>().AuthorizeAsync(Manager, null, Policy).ConfigureAwait(true);

        Assert.Equal(allowed, result.Succeeded);
    }

    // The resource of an endpoint is its HttpContext: the abort of the request cancels the function that is waiting, and the function got
    // the token of the request.
    [Fact]
    public async Task Policy_OnAnEndpoint_ReceivesTheTokenOfTheRequest()
    {
        var probe = new TokenProbe();
        using CancellationTokenSource aborted = new();
        using ServiceProvider provider = Provider(probe, "hang(user.role[0])");
        DefaultHttpContext request = new() { RequestAborted = aborted.Token };

        Task<AuthorizationResult> running = provider.GetRequiredService<IAuthorizationService>().AuthorizeAsync(Manager, request, Policy);
        CancellationToken received = await probe.Received.ConfigureAwait(true);
        await aborted.CancelAsync().ConfigureAwait(true);

        Assert.Equal(aborted.Token, received);
        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => running).ConfigureAwait(true);
    }

    // A resource of the application is no request: IAuthorizationService has no token for it, so the function receives one that
    // cannot be canceled.
    [Fact]
    public async Task Policy_WithAResourceOfTheApplication_ReceivesATokenThatCannotBeCanceled()
    {
        var probe = new TokenProbe();
        ServiceCollection services = new();
        services.AddLogging();
        services.AddCel(probe.Configure);
        services.AddCelAuthorization();
        services.AddAuthorizationCore(options => options.AddPolicy(Policy, policy => policy.RequireCel<Order>("capture(resource.id)")));
        using ServiceProvider provider = services.BuildServiceProvider();

        AuthorizationResult result = await provider.GetRequiredService<IAuthorizationService>().AuthorizeAsync(Manager, new Order { Id = "o-1" }, Policy).ConfigureAwait(true);

        Assert.True(result.Succeeded);
        CancellationToken received = await probe.Received.ConfigureAwait(true);
        Assert.False(received.CanBeCanceled);
    }

    private static ServiceProvider Provider(TokenProbe probe, string expression)
    {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddCel(probe.Configure);
        services.AddCelAuthorization();
        services.AddAuthorizationCore(options => options.AddPolicy(Policy, policy => policy.RequireCel(expression)));
        return services.BuildServiceProvider();
    }

    // IValidateOptions is synchronous by contract: the expression that calls an asynchronous function fails the check with its code.
    [Fact]
    public void OptionsValidation_WithAnAsynchronousFunction_FailsWithAsyncEvaluationRequired()
    {
        using ServiceProvider provider = new ServiceCollection()
            .AddCel(AsyncAdapterSupport.Configure)
            .AddOptions<Address>()
            .Configure(address => address.City = "Paris")
            .ValidateCel("check(city)", "The city is not Paris")
            .Services
            .BuildServiceProvider();

        OptionsValidationException exception = Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<Address>>().Value);

        Assert.StartsWith("The city is not Paris (AsyncEvaluationRequired: ", Assert.Single(exception.Failures), StringComparison.Ordinal);
    }
}
