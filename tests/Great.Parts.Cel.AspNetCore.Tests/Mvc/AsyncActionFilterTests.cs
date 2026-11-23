using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Great.Parts.Cel.DependencyInjection;
using Great.Parts.Cel.Generator.Samples.Orders;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Great.Parts.Cel.AspNetCore.Tests.Mvc;

// The MVC filter with a function of the application that waits: the filter is asynchronous, so the expression may call it.
[Trait("Requirement", "F-17")]
[Trait("Requirement", "F-23")]
public sealed class AsyncActionFilterTests
{
    [Theory]
    [InlineData(5, true)]
    [InlineData(-1, false)]
    public async Task MvcFilter_WithAnAsynchronousFunction_RunsTheActionByItsResult(double total, bool ran)
    {
        CelValidateAttribute<Order> attribute = new("positive(total)", "The total is not positive");
        using ServiceProvider services = new ServiceCollection().AddCel(AsyncAdapterSupport.Configure).BuildServiceProvider();
        ActionContext action = new(new DefaultHttpContext { RequestServices = services }, new RouteData(), new ActionDescriptor());
        ActionExecutingContext context = new(action, [], new Dictionary<string, object?> { ["order"] = new Order { Total = total } }, new object());
        bool ranAction = false;

        await attribute.OnActionExecutionAsync(context, () =>
        {
            ranAction = true;
            return Task.FromResult(new ActionExecutedContext(action, [], new object()));
        }).ConfigureAwait(true);

        Assert.Equal(ran, ranAction);
        Assert.Equal(!ran, context.Result is BadRequestObjectResult);
    }

    // The function that is waiting got the token of the request, and the abort of the request cancels it: the filter ends with the
    // cancellation and the action does not run.
    [Fact]
    public async Task MvcFilter_ReceivesTheTokenOfTheRequest()
    {
        var probe = new TokenProbe();
        CelValidateAttribute<Order> attribute = new("hang(id)", "The order is not accepted");
        using ServiceProvider services = new ServiceCollection().AddCel(probe.Configure).BuildServiceProvider();
        using CancellationTokenSource aborted = new();
        ActionContext action = new(new DefaultHttpContext { RequestServices = services, RequestAborted = aborted.Token }, new RouteData(), new ActionDescriptor());
        ActionExecutingContext context = new(action, [], new Dictionary<string, object?> { ["order"] = new Order { Id = "o-1" } }, new object());
        bool ranAction = false;

        Task running = attribute.OnActionExecutionAsync(context, () =>
        {
            ranAction = true;
            return Task.FromResult(new ActionExecutedContext(action, [], new object()));
        });
        CancellationToken received = await probe.Received.ConfigureAwait(true);
        await aborted.CancelAsync().ConfigureAwait(true);

        Assert.Equal(aborted.Token, received);
        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => running).ConfigureAwait(true);
        Assert.False(ranAction);
    }
}
