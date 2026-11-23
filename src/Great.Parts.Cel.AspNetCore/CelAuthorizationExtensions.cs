using System;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Great.Parts.Cel.AspNetCore;

/// <summary>
/// Authorization policies with CEL expressions: <c>policy.RequireCel("'admin' in user.role")</c>.
/// </summary>
/// <remarks>
/// <para>
/// An expression of a policy has the type <c>bool</c> and sees the variable <c>user</c> of the type
/// <c>map(string, list(string))</c>: the claim types of the user and their values, so <c>'admin' in user.role</c> or
/// <c>user.department == ['sales']</c>. With <see cref="RequireCel{TResource}(AuthorizationPolicyBuilder, string)"/> it also
/// sees <c>resource</c>, the resource of the authorization, of the type that a type source of the CEL environment describes.
/// </para>
/// <para>
/// The handler derives its environments from the one registered with the dependency injection package: one for the user and
/// one per resource type, each with a compilation cache, so each expression is compiled once. <see langword="true"/> allows access.
/// An error in an expression (compilation, evaluation, a false value) denies access with a reason that has the code of the
/// error and throws no exception. A CEL environment that is not registered is an error of the configuration: the first
/// authorization throws the <see cref="InvalidOperationException"/> of the service provider and access is not given.
/// </para>
/// </remarks>
public static class CelAuthorizationExtensions
{
    /// <summary>
    /// Registers the authorization handler of CEL requirements, which evaluates them in a CEL environment registered with
    /// <see cref="DependencyInjection.CelServiceCollectionExtensions.AddCel"/> or, by name, with
    /// <see cref="DependencyInjection.CelServiceCollectionExtensions.AddKeyedCel"/>.
    /// </summary>
    /// <remarks>
    /// The environment must be registered too; the handler declares the variables <c>user</c> and <c>resource</c> in an
    /// environment derived from it, so the registered environment must not declare variables with these names. A second call
    /// adds no services. If the environment is not registered, the first authorization throws the
    /// <see cref="InvalidOperationException"/> of the service provider and access is not given; the check does not run at start.
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <param name="environmentName">
    /// The name of the environment registered with <see cref="DependencyInjection.CelServiceCollectionExtensions.AddKeyedCel"/>,
    /// or <see langword="null"/> for the one registered with
    /// <see cref="DependencyInjection.CelServiceCollectionExtensions.AddCel"/>.
    /// </param>
    /// <returns><paramref name="services"/>, for chained calls.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="environmentName"/> is empty.</exception>
    public static IServiceCollection AddCelAuthorization(this IServiceCollection services, string? environmentName = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        if (environmentName is { Length: 0 })
        {
            throw new ArgumentException("The name of the environment is empty; pass null for the environment registered with AddCel.", nameof(environmentName));
        }

        // TryAddEnumerable compares the implementation type, so a second call adds no handler.
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IAuthorizationHandler, CelAuthorizationHandler>(
            provider => new CelAuthorizationHandler(provider, environmentName)));
        return services;
    }

    /// <summary>
    /// Adds to the policy a requirement that a CEL expression over the claims of the user gives <see langword="true"/>.
    /// </summary>
    /// <remarks>
    /// The expression is compiled when the policy is first evaluated, not here: a compilation error denies access with its
    /// code as the reason. Validate the expressions of the policies at startup with
    /// <see cref="DependencyInjection.ICelEvaluator.Validate{T}(string, CelCompileOptions)"/> when they come from
    /// configuration.
    /// </remarks>
    /// <param name="builder">The policy builder.</param>
    /// <param name="expression">The expression of the type <c>bool</c> over the variable <c>user</c>.</param>
    /// <returns><paramref name="builder"/>, for chained calls.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> or <paramref name="expression"/> is <see langword="null"/>.</exception>
    public static AuthorizationPolicyBuilder RequireCel(this AuthorizationPolicyBuilder builder, string expression)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(expression);
        return builder.AddRequirements(new CelAuthorizationRequirement(expression, null));
    }

    /// <summary>
    /// Adds to the policy a requirement that a CEL expression over the claims of the user and the resource of the
    /// authorization gives <see langword="true"/>, for resource-based authorization with
    /// <c>IAuthorizationService.AuthorizeAsync(user, resource, policy)</c>.
    /// </summary>
    /// <remarks>
    /// The variable <c>resource</c> has the CEL type that a type source of the environment gives
    /// <typeparamref name="TResource"/>; a resource of another type, or no resource, denies access. The expression is compiled
    /// when the policy is first evaluated, as for <see cref="RequireCel(AuthorizationPolicyBuilder, string)"/>.
    /// </remarks>
    /// <typeparam name="TResource">The .NET type of the resource, described by a type source of the CEL environment.</typeparam>
    /// <param name="builder">The policy builder.</param>
    /// <param name="expression">The expression of the type <c>bool</c> over the variables <c>user</c> and <c>resource</c>.</param>
    /// <returns><paramref name="builder"/>, for chained calls.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> or <paramref name="expression"/> is <see langword="null"/>.</exception>
    public static AuthorizationPolicyBuilder RequireCel<TResource>(this AuthorizationPolicyBuilder builder, string expression)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(expression);
        return builder.AddRequirements(new CelAuthorizationRequirement(expression, ResourceSlot<TResource>.Instance));
    }
}
