using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace Great.Parts.Cel.AspNetCore;

// The handler of CEL requirements. It derives from the registered environment one environment with the variable user, and one more
// for each resource type with the variable resource as well, each with its own compilation cache, so an expression is compiled once.
// true succeeds the requirement; false, a build, compilation or evaluation error and a resource of another type fail it with a reason.
internal sealed class CelAuthorizationHandler : AuthorizationHandler<CelAuthorizationRequirement>
{
    private const string UserVariable = "user";

    private static readonly CelType UserType = CelType.MapOf(CelType.String, CelType.ListOf(CelType.String));

    private readonly Lazy<CelEnvironment> _environment;
    private readonly Lazy<Scope> _userScope;
    private readonly ConcurrentDictionary<Type, Lazy<Scope>> _resourceScopes = new();

    internal CelAuthorizationHandler(IServiceProvider services, string? environmentName)
    {
        _environment = new Lazy<CelEnvironment>(() => environmentName is null
            ? services.GetRequiredService<CelEnvironment>()
            : services.GetRequiredKeyedService<CelEnvironment>(environmentName));
        _userScope = new Lazy<Scope>(() => Scope.Create(_environment.Value, null));
    }

    [SuppressMessage(
        "ApiDesign",
        "RS0030:Do not use banned APIs",
        Justification = "The contract of AuthorizationHandler returns a Task; the handler evaluates synchronously and completes it at once, creating no thread.")]
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, CelAuthorizationRequirement requirement)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(requirement);

        string? denial = Check(context, requirement);
        if (denial is null)
        {
            context.Succeed(requirement);
        }
        else
        {
            context.Fail(new AuthorizationFailureReason(this, denial));
        }

        return Task.CompletedTask;
    }

    // Null when the expression gives true; otherwise the reason of the denial.
    private string? Check(AuthorizationHandlerContext context, CelAuthorizationRequirement requirement)
    {
        ResourceSlot? slot = requirement.Resource;
        Scope scope = slot is null
            ? _userScope.Value
            : _resourceScopes.GetOrAdd(slot.Type, static (_, state) => new Lazy<Scope>(() => Scope.Create(state.Environment.Value, state.Slot)), (Environment: _environment, Slot: slot)).Value;
        if (scope.Cache is null)
        {
            return Reason(scope.Error!);
        }

        CelBindings bindings = new CelBindings().Add(UserVariable, UserMap(context.User));
        if (slot is not null && !slot.TryBind(context.Resource, bindings))
        {
            return string.Create(CultureInfo.InvariantCulture, $"The resource of the authorization is not an instance of {slot.Type.Name}.");
        }

        CelEvalResult<bool> result = scope.Cache.Evaluate<bool>(requirement.Expression, bindings, cancellationToken: CancellationToken.None);
        if (!result.IsSuccess)
        {
            return Reason(result.Errors[0]);
        }

        return result.Value ? null : "The CEL expression of the requirement gave false.";
    }

    private static string Reason(CelError error) =>
        string.Create(CultureInfo.InvariantCulture, $"The CEL expression of the requirement failed with the error {error.Code}.");

    // The claims of the user as map(string, list(string)): the claim type to the values of the claims of that type, in their order.
    private static CelMap UserMap(ClaimsPrincipal user)
    {
        Dictionary<string, List<string>> claims = new(StringComparer.Ordinal);
        foreach (Claim claim in user.Claims)
        {
            ref List<string>? values = ref CollectionsMarshal.GetValueRefOrAddDefault(claims, claim.Type, out _);
            values ??= [];
            values.Add(claim.Value);
        }

        return CelMap.CreateView(claims, static values => CelValue.FromList(CelList.CreateView(values, static value => CelValue.FromString(value))), keepSourceOrder: true);
    }

    // The derived environment of the user and, with a slot, of the resource, with its cache; or the error of building it.
    private sealed class Scope
    {
        private Scope(CelCompilationCache? cache, CelError? error)
        {
            Cache = cache;
            Error = error;
        }

        internal CelCompilationCache? Cache { get; }

        internal CelError? Error { get; }

        internal static Scope Create(CelEnvironment environment, ResourceSlot? slot)
        {
            CelEnvironmentBuilder builder = environment.ToBuilder().AddVariable(UserVariable, UserType);
            slot?.Declare(builder);
            CelBuildResult build = builder.Build();
            return build.IsSuccess ? new Scope(new CelCompilationCache(build.GetEnvironmentOrThrow()), null) : new Scope(null, build.Errors[0]);
        }
    }
}
