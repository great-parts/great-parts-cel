using System;
using Microsoft.AspNetCore.Authorization;

namespace Great.Parts.Cel.AspNetCore;

/// <summary>
/// The authorization requirement that <see cref="CelAuthorizationExtensions.RequireCel(AuthorizationPolicyBuilder, string)"/>
/// adds to a policy: a CEL expression that must give <see langword="true"/>, and the type of the resource it reads, if any.
/// </summary>
/// <remarks>
/// The handler registered with <see cref="CelAuthorizationExtensions.AddCelAuthorization"/> evaluates the requirement. An
/// application reads the requirement, for example to list the rules of its policies; it creates requirements with
/// <c>RequireCel</c>. The requirement is immutable and can be shared between threads.
/// </remarks>
public sealed class CelAuthorizationRequirement : IAuthorizationRequirement
{
    internal CelAuthorizationRequirement(string expression, ResourceSlot? resource)
    {
        Expression = expression;
        Resource = resource;
    }

    /// <summary>
    /// The text of the expression, of the type <c>bool</c>, over the variable <c>user</c> and, with a resource type, the
    /// variable <c>resource</c>.
    /// </summary>
    public string Expression { get; }

    /// <summary>
    /// The .NET type of the variable <c>resource</c>, given by
    /// <see cref="CelAuthorizationExtensions.RequireCel{TResource}(AuthorizationPolicyBuilder, string)"/>, or
    /// <see langword="null"/> when the expression reads only the user.
    /// </summary>
    public Type? ResourceType => Resource?.Type;

    // The declaration and the binding of the variable resource, or null when the expression reads only the user.
    internal ResourceSlot? Resource { get; }
}
