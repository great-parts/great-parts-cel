using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Great.Parts.Cel.Declarations;

// The definition that CelExtension.Create builds: declarations of the application. Its overloads are overloads of the
// application with the implementations that were given for them, so they get what an application function gets — the
// exception boundary, the check of the arguments and the result against the declared types and the cost function. It has
// no macros, abstract types or variables. The conflicts and the other build errors are reported by Build, which sees the
// whole environment; the set itself keeps what Build needs to report a missing or an unused implementation.
internal sealed class ApplicationExtension : ExtensionDefinition
{
    private ApplicationExtension(
        string name,
        uint version,
        ImmutableArray<OverloadBinding> overloads,
        ImmutableArray<string> unusedImplementations,
        ImmutableArray<string> dependencies)
        : base(name, version, DeclarationChecks.ApplicationSource, overloads, dependencies: dependencies)
    {
        UnusedImplementations = unusedImplementations;
    }

    // The identifiers of the implementations that no declared overload has, in ordinal order.
    internal ImmutableArray<string> UnusedImplementations { get; }

    // Copies the declarations, the implementations and the names, so a later change of the arguments cannot change the set.
    internal static ApplicationExtension Create(
        string name,
        uint version,
        ReadOnlySpan<CelFunctionDecl> functions,
        IReadOnlyDictionary<string, CelFunctionImplementation> implementations,
        ReadOnlySpan<string> dependencies)
    {
        ImmutableArray<OverloadBinding>.Builder overloads = ImmutableArray.CreateBuilder<OverloadBinding>();
        HashSet<string> declared = new(StringComparer.Ordinal);
        foreach (CelFunctionDecl function in functions)
        {
            foreach (CelOverloadDecl overload in function.Overloads)
            {
                declared.Add(overload.OverloadId);
                OverloadImplementation? implementation = implementations.TryGetValue(overload.OverloadId, out CelFunctionImplementation? given)
                    ? TypedImplementation.Of(given)
                    : null;
                overloads.Add(new OverloadBinding(function.Name, overload, implementation, 1, overload.Cost, DeclarationChecks.ApplicationSource, isApplication: true));
            }
        }

        List<string> unused = [];
        foreach (string id in implementations.Keys)
        {
            if (!declared.Contains(id))
            {
                unused.Add(id);
            }
        }

        unused.Sort(StringComparer.Ordinal);
        return new ApplicationExtension(name, version, overloads.ToImmutable(), [.. unused], [.. dependencies]);
    }
}
