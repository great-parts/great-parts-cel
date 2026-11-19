using System;
using System.Collections.Generic;
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

// The MVC action filter, called as MVC calls it before an action: true runs the action; false, an error and an action without the
// argument end the request with 400 and a validation problem.
[Trait("Requirement", "F-17")]
public sealed class CelValidateAttributeTests
{
    private const string Message = "The total is not positive";

    [Fact]
    public async Task TrueExpression_RunsTheAction()
    {
        CelValidateAttribute<Order> attribute = new("total > 0.0", Message);

        (bool ran, ActionExecutingContext context) = await Run(attribute, new Order { Total = 5 }).ConfigureAwait(true);

        Assert.True(ran);
        Assert.Null(context.Result);
    }

    [Theory]
    [InlineData("total > 0.0", null)]
    [InlineData("amount > 0.0", "The validation rule failed with the error UndeclaredReference.")]
    public async Task FalseOrError_EndsTheRequestWith400(string expression, string? detail)
    {
        CelValidateAttribute<Order> attribute = new(expression, Message);

        (bool ran, ActionExecutingContext context) = await Run(attribute, new Order { Total = -1 }).ConfigureAwait(true);

        Assert.False(ran);
        ValidationProblemDetails problem = Problem(context);
        Assert.Equal(Message, Assert.Single(problem.Errors[string.Empty]));
        Assert.Equal(detail, problem.Detail);
    }

    [Fact]
    public async Task ActionWithoutTheArgument_EndsTheRequestWith400()
    {
        CelValidateAttribute<Order> attribute = new("total > 0.0", Message);

        (bool ran, ActionExecutingContext context) = await Run(attribute, "not an order").ConfigureAwait(true);

        Assert.False(ran);
        Assert.Equal("The request has no argument of the type Order.", Problem(context).Detail);
    }

    // One attribute serves every request: the rule made at the first one is used for the next.
    [Fact]
    public async Task OneAttribute_ServesEveryRequest()
    {
        CelValidateAttribute<Order> attribute = new("total > 0.0", Message);

        (bool first, _) = await Run(attribute, new Order { Total = 5 }).ConfigureAwait(true);
        (bool second, _) = await Run(attribute, new Order { Total = -5 }).ConfigureAwait(true);

        Assert.True(first);
        Assert.False(second);
        Assert.Equal("total > 0.0", attribute.Expression);
        Assert.Equal(Message, attribute.Message);
    }

    [Fact]
    public async Task Methods_CheckTheirArguments()
    {
        CelValidateAttribute<Order> attribute = new("true", Message);

        Assert.Throws<ArgumentNullException>("expression", () => new CelValidateAttribute<Order>(null!, Message));
        Assert.Throws<ArgumentNullException>("message", () => new CelValidateAttribute<Order>("true", null!));
        await Assert.ThrowsAsync<ArgumentNullException>("context", () => attribute.OnActionExecutionAsync(null!, () => null!)).ConfigureAwait(true);
        await Assert.ThrowsAsync<ArgumentNullException>("next", () => attribute.OnActionExecutionAsync(Context("order"), null!)).ConfigureAwait(true);
    }

    private static ActionExecutingContext Context(object argument) =>
        new(new ActionContext(new DefaultHttpContext(), new RouteData(), new ActionDescriptor()), [], new Dictionary<string, object?> { ["order"] = argument }, new object());

    private static ValidationProblemDetails Problem(ActionExecutingContext context)
    {
        BadRequestObjectResult result = Assert.IsType<BadRequestObjectResult>(context.Result);
        ValidationProblemDetails problem = Assert.IsType<ValidationProblemDetails>(result.Value);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.Status);
        return problem;
    }

    // Calls the filter with one action argument; tells whether the rest of the pipeline ran.
    private static async Task<(bool Ran, ActionExecutingContext Context)> Run(CelValidateAttribute<Order> attribute, object argument)
    {
        using ServiceProvider services = new ServiceCollection().AddCel(environment => environment.AddTypes(ShopContext.Default)).BuildServiceProvider();
        ActionContext action = new(new DefaultHttpContext { RequestServices = services }, new RouteData(), new ActionDescriptor());
        ActionExecutingContext context = new(action, [], new Dictionary<string, object?> { ["order"] = argument }, new object());
        bool ran = false;

        await attribute.OnActionExecutionAsync(context, () =>
        {
            ran = true;
            return Task.FromResult(new ActionExecutedContext(action, [], new object()));
        }).ConfigureAwait(true);

        return (ran, context);
    }
}
