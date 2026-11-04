using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Runtime.InteropServices;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Types;

namespace Great.Parts.Cel.Declarations;

// The checks of Build over the whole set of declarations against a version of the standard library. Every
// error found is reported, ordered by code and then by message, so the order of the builder calls changes
// neither the environment nor the errors. A declaration that fails a name or type check is left out of the
// later checks; its overload identifier still counts as declared, so it produces no second error.
internal sealed class DeclarationChecks
{
    internal const string ApplicationSource = "application";

    private readonly DeclarationSet _set;
    private readonly LibraryDeclarations _library;
    private readonly List<CelError> _errors = [];

    // The identifiers of all overloads the application declares, those left out of the checks included.
    private readonly HashSet<string> _declaredIds = new(StringComparer.Ordinal);

    // The overloads of the application that passed the name and type checks, distinct, by identifier.
    private readonly Dictionary<string, List<(string Function, CelOverloadDecl Overload)>> _byId = new(StringComparer.Ordinal);

    // The overloads of the application with an identifier of their own, by identifier.
    private readonly SortedDictionary<string, (string Function, CelOverloadDecl Overload)> _unique = new(StringComparer.Ordinal);

    private readonly Dictionary<string, List<ImplementationEntry>> _implementations = new(StringComparer.Ordinal);

    private DeclarationChecks(DeclarationSet set, LibraryDeclarations library)
    {
        _set = set;
        _library = library;
    }

    // The errors of the set; when there are none, the variables with type parameters erased to dyn, the
    // overloads of the application ordered by identifier, and the container, empty without one.
    internal static ImmutableArray<CelError> Check(
        DeclarationSet set,
        LibraryDeclarations library,
        out ImmutableArray<CelVariableDecl> variables,
        out ImmutableArray<OverloadBinding> overloads,
        out string container)
    {
        var checks = new DeclarationChecks(set, library);
        variables = checks.CheckVariables();
        checks.CheckContainers();
        checks.CheckFunctions();
        checks.CheckOverloadIds();
        checks.CheckOverlaps();
        checks.CheckImplementations();
        overloads = checks.Bindings();
        container = set.Containers.IsEmpty ? string.Empty : set.Containers[0];
        return checks.Errors();
    }

    private ImmutableArray<CelVariableDecl> CheckVariables()
    {
        var byName = new Dictionary<string, CelVariableDecl>(StringComparer.Ordinal);
        ImmutableArray<CelVariableDecl>.Builder erased = ImmutableArray.CreateBuilder<CelVariableDecl>();
        foreach (CelVariableDecl variable in _set.Variables)
        {
            if (!DeclarationNames.IsQualifiedIdentifier(variable.Name))
            {
                _errors.Add(BuildErrors.InvalidVariableName(variable.Name, ApplicationSource));
                continue;
            }

            if (Undeclared(variable.Type) is CelType undeclared)
            {
                _errors.Add(BuildErrors.TypeNotDeclared("variable", variable.Name, undeclared.ToString()));
                continue;
            }

            if (_library.ReservedNames.Contains(variable.Name))
            {
                _errors.Add(BuildErrors.StandardLibraryConflict(
                    "variable", variable.Name, ApplicationSource, _library.VersionText, "it is a type identifier"));
            }

            ref CelVariableDecl? kept = ref CollectionsMarshal.GetValueRefOrAddDefault(byName, variable.Name, out bool exists);
            if (!exists)
            {
                kept = variable;
                CelType type = AssignabilityRules.Erase(variable.Type);
                erased.Add(ReferenceEquals(type, variable.Type) ? variable : new CelVariableDecl(variable.Name, type));
            }
            else if (!kept!.SameAs(variable))
            {
                _errors.Add(BuildErrors.DuplicateVariable(variable.Name, ApplicationSource, ApplicationSource));
            }
        }

        foreach (UnmappedDeclaration unmapped in _set.Unmapped)
        {
            if (unmapped.Function is not null)
            {
                continue;
            }

            _errors.Add(DeclarationNames.IsQualifiedIdentifier(unmapped.Name)
                ? BuildErrors.TypeNotDeclared("variable", unmapped.Name, unmapped.ClrType.ToString())
                : BuildErrors.InvalidVariableName(unmapped.Name, ApplicationSource));
        }

        return erased.ToImmutable();
    }

    // Two different non-empty containers: the smallest one against each other one.
    private void CheckContainers()
    {
        string[] containers = [.. _set.Containers];
        Array.Sort(containers, StringComparer.Ordinal);
        for (int i = 1; i < containers.Length; i++)
        {
            _errors.Add(BuildErrors.ContainerConflict(containers[0], ApplicationSource, containers[i], ApplicationSource));
        }
    }

    // The names of the functions and the types of their overloads; a function in the form of a macro.
    private void CheckFunctions()
    {
        foreach (CelFunctionDecl function in _set.Functions)
        {
            bool validName = DeclarationNames.IsQualifiedIdentifier(function.Name);
            if (!validName)
            {
                _errors.Add(BuildErrors.InvalidFunctionName(function.Name, ApplicationSource));
            }

            foreach (CelOverloadDecl overload in function.Overloads)
            {
                _declaredIds.Add(overload.OverloadId);
                if (validName && Passes(function.Name, overload))
                {
                    Add(function.Name, overload);
                }
            }
        }

        foreach (UnmappedDeclaration unmapped in _set.Unmapped)
        {
            if (unmapped.Function is not string function)
            {
                continue;
            }

            _declaredIds.Add(unmapped.Name);
            _errors.Add(DeclarationNames.IsQualifiedIdentifier(function)
                ? BuildErrors.TypeNotDeclared("overload", unmapped.Name, unmapped.ClrType.ToString())
                : BuildErrors.InvalidFunctionName(function, ApplicationSource));
        }
    }

    // Whether an overload passes the type check and does not have the form of a macro.
    private bool Passes(string function, CelOverloadDecl overload)
    {
        if (Undeclared(overload) is CelType undeclared)
        {
            _errors.Add(BuildErrors.TypeNotDeclared("overload", overload.OverloadId, undeclared.ToString()));
            return false;
        }

        if (MacroForm(function, overload))
        {
            _errors.Add(BuildErrors.StandardLibraryConflict(
                "function", function, ApplicationSource, _library.VersionText, "it has the form of the macro '" + function + "'"));
            return false;
        }

        return true;
    }

    // An overload that is not the same as one already kept under its identifier.
    private void Add(string function, CelOverloadDecl overload)
    {
        List<(string Function, CelOverloadDecl Overload)> sameId = CollectionsMarshal.GetValueRefOrAddDefault(_byId, overload.OverloadId, out _) ??= [];

        foreach ((string keptFunction, CelOverloadDecl kept) in sameId)
        {
            if (string.Equals(keptFunction, function, StringComparison.Ordinal) && kept.SameAs(overload))
            {
                return;
            }
        }

        sameId.Add((function, overload));
    }

    // One identifier for overloads that are not the same, in the application or across the application
    // and the version. The same overload as one of the version is kept, so that it conflicts with it.
    private void CheckOverloadIds()
    {
        foreach ((string id, List<(string Function, CelOverloadDecl Overload)> sameId) in _byId)
        {
            if (_library.ById.TryGetValue(id, out OverloadBinding? ofLibrary))
            {
                foreach ((string function, CelOverloadDecl overload) in sameId)
                {
                    if (!string.Equals(function, ofLibrary.Function, StringComparison.Ordinal) || !overload.SameAs(ofLibrary.Declaration))
                    {
                        _errors.Add(BuildErrors.DuplicateOverloadId(id, ofLibrary.Source, ApplicationSource));
                    }
                    else
                    {
                        _unique.TryAdd(id, (function, overload));
                    }
                }
            }
            else if (sameId.Count > 1)
            {
                for (int i = 1; i < sameId.Count; i++)
                {
                    _errors.Add(BuildErrors.DuplicateOverloadId(id, ApplicationSource, ApplicationSource));
                }
            }
            else
            {
                _unique.Add(id, sameId[0]);
            }
        }
    }

    // Overloads of one function, call style and parameter count that overlap after erasure: with an
    // overload of the version, or with another overload of the application, the smaller identifier first.
    private void CheckOverlaps()
    {
        var byCall = new Dictionary<(string Function, bool IsReceiverStyle, int Count), List<(string Id, CelOverloadDecl Overload)>>();
        foreach ((string id, (string function, CelOverloadDecl overload)) in _unique)
        {
            CheckOverlapWithLibrary(function, overload);
            (string, bool, int) key = (function, overload.IsReceiverStyle, overload.ParameterTypes.Length);
            List<(string Id, CelOverloadDecl Overload)> sameCall = CollectionsMarshal.GetValueRefOrAddDefault(byCall, key, out _) ??= [];

            // In the order of the identifiers, as _unique enumerates them.
            foreach ((string earlierId, CelOverloadDecl earlier) in sameCall)
            {
                if (Overlap(earlier, overload))
                {
                    _errors.Add(BuildErrors.OverlappingOverloads(earlierId, ApplicationSource, id, ApplicationSource, function));
                }
            }

            sameCall.Add((id, overload));
        }
    }

    private void CheckOverlapWithLibrary(string function, CelOverloadDecl overload)
    {
        if (!_library.ByFunction.TryGetValue(function, out ImmutableArray<OverloadBinding> ofLibrary))
        {
            return;
        }

        foreach (OverloadBinding standard in ofLibrary)
        {
            CelOverloadDecl declaration = standard.Declaration;
            if (declaration.IsReceiverStyle == overload.IsReceiverStyle
                && declaration.ParameterTypes.Length == overload.ParameterTypes.Length
                && Overlap(declaration, overload))
            {
                _errors.Add(BuildErrors.StandardLibraryConflict(
                    "overload", overload.OverloadId, ApplicationSource, _library.VersionText, "it overlaps overload '" + declaration.OverloadId + "'"));
            }
        }
    }

    // Implementations against the declared overloads: one per identifier, none for an overload of the
    // version, each for a declared overload with a fitting signature, and one for each overload.
    private void CheckImplementations()
    {
        foreach (ImplementationEntry implementation in _set.Implementations)
        {
            (CollectionsMarshal.GetValueRefOrAddDefault(_implementations, implementation.OverloadId, out _) ??= []).Add(implementation);
        }

        foreach ((string id, List<ImplementationEntry> sameId) in _implementations)
        {
            if (sameId.Count > 1 || _library.ById.ContainsKey(id))
            {
                _errors.Add(BuildErrors.DuplicateImplementation(id));
            }
            else if (!_declaredIds.Contains(id))
            {
                _errors.Add(BuildErrors.UnusedImplementation(id));
            }
            else if (_unique.TryGetValue(id, out (string Function, CelOverloadDecl Overload) declared)
                && sameId[0].IsTyped
                && !SignatureFits(sameId[0], declared.Overload))
            {
                _errors.Add(BuildErrors.ImplementationSignatureMismatch(id, DeclarationText.Signature(declared.Function, declared.Overload)));
            }
        }

        foreach ((string id, List<(string Function, CelOverloadDecl Overload)> sameId) in _byId)
        {
            if (!_library.ById.ContainsKey(id) && !_implementations.ContainsKey(id))
            {
                _errors.Add(BuildErrors.MissingImplementation(id, sameId[0].Function));
            }
        }
    }

    // The overloads of the application with their implementations and the cost of a call: 1 unit and the
    // cost function of the declaration. Empty when there are errors.
    private ImmutableArray<OverloadBinding> Bindings()
    {
        if (_errors.Count > 0)
        {
            return [];
        }

        ImmutableArray<OverloadBinding>.Builder bindings = ImmutableArray.CreateBuilder<OverloadBinding>(_unique.Count);
        foreach ((string id, (string function, CelOverloadDecl overload)) in _unique)
        {
            OverloadImplementation implementation = _implementations[id][0].Implementation;
            bindings.Add(new OverloadBinding(function, overload, implementation, 1, overload.Cost, ApplicationSource, isApplication: true));
        }

        return bindings.MoveToImmutable();
    }

    // By code, then by message, ordinal; an error found twice is reported once.
    private ImmutableArray<CelError> Errors()
    {
        _errors.Sort(static (left, right) =>
        {
            int byCode = left.Code.CompareTo(right.Code);
            return byCode != 0 ? byCode : string.CompareOrdinal(left.Message, right.Message);
        });

        ImmutableArray<CelError>.Builder errors = ImmutableArray.CreateBuilder<CelError>(_errors.Count);
        foreach (CelError error in _errors)
        {
            if (errors.Count == 0 || errors[^1].Code != error.Code || !string.Equals(errors[^1].Message, error.Message, StringComparison.Ordinal))
            {
                errors.Add(error);
            }
        }

        return errors.ToImmutable();
    }

    // The CEL types of a typed delegate equal the declared types with type parameters erased to dyn, so
    // dyn and type parameters take CelValue and object; a declared wrapper(P) takes also string or byte[]
    // whose type is P.
    private static bool SignatureFits(ImplementationEntry implementation, CelOverloadDecl overload)
    {
        ImmutableArray<CelType?> parameterTypes = implementation.ParameterTypes;
        ImmutableArray<bool> takesNull = implementation.ParameterTakesNull;
        if (parameterTypes.Length != overload.ParameterTypes.Length
            || !Fits(implementation.ResultType, implementation.ResultTakesNull, overload.ResultType))
        {
            return false;
        }

        for (int i = 0; i < parameterTypes.Length; i++)
        {
            if (!Fits(parameterTypes[i], !takesNull.IsDefault && takesNull[i], overload.ParameterTypes[i]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool Fits(CelType? clrMapped, bool takesNull, CelType declared)
    {
        if (clrMapped is null)
        {
            return false;
        }

        CelType erased = AssignabilityRules.Erase(declared);
        return clrMapped.Equals(erased)
            || (takesNull && erased.Kind == CelTypeKind.Wrapper && clrMapped.Equals(erased.Parameters[0]));
    }

    // Overloads of one call style and parameter count overlap when every pair of parameter types overlaps.
    private static bool Overlap(CelOverloadDecl first, CelOverloadDecl second)
    {
        for (int i = 0; i < first.ParameterTypes.Length; i++)
        {
            if (!AssignabilityRules.Overlap(first.ParameterTypes[i], second.ParameterTypes[i]))
            {
                return false;
            }
        }

        return true;
    }

    // has(x) globally; all, exists, exists_one, filter and map on a receiver with two arguments; map on a
    // receiver with three.
    private static bool MacroForm(string function, CelOverloadDecl overload)
    {
        int arguments = overload.ParameterTypes.Length - (overload.IsReceiverStyle ? 1 : 0);
        if (!overload.IsReceiverStyle)
        {
            return arguments == 1 && function == "has";
        }

        return function switch
        {
            "all" or "exists" or "exists_one" or "filter" => arguments == 2,
            "map" => arguments is 2 or 3,
            _ => false,
        };
    }

    // The first type of an overload that nothing in the environment declares, parameters before the result.
    private static CelType? Undeclared(CelOverloadDecl overload)
    {
        foreach (CelType parameterType in overload.ParameterTypes)
        {
            if (Undeclared(parameterType) is CelType undeclared)
            {
                return undeclared;
            }
        }

        return Undeclared(overload.ResultType);
    }

    // An object type, which no type source describes yet, or a type of time, optional or decimal, which no
    // extension set declares yet.
    private static CelType? Undeclared(CelType type)
    {
        if (type.Kind is CelTypeKind.Object or CelTypeKind.Any or CelTypeKind.Optional)
        {
            return type;
        }

        foreach (CelType parameter in type.Parameters)
        {
            if (Undeclared(parameter) is CelType undeclared)
            {
                return undeclared;
            }
        }

        return null;
    }
}
