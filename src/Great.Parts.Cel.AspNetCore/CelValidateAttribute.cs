using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Great.Parts.Cel.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;

namespace Great.Parts.Cel.AspNetCore;

/// <summary>
/// Validates the action argument of the type <typeparamref name="TRequest"/> of an MVC controller by a CEL expression:
/// <c>[CelValidate&lt;CreateOrder&gt;("items.size() &gt; 0", "The order has no items")]</c>.
/// </summary>
/// <remarks>
/// <para>
/// The filter runs before the action. It evaluates the expression with the argument of the type
/// <typeparamref name="TRequest"/> as the input of an environment derived from the CEL environment registered with the
/// dependency injection package, as <c>environment.ToBuilder().UseInput&lt;TRequest&gt;()</c> does, so a type source of the
/// environment must describe <typeparamref name="TRequest"/>. The type is a type argument, not a <see cref="Type"/> read at
/// run time, so the filter needs no reflection. The derived environment and the compiled expression are created once.
/// </para>
/// <para>
/// <see langword="true"/> runs the action. <see langword="false"/> ends the request with the response 400 and a validation
/// problem with the message; a compilation or an evaluation error ends it with 400 and the code of the error, and so does an
/// action without an argument of the type <typeparamref name="TRequest"/>. Minimal API endpoints use
/// <see cref="CelEndpointExtensions.WithCelValidation{TRequest}(Microsoft.AspNetCore.Builder.RouteHandlerBuilder, string, string)"/>.
/// </para>
/// <para>
/// The expression and the message do not change after the attribute is created, and the rule made from them at the first
/// request is kept; one instance serves requests on any number of threads at once.
/// </para>
/// </remarks>
/// <typeparam name="TRequest">The .NET type of the action argument, described by a type source of the CEL environment.</typeparam>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class CelValidateAttribute<TRequest> : Attribute, IAsyncActionFilter
{
    // The rule, made at the first request from the environment registered with AddCel and then kept; two first requests at once
    // may both make it, and one of the two is kept.
    private RequestRule<TRequest>? _rule;

    /// <summary>
    /// Creates the attribute with the expression and the message of its validation.
    /// </summary>
    /// <param name="expression">The expression of the type <c>bool</c> over the fields of <typeparamref name="TRequest"/>.</param>
    /// <param name="message">The message of the validation problem when the expression gives <see langword="false"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="expression"/> or <paramref name="message"/> is <see langword="null"/>.</exception>
    public CelValidateAttribute(string expression, string message)
    {
        ArgumentNullException.ThrowIfNull(expression);
        ArgumentNullException.ThrowIfNull(message);
        Expression = expression;
        Message = message;
    }

    /// <summary>
    /// The expression of the type <c>bool</c> over the fields of <typeparamref name="TRequest"/>.
    /// </summary>
    public string Expression { get; }

    /// <summary>
    /// The message of the validation problem when the expression gives <see langword="false"/>.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Validates the argument before the action and either calls <paramref name="next"/> or sets the result 400 of the
    /// request; called by MVC.
    /// </summary>
    /// <param name="context">The context of the action.</param>
    /// <param name="next">The rest of the pipeline, which runs the action.</param>
    /// <returns>The task of the filter.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> or <paramref name="next"/> is <see langword="null"/>.</exception>
    [SuppressMessage(
        "ApiDesign",
        "RS0030:Do not use banned APIs",
        Justification = "The contract of IAsyncActionFilter returns a Task; the filter evaluates synchronously and creates no thread.")]
    public Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        RequestRule<TRequest>? rule = Volatile.Read(ref _rule);
        if (rule is null)
        {
            rule = new RequestRule<TRequest>(context.HttpContext.RequestServices.GetRequiredService<CelEnvironment>(), Expression);
            rule = Interlocked.CompareExchange(ref _rule, rule, null) ?? rule;
        }

        bool found = RequestRule<TRequest>.TryFind(context.ActionArguments.Values, out TRequest? request);
        CelError? error = null;
        if (found && rule.Passes(request!, out error))
        {
            return next();
        }

        context.Result = new BadRequestObjectResult(new ValidationProblemDetails(RequestRule<TRequest>.Errors(Message))
        {
            Status = StatusCodes.Status400BadRequest,
            Detail = RequestRule<TRequest>.Detail(found, error),
        });
        return Task.CompletedTask;
    }
}
