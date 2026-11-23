using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Great.Parts.Cel.DependencyInjection;
using Great.Parts.Cel.Generator.Samples.Orders;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Great.Parts.Cel.AspNetCore.Tests.Endpoints;

// The filter of minimal API endpoints with a function of the application that waits: the filter is asynchronous, so the expression of
// the rule may call it, and the answer is the same as for a synchronous rule.
[Trait("Requirement", "F-17")]
[Trait("Requirement", "F-23")]
public sealed class AsyncEndpointFilterTests
{
    [Theory]
    [InlineData(5, 200)]
    [InlineData(-1, 400)]
    public async Task EndpointFilter_WithAnAsynchronousFunction_AnswersByItsResult(double total, int status)
    {
        WebApplicationBuilder builder = WebApplication.CreateSlimBuilder();
        builder.Services.AddCel(AsyncAdapterSupport.Configure);
        builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.TypeInfoResolverChain.Insert(0, TestJsonContext.Default));
        using WebApplication app = builder.Build();
        app.MapPost("/orders", (Order order) => TypedResults.Ok(order.Id)).WithCelValidation<Order>("positive(total)", "The total is not positive");
        using IServiceScope scope = app.Services.CreateScope();
        DefaultHttpContext context = Request(scope, total, CancellationToken.None);

        await Handler(app)(context).ConfigureAwait(true);

        Assert.Equal(status, context.Response.StatusCode);
    }

    // The function that is waiting got the token of the request, and the abort of the request cancels it: the filter ends with the
    // cancellation, not with a validation problem.
    [Fact]
    public async Task EndpointFilter_ReceivesTheTokenOfTheRequest()
    {
        var probe = new TokenProbe();
        WebApplicationBuilder builder = WebApplication.CreateSlimBuilder();
        builder.Services.AddCel(probe.Configure);
        builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.TypeInfoResolverChain.Insert(0, TestJsonContext.Default));
        using WebApplication app = builder.Build();
        app.MapPost("/orders", (Order order) => TypedResults.Ok(order.Id)).WithCelValidation<Order>("hang(id)", "The order is not accepted");
        using IServiceScope scope = app.Services.CreateScope();
        using CancellationTokenSource aborted = new();
        DefaultHttpContext context = Request(scope, 5, aborted.Token);

        Task running = Handler(app)(context);
        CancellationToken received = await probe.Received.ConfigureAwait(true);
        await aborted.CancelAsync().ConfigureAwait(true);

        Assert.Equal(aborted.Token, received);
        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => running).ConfigureAwait(true);
    }

    // The request delegate of the only endpoint, as ASP.NET Core builds it.
    private static RequestDelegate Handler(WebApplication app) =>
        ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Single()
            .RequestDelegate!;

    // A request with an order of the total as its JSON body.
    private static DefaultHttpContext Request(IServiceScope scope, double total, CancellationToken aborted)
    {
        byte[] json = JsonSerializer.SerializeToUtf8Bytes(new Order { Id = "o-1", Total = total }, TestJsonContext.Default.Order);
        DefaultHttpContext context = new() { RequestServices = scope.ServiceProvider, RequestAborted = aborted };
        context.Request.Method = HttpMethods.Post;
        context.Request.Path = "/orders";
        context.Request.ContentType = "application/json";
        context.Request.ContentLength = json.Length;
        context.Request.Body = new MemoryStream(json);
        context.Features.Set<IHttpRequestBodyDetectionFeature>(new BodyDetection());
        context.Response.Body = new MemoryStream();
        return context;
    }

    // A request of a server has this feature; the delegate of an endpoint reads a body only when it says that the request can have one.
    private sealed class BodyDetection : IHttpRequestBodyDetectionFeature
    {
        public bool CanHaveBody => true;
    }
}
