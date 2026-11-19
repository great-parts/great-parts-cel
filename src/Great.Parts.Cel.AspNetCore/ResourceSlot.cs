using System;

namespace Great.Parts.Cel.AspNetCore;

// The variable resource of an authorization requirement: its .NET type, its declaration in the derived environment and its binding.
// The type is a type argument, so the declaration and the binding need no reflection. One instance per resource type.
internal abstract class ResourceSlot
{
    // The name of the variable of the resource.
    internal const string VariableName = "resource";

    internal abstract Type Type { get; }

    internal abstract void Declare(CelEnvironmentBuilder builder);

    // Binds the resource of the authorization; false when it is not an instance of the type, which denies access.
    internal abstract bool TryBind(object? resource, CelBindings bindings);
}

internal sealed class ResourceSlot<TResource> : ResourceSlot
{
    internal static readonly ResourceSlot<TResource> Instance = new();

    private ResourceSlot()
    {
    }

    internal override Type Type => typeof(TResource);

    internal override void Declare(CelEnvironmentBuilder builder) => builder.AddVariable<TResource>(VariableName);

    internal override bool TryBind(object? resource, CelBindings bindings)
    {
        if (resource is not TResource typed)
        {
            return false;
        }

        bindings.Add(VariableName, typed);
        return true;
    }
}
