using System;
using System.Threading.Tasks;
using Great.Parts.Cel.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Great.Parts.Cel.AspNetCore;

/// <summary>
/// Validation of requests of minimal API endpoints with CEL expressions:
/// <c>app.MapPost("/orders", Create).WithCelValidation&lt;CreateOrder&gt;("items.size() &gt; 0", "The order has no items")</c>.
/// </summary>
/// <remarks>
/// <para>
/// The method adds an endpoint filter. Before the handler runs, the filter takes the argument of the handler of the type
/// <c>TRequest</c> and evaluates the expression with it as the input of an environment derived from the
/// registered CEL environment, as <c>environment.ToBuilder().UseInput&lt;TRequest&gt;()</c> does, so the fields of the
/// request are the variables of the expression and a type source of the environment must describe
/// <c>TRequest</c>. The derived environment and the compiled expression are created once.
/// </para>
/// <para>
/// <see langword="true"/> runs the handler. <see langword="false"/> returns the response 400 with a validation problem whose
/// message is the given one. A compilation or an evaluation error also returns 400, with the code of the error; a handler
/// without an argument of the type <c>TRequest</c> returns 400 as well. The filter works with the request
/// delegate generator of ASP.NET Core under Native AOT.
/// </para>
/// <para>
/// The evaluation is asynchronous, so the expression may call an asynchronous function of the application. It receives
/// <see cref="HttpContext.RequestAborted"/>: when the client disconnects, no new call starts, the calls in flight receive
/// the canceled token, and the request ends with <see cref="OperationCanceledException"/>.
/// </para>
/// </remarks>
public static class CelEndpointExtensions
{
    /// <summary>
    /// Adds to the endpoint a filter that validates the argument of the type <typeparamref name="TRequest"/> by a CEL
    /// expression, which must give <see langword="true"/>.
    /// </summary>
    /// <typeparam name="TRequest">The .NET type of the request argument, described by a type source of the CEL environment.</typeparam>
    /// <param name="builder">The builder of the endpoint.</param>
    /// <param name="expression">The expression of the type <c>bool</c> over the fields of <typeparamref name="TRequest"/>.</param>
    /// <param name="message">The message of the validation problem when the expression gives <see langword="false"/>.</param>
    /// <returns><paramref name="builder"/>, for chained calls.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="builder"/>, <paramref name="expression"/> or <paramref name="message"/> is <see langword="null"/>.
    /// </exception>
    public static RouteHandlerBuilder WithCelValidation<TRequest>(this RouteHandlerBuilder builder, string expression, string message)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(expression);
        ArgumentNullException.ThrowIfNull(message);
        return builder.AddEndpointFilterFactory(FilterFactory<TRequest>(expression, message));
    }

    /// <summary>
    /// Adds to every endpoint of the group a filter that validates the argument of the type <typeparamref name="TRequest"/>
    /// by a CEL expression, as <see cref="WithCelValidation{TRequest}(RouteHandlerBuilder, string, string)"/> does for one
    /// endpoint.
    /// </summary>
    /// <typeparam name="TRequest">The .NET type of the request argument, described by a type source of the CEL environment.</typeparam>
    /// <param name="builder">The builder of the group of endpoints.</param>
    /// <param name="expression">The expression of the type <c>bool</c> over the fields of <typeparamref name="TRequest"/>.</param>
    /// <param name="message">The message of the validation problem when the expression gives <see langword="false"/>.</param>
    /// <returns><paramref name="builder"/>, for chained calls.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="builder"/>, <paramref name="expression"/> or <paramref name="message"/> is <see langword="null"/>.
    /// </exception>
    public static RouteGroupBuilder WithCelValidation<TRequest>(this RouteGroupBuilder builder, string expression, string message)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(expression);
        ArgumentNullException.ThrowIfNull(message);
        return builder.AddEndpointFilterFactory(FilterFactory<TRequest>(expression, message));
    }

    // The factory runs once for each endpoint when its request delegate is built. The rule is made at the first request of the
    // endpoint, from the environment registered with AddCel, and then serves every request.
    private static Func<EndpointFilterFactoryContext, EndpointFilterDelegate, EndpointFilterDelegate> FilterFactory<TRequest>(string expression, string message) =>
        (factoryContext, next) =>
        {
            IServiceProvider services = factoryContext.ApplicationServices;
            Lazy<RequestRule<TRequest>> rule = new(() => new RequestRule<TRequest>(services.GetRequiredService<CelEnvironment>(), expression));
            return invocationContext => Validate(invocationContext, next, rule.Value, message);
        };

    private static async ValueTask<object?> Validate<TRequest>(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next,
        RequestRule<TRequest> rule,
        string message)
    {
        bool found = RequestRule<TRequest>.TryFind(context.Arguments, out TRequest? request);
        CelError? error = null;
        if (found)
        {
            bool passed;
            (passed, error) = await rule.PassesAsync(request!, context.HttpContext.RequestAborted).ConfigureAwait(false);
            if (passed)
            {
                return await next(context).ConfigureAwait(false);
            }
        }

        return TypedResults.ValidationProblem(RequestRule<TRequest>.Errors(message), RequestRule<TRequest>.Detail(found, error));
    }
}
