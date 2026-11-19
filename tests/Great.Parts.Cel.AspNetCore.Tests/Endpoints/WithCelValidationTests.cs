using System;
using System.IO;
using System.Linq;
using System.Text.Json;
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

// The endpoint filter of minimal API endpoints, run through the request delegate that ASP.NET Core builds for the endpoint (the
// request delegate generator under Native AOT): true runs the handler; false, an error and a handler without the argument answer 400.
[Trait("Requirement", "F-17")]
public sealed class WithCelValidationTests
{
    private const string Message = "The total is not positive";

    [Fact]
    public async Task TrueExpression_RunsTheHandler()
    {
        using WebApplication app = App();
        app.MapPost("/orders", (Order order) => TypedResults.Ok(order.Id)).WithCelValidation<Order>("total > 0.0", Message);

        (int status, JsonElement body) = await Post(app, "/orders", new Order { Id = "o-1", Total = 5 }).ConfigureAwait(true);

        Assert.Equal(StatusCodes.Status200OK, status);
        Assert.Equal("o-1", body.GetString());
    }

    [Fact]
    public async Task FalseExpression_AnswersAValidationProblemWithTheMessage()
    {
        using WebApplication app = App();
        app.MapPost("/orders", (Order order) => TypedResults.Ok(order.Id)).WithCelValidation<Order>("total > 0.0", Message);

        (int status, JsonElement body) = await Post(app, "/orders", new Order { Total = -1 }).ConfigureAwait(true);

        Assert.Equal(StatusCodes.Status400BadRequest, status);
        Assert.Equal(Message, body.GetProperty("errors").GetProperty(string.Empty)[0].GetString());
        Assert.False(body.TryGetProperty("detail", out _));
    }

    [Theory]
    [InlineData("amount > 0.0", "UndeclaredReference")]
    [InlineData("lines[0].qty > 0", "IndexOutOfRange")]
    public async Task CompilationOrEvaluationError_AnswersAValidationProblemWithTheCode(string expression, string code)
    {
        using WebApplication app = App();
        app.MapPost("/orders", (Order order) => TypedResults.Ok(order.Id)).WithCelValidation<Order>(expression, Message);

        (int status, JsonElement body) = await Post(app, "/orders", new Order { Total = 5 }).ConfigureAwait(true);

        Assert.Equal(StatusCodes.Status400BadRequest, status);
        Assert.Equal(Message, body.GetProperty("errors").GetProperty(string.Empty)[0].GetString());
        Assert.Equal($"The validation rule failed with the error {code}.", body.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task HandlerWithoutTheArgument_AnswersAValidationProblem()
    {
        using WebApplication app = App();
        app.MapPost("/ping", () => TypedResults.Ok("pong")).WithCelValidation<Order>("total > 0.0", Message);

        (int status, JsonElement body) = await Post(app, "/ping", new Order { Total = 5 }).ConfigureAwait(true);

        Assert.Equal(StatusCodes.Status400BadRequest, status);
        Assert.Equal("The request has no argument of the type Order.", body.GetProperty("detail").GetString());
    }

    // The call on a group adds the filter to every endpoint of the group.
    [Fact]
    public async Task GroupFilter_ValidatesEveryEndpointOfTheGroup()
    {
        using WebApplication app = App();
        RouteGroupBuilder group = app.MapGroup("/api").WithCelValidation<Order>("total > 0.0", Message);
        group.MapPost("/orders", (Order order) => TypedResults.Ok(order.Id));
        group.MapPut("/orders", (Order order) => TypedResults.Ok(order.Id));

        (int created, _) = await Post(app, "/api/orders", new Order { Total = -1 }, HttpMethods.Post).ConfigureAwait(true);
        (int updated, _) = await Post(app, "/api/orders", new Order { Total = -1 }, HttpMethods.Put).ConfigureAwait(true);
        (int valid, _) = await Post(app, "/api/orders", new Order { Total = 1 }, HttpMethods.Put).ConfigureAwait(true);

        Assert.Equal(StatusCodes.Status400BadRequest, created);
        Assert.Equal(StatusCodes.Status400BadRequest, updated);
        Assert.Equal(StatusCodes.Status200OK, valid);
    }

    [Fact]
    public void WithCelValidation_ChecksItsArguments()
    {
        using WebApplication app = App();
        RouteHandlerBuilder endpoint = app.MapPost("/orders", (Order order) => TypedResults.Ok(order.Id));
        RouteGroupBuilder group = app.MapGroup("/api");

        Assert.Throws<ArgumentNullException>("builder", () => CelEndpointExtensions.WithCelValidation<Order>((RouteHandlerBuilder)null!, "true", Message));
        Assert.Throws<ArgumentNullException>("expression", () => endpoint.WithCelValidation<Order>(null!, Message));
        Assert.Throws<ArgumentNullException>("message", () => endpoint.WithCelValidation<Order>("true", null!));
        Assert.Throws<ArgumentNullException>("builder", () => CelEndpointExtensions.WithCelValidation<Order>((RouteGroupBuilder)null!, "true", Message));
        Assert.Throws<ArgumentNullException>("expression", () => group.WithCelValidation<Order>(null!, Message));
        Assert.Throws<ArgumentNullException>("message", () => group.WithCelValidation<Order>("true", null!));
    }

    private static WebApplication App()
    {
        WebApplicationBuilder builder = WebApplication.CreateSlimBuilder();
        builder.Services.AddCel(environment => environment.AddTypes(ShopContext.Default));
        builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.TypeInfoResolverChain.Insert(0, TestJsonContext.Default));
        return builder.Build();
    }

    // Runs the request delegate of the endpoint of the path and the method on a JSON body; gives the status and the JSON of the answer.
    private static async Task<(int Status, JsonElement Body)> Post(WebApplication app, string path, Order order, string method = "POST")
    {
        RequestDelegate handler = Endpoint(app, path, method);
        using IServiceScope scope = app.Services.CreateScope();
        byte[] json = JsonSerializer.SerializeToUtf8Bytes(order, TestJsonContext.Default.Order);
        using MemoryStream response = new();
        DefaultHttpContext context = new() { RequestServices = scope.ServiceProvider };
        context.Request.Method = method;
        context.Request.Path = path;
        context.Request.ContentType = "application/json";
        context.Request.ContentLength = json.Length;
        context.Request.Body = new MemoryStream(json);
        context.Features.Set<IHttpRequestBodyDetectionFeature>(new BodyDetection());
        context.Response.Body = response;

        await handler(context).ConfigureAwait(true);

        response.Position = 0;
        using JsonDocument document = await JsonDocument.ParseAsync(response, cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(true);
        return (context.Response.StatusCode, document.RootElement.Clone());
    }

    // The request delegate of the endpoint of the path and the method, as ASP.NET Core builds it.
    private static RequestDelegate Endpoint(WebApplication app, string path, string method) =>
        ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Single(candidate => string.Equals(candidate.RoutePattern.RawText, path, StringComparison.Ordinal)
                && candidate.Metadata.GetRequiredMetadata<HttpMethodMetadata>().HttpMethods.Contains(method))
            .RequestDelegate!;

    // A request of a server has this feature; the delegate of an endpoint reads a body only when it says that the request can have one.
    private sealed class BodyDetection : IHttpRequestBodyDetectionFeature
    {
        public bool CanHaveBody => true;
    }
}
