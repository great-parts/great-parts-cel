using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Runtime.InteropServices;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Declarations;

// The checks of Build over the whole set of declarations against a version of the standard library. Every
// error found is reported, ordered by code and then by message, so the order of the builder calls changes
// neither the environment nor the errors. A declaration that fails a name or type check is left out of the
// later checks; its overload identifier still counts as declared, so it produces no second error.
internal sealed class DeclarationChecks
{
    internal const string ApplicationSource = "application";

    // The declarations to check: the set of the builder, with the declarations of the types of the application that the type
    // index describes resolved (ResolveUnmapped).
    private DeclarationSet _set;
    private readonly LibraryDeclarations _library;
    private readonly List<CelError> _errors = [];

    // The identifiers of all overloads the application declares, those left out of the checks included.
    private readonly HashSet<string> _declaredIds = new(StringComparer.Ordinal);

    // The overloads of the application and of descriptions in JSON that passed the name and type checks, distinct, by
    // identifier.
    private readonly Dictionary<string, List<OverloadEntry>> _byId = new(StringComparer.Ordinal);

    // The overloads with an identifier of their own, by identifier.
    private readonly SortedDictionary<string, OverloadEntry> _unique = new(StringComparer.Ordinal);

    private readonly Dictionary<string, List<ImplementationEntry>> _implementations = new(StringComparer.Ordinal);

    // The names of all object and enumeration types of the type sources, those in conflict included, so that a type
    // that names one of them is declared and produces no second error.
    private readonly HashSet<string> _typeNames = new(StringComparer.Ordinal);

    private DeclarationChecks(DeclarationSet set, LibraryDeclarations library)
    {
        _set = set;
        _library = library;
    }

    // The errors of the set; when there are none, the variables with type parameters erased to dyn, the
    // overloads of the application ordered by identifier, the container, empty without one, and the type index.
    internal static ImmutableArray<CelError> Check(
        DeclarationSet set,
        LibraryDeclarations library,
        out ImmutableArray<CelVariableDecl> variables,
        out ImmutableArray<OverloadBinding> overloads,
        out string container,
        out TypeIndex types)
    {
        var checks = new DeclarationChecks(set, library);
        types = checks.CheckTypes();
        checks.ResolveUnmapped(types);
        checks.ResolveImplementations(types);
        variables = checks.CheckVariables();
        checks.CheckContainers();
        checks.CheckFunctions();
        checks.CheckOverloadIds();
        checks.CheckOverlaps();
        checks.CheckImplementations();
        overloads = checks.Bindings();
        container = set.Containers.IsEmpty ? string.Empty : set.Containers[0].Value;
        return checks.Errors();
    }

    // The descriptions of the type sources: one CEL name and one .NET type for each description, no name of a type
    // identifier of the version, and declared object types in the types of fields. The same description instance in two
    // sources is one declaration. The index holds the descriptions as they are; with errors there is no environment.
    private TypeIndex CheckTypes()
    {
        if (_set.TypeSources.IsEmpty)
        {
            return TypeIndex.Empty;
        }

        var byName = new Dictionary<string, List<TypeDeclaration>>(StringComparer.Ordinal);
        var byClrType = new Dictionary<Type, List<TypeDeclaration>>();
        List<TypeDeclaration> objectTypes = CollectTypes(byName, byClrType);
        string library = StandardLibrarySource;
        foreach ((string name, List<TypeDeclaration> declarations) in byName)
        {
            _typeNames.Add(name);
            if (_library.ReservedNames.Contains(name))
            {
                foreach (TypeDeclaration declaration in declarations)
                {
                    _errors.Add(BuildErrors.DuplicateType(name, library, declaration.Source.Text, declaration.Position));
                }
            }
            else
            {
                AddDuplicateTypes(name, declarations);
            }
        }

        foreach ((Type clrType, List<TypeDeclaration> declarations) in byClrType)
        {
            AddDuplicateTypes(clrType.ToString(), declarations);
        }

        return Index(objectTypes, byName);
    }

    // The distinct descriptions of the type sources, in the order of the sources, by name and by .NET type; the object
    // types among them.
    private List<TypeDeclaration> CollectTypes(Dictionary<string, List<TypeDeclaration>> byName, Dictionary<Type, List<TypeDeclaration>> byClrType)
    {
        var seen = new HashSet<object>(ReferenceEqualityComparer.Instance);
        var objectTypes = new List<TypeDeclaration>();
        foreach (TypeSource source in _set.TypeSources)
        {
            foreach (CelObjectType objectType in source.Provider.ObjectTypes)
            {
                if (seen.Add(objectType))
                {
                    var declaration = new TypeDeclaration(objectType, source, seen.Count);
                    objectTypes.Add(declaration);
                    (CollectionsMarshal.GetValueRefOrAddDefault(byName, objectType.Name, out _) ??= []).Add(declaration);
                    if (objectType.ClrType is Type clrType)
                    {
                        (CollectionsMarshal.GetValueRefOrAddDefault(byClrType, clrType, out _) ??= []).Add(declaration);
                    }
                }
            }

            foreach (CelEnumType enumType in source.Provider.EnumTypes)
            {
                if (seen.Add(enumType))
                {
                    (CollectionsMarshal.GetValueRefOrAddDefault(byName, enumType.Name, out _) ??= []).Add(new TypeDeclaration(enumType, source, seen.Count));
                }
            }
        }

        return objectTypes;
    }

    // The declarations whose .NET type is a class, record, struct or interface of the application, which the input rows of the
    // type mapping do not cover: by the type index, such a variable is declared with the object type that a type source
    // describes for exactly that .NET type, and such a delegate form of a function is the overload of the object types of its
    // parameters and result with its implementation. A type that no source describes stays unmapped and is TypeNotDeclared; the
    // .NET type that the error names is the first that has no CEL type.
    private void ResolveUnmapped(TypeIndex types)
    {
        if (_set.Unmapped.IsEmpty || types.ObjectTypes.Count == 0)
        {
            return;
        }

        List<CelVariableDecl>? variables = null;
        List<CelFunctionDecl>? functions = null;
        List<ImplementationEntry>? implementations = null;
        ImmutableArray<UnmappedDeclaration>.Builder remaining = ImmutableArray.CreateBuilder<UnmappedDeclaration>(_set.Unmapped.Length);
        foreach (UnmappedDeclaration unmapped in _set.Unmapped)
        {
            if (unmapped.Function is null)
            {
                if (ConversionRules.IsApplicationObject(unmapped.ClrType) && types.TryGetByClrType(unmapped.ClrType, out CelObjectType? described))
                {
                    (variables ??= []).Add(new CelVariableDecl(unmapped.Name, described.Type));
                    continue;
                }
            }
            else if (unmapped.Delegate is { } declaration)
            {
                if (declaration.TryResolve(types, out string overloadId, out CelFunctionDecl? function, out ImplementationEntry? implementation, out Type? unresolved))
                {
                    (functions ??= []).Add(function!);
                    AddImplementation(ref implementations, implementation!);
                    continue;
                }

                // The error names the first .NET type without a CEL type, and the identifier with the types that the index gave.
                remaining.Add(unmapped with { Name = overloadId, ClrType = unresolved! });
                continue;
            }

            remaining.Add(unmapped);
        }

        if (variables is null && functions is null)
        {
            _set = new DeclarationSet(_set.Variables, _set.Functions, _set.Implementations, _set.Containers, remaining.DrainToImmutable(), _set.TypeSources);
            return;
        }

        _set = new DeclarationSet(
            [.. _set.Variables, .. InApplication(variables)],
            [.. _set.Functions, .. InApplication(functions)],
            [.. _set.Implementations, .. implementations ?? []],
            _set.Containers,
            remaining.DrainToImmutable(),
            _set.TypeSources);
    }

    // The typed implementations of AddFunctionImplementation whose parameter or result is a class, record, struct or interface
    // of the application, given the object types that the type index describes for those .NET types; the signature check
    // compares them with the declared overload. A type that no source describes is TypeNotDeclared (CheckImplementations).
    private void ResolveImplementations(TypeIndex types)
    {
        ImmutableArray<ImplementationEntry>.Builder? resolved = null;
        for (int i = 0; i < _set.Implementations.Length; i++)
        {
            ImplementationEntry entry = _set.Implementations[i];
            ImplementationEntry resolvedEntry = entry.Resolve(types);
            if (!ReferenceEquals(resolvedEntry, entry))
            {
                resolved ??= _set.Implementations.ToBuilder();
                resolved[i] = resolvedEntry;
            }
        }

        if (resolved is not null)
        {
            _set = new DeclarationSet(_set.Variables, _set.Functions, resolved.MoveToImmutable(), _set.Containers, _set.Unmapped, _set.TypeSources);
        }
    }

    private static ImmutableArray<SourcedDeclaration<T>> InApplication<T>(List<T>? declarations)
        where T : notnull
    {
        if (declarations is null)
        {
            return [];
        }

        ImmutableArray<SourcedDeclaration<T>>.Builder sourced = ImmutableArray.CreateBuilder<SourcedDeclaration<T>>(declarations.Count);
        foreach (T declaration in declarations)
        {
            sourced.Add(SourcedDeclaration<T>.InApplication(declaration));
        }

        return sourced.MoveToImmutable();
    }

    // The same delegate registered twice under one identifier is one implementation, as the builder keeps it.
    private void AddImplementation(ref List<ImplementationEntry>? added, ImplementationEntry implementation)
    {
        foreach (ImplementationEntry kept in _set.Implementations)
        {
            if (kept.SameAs(implementation))
            {
                return;
            }
        }

        added ??= [];
        foreach (ImplementationEntry kept in added)
        {
            if (kept.SameAs(implementation))
            {
                return;
            }
        }

        added.Add(implementation);
    }

    // The type index of the descriptions, after the checks of the types of their fields.
    private TypeIndex Index(List<TypeDeclaration> objectTypes, Dictionary<string, List<TypeDeclaration>> byName)
    {
        var index = new Dictionary<string, CelObjectType>(StringComparer.Ordinal);
        var enumConstants = new Dictionary<string, long>(StringComparer.Ordinal);
        var describedClrTypes = new Dictionary<Type, CelObjectType>();
        foreach (TypeDeclaration declaration in objectTypes)
        {
            var objectType = (CelObjectType)declaration.Description;
            CheckFieldTypes(objectType, declaration.Source);
            index.TryAdd(objectType.Name, objectType);
            if (objectType.ClrType is Type clrType)
            {
                describedClrTypes.TryAdd(clrType, objectType);
            }
        }

        foreach (List<TypeDeclaration> declarations in byName.Values)
        {
            if (declarations[0].Description is CelEnumType enumType)
            {
                foreach (KeyValuePair<string, long> constant in enumType.Constants)
                {
                    enumConstants.TryAdd(enumType.Name + "." + constant.Key, constant.Value);
                }
            }
        }

        return new TypeIndex(index, enumConstants, describedClrTypes);
    }

    // Descriptions of one name or one .NET type: the first in the order of sources against each other one.
    private void AddDuplicateTypes(string name, List<TypeDeclaration> declarations)
    {
        if (declarations.Count < 2)
        {
            return;
        }

        declarations.Sort(TypeDeclaration.Compare);
        TypeDeclaration first = declarations[0];
        for (int i = 1; i < declarations.Count; i++)
        {
            TypeDeclaration other = declarations[i];
            _errors.Add(BuildErrors.DuplicateType(name, first.Source.Text, other.Source.Text, first.Position ?? other.Position));
        }
    }

    // An object type in the type of a field that no type source describes.
    private void CheckFieldTypes(CelObjectType objectType, TypeSource source)
    {
        foreach (CelObjectField field in objectType.Fields)
        {
            if (Undeclared(field.Type) is CelType undeclared)
            {
                _errors.Add(BuildErrors.TypeNotDeclared("field", objectType.Name + "." + field.Name, undeclared.ToString(), source.PositionOf(field)));
            }
        }
    }

    // The source text of the standard library version, such as standard library v1.
    private string StandardLibrarySource => "standard library " + _library.VersionText;

    private ImmutableArray<CelVariableDecl> CheckVariables()
    {
        var byName = new Dictionary<string, List<SourcedDeclaration<CelVariableDecl>>>(StringComparer.Ordinal);
        ImmutableArray<CelVariableDecl>.Builder erased = ImmutableArray.CreateBuilder<CelVariableDecl>();
        foreach (SourcedDeclaration<CelVariableDecl> sourced in _set.Variables)
        {
            CelVariableDecl variable = sourced.Value;
            if (!DeclarationNames.IsQualifiedIdentifier(variable.Name))
            {
                _errors.Add(BuildErrors.InvalidVariableName(variable.Name, sourced.Source, sourced.Position));
                continue;
            }

            if (Undeclared(variable.Type) is CelType undeclared)
            {
                _errors.Add(BuildErrors.TypeNotDeclared("variable", variable.Name, undeclared.ToString(), sourced.Position));
                continue;
            }

            if (_library.ReservedNames.Contains(variable.Name))
            {
                _errors.Add(BuildErrors.StandardLibraryConflict(
                    "variable", variable.Name, sourced.Source, _library.VersionText, "it is a type identifier", sourced.Position));
            }

            List<SourcedDeclaration<CelVariableDecl>> sameName = CollectionsMarshal.GetValueRefOrAddDefault(byName, variable.Name, out bool exists) ??= [];
            if (!exists)
            {
                CelType type = AssignabilityRules.Erase(variable.Type);
                erased.Add(ReferenceEquals(type, variable.Type) ? variable : new CelVariableDecl(variable.Name, type));
            }

            sameName.Add(sourced);
        }

        foreach (List<SourcedDeclaration<CelVariableDecl>> sameName in byName.Values)
        {
            AddDuplicateVariables(sameName);
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

    // Declarations of one variable name: the first by source against each other one that is not the same declaration.
    private void AddDuplicateVariables(List<SourcedDeclaration<CelVariableDecl>> sameName)
    {
        if (sameName.Count < 2)
        {
            return;
        }

        sameName.Sort(static (left, right) => left.CompareTo(right));
        SourcedDeclaration<CelVariableDecl> first = sameName[0];
        for (int i = 1; i < sameName.Count; i++)
        {
            SourcedDeclaration<CelVariableDecl> other = sameName[i];
            if (!first.Value.SameAs(other.Value))
            {
                _errors.Add(BuildErrors.DuplicateVariable(first.Value.Name, first.Source, other.Source, first.Position ?? other.Position));
            }
        }
    }

    // Two different non-empty containers: the first by source and container against each other one.
    private void CheckContainers()
    {
        SourcedDeclaration<string>[] containers = [.. _set.Containers];
        Array.Sort(
            containers,
            static (left, right) =>
            {
                int bySource = string.CompareOrdinal(left.Source, right.Source);
                return bySource != 0 ? bySource : string.CompareOrdinal(left.Value, right.Value);
            });

        for (int i = 1; i < containers.Length; i++)
        {
            SourcedDeclaration<string> first = containers[0];
            SourcedDeclaration<string> other = containers[i];
            _errors.Add(BuildErrors.ContainerConflict(first.Value, first.Source, other.Value, other.Source, first.Position ?? other.Position));
        }
    }

    // The names of the functions and the types of their overloads; a function in the form of a macro.
    private void CheckFunctions()
    {
        foreach (SourcedDeclaration<CelFunctionDecl> sourced in _set.Functions)
        {
            CelFunctionDecl function = sourced.Value;
            bool validName = DeclarationNames.IsQualifiedIdentifier(function.Name);
            if (!validName)
            {
                _errors.Add(BuildErrors.InvalidFunctionName(function.Name, sourced.Source, sourced.Position));
            }

            foreach (CelOverloadDecl overload in function.Overloads)
            {
                _declaredIds.Add(overload.OverloadId);
                if (validName && Passes(sourced, overload))
                {
                    Add(new OverloadEntry(function.Name, overload, sourced.Source, sourced.PartPosition(overload)));
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
    private bool Passes(SourcedDeclaration<CelFunctionDecl> sourced, CelOverloadDecl overload)
    {
        string function = sourced.Value.Name;
        if (Undeclared(overload) is CelType undeclared)
        {
            _errors.Add(BuildErrors.TypeNotDeclared("overload", overload.OverloadId, undeclared.ToString(), sourced.PartPosition(overload)));
            return false;
        }

        if (MacroForm(function, overload))
        {
            _errors.Add(BuildErrors.StandardLibraryConflict(
                "function", function, sourced.Source, _library.VersionText, "it has the form of the macro '" + function + "'", sourced.Position));
            return false;
        }

        return true;
    }

    // An overload that is not the same as one already kept under its identifier; of two equal ones the first by source stays.
    private void Add(OverloadEntry added)
    {
        List<OverloadEntry> sameId = CollectionsMarshal.GetValueRefOrAddDefault(_byId, added.Overload.OverloadId, out _) ??= [];

        for (int i = 0; i < sameId.Count; i++)
        {
            OverloadEntry kept = sameId[i];
            if (string.Equals(kept.Function, added.Function, StringComparison.Ordinal) && kept.Overload.SameAs(added.Overload))
            {
                if (added.CompareTo(kept) < 0)
                {
                    sameId[i] = added;
                }

                return;
            }
        }

        sameId.Add(added);
    }

    // One identifier for overloads that are not the same, in the application, in descriptions in JSON or across them and
    // the version. The same overload as one of the version is kept, so that it conflicts with it. Of overloads with one
    // identifier the first by source is named first.
    private void CheckOverloadIds()
    {
        foreach ((string id, List<OverloadEntry> sameId) in _byId)
        {
            sameId.Sort(static (left, right) => left.CompareTo(right));
            if (_library.ById.TryGetValue(id, out OverloadBinding? ofLibrary))
            {
                foreach (OverloadEntry entry in sameId)
                {
                    if (!string.Equals(entry.Function, ofLibrary.Function, StringComparison.Ordinal) || !entry.Overload.SameAs(ofLibrary.Declaration))
                    {
                        _errors.Add(BuildErrors.DuplicateOverloadId(id, ofLibrary.Source, entry.Source, entry.Position));
                    }
                    else
                    {
                        _unique.TryAdd(id, entry);
                    }
                }
            }
            else if (sameId.Count > 1)
            {
                OverloadEntry first = sameId[0];
                for (int i = 1; i < sameId.Count; i++)
                {
                    _errors.Add(BuildErrors.DuplicateOverloadId(id, first.Source, sameId[i].Source, first.Position ?? sameId[i].Position));
                }
            }
            else
            {
                _unique.Add(id, sameId[0]);
            }
        }
    }

    // Overloads of one function, call style and parameter count that overlap after erasure: with an
    // overload of the version, or with another overload; the first by source, then by identifier, is named first.
    private void CheckOverlaps()
    {
        var byCall = new Dictionary<(string Function, bool IsReceiverStyle, int Count), List<OverloadEntry>>();
        foreach ((_, OverloadEntry entry) in _unique)
        {
            CelOverloadDecl overload = entry.Overload;
            CheckOverlapWithLibrary(entry);
            (string, bool, int) key = (entry.Function, overload.IsReceiverStyle, overload.ParameterTypes.Length);
            List<OverloadEntry> sameCall = CollectionsMarshal.GetValueRefOrAddDefault(byCall, key, out _) ??= [];

            // In the order of the identifiers, as _unique enumerates them.
            foreach (OverloadEntry earlier in sameCall)
            {
                if (Overlap(earlier.Overload, overload))
                {
                    (OverloadEntry first, OverloadEntry second) = string.CompareOrdinal(entry.Source, earlier.Source) < 0 ? (entry, earlier) : (earlier, entry);
                    _errors.Add(BuildErrors.OverlappingOverloads(
                        first.Overload.OverloadId,
                        first.Source,
                        second.Overload.OverloadId,
                        second.Source,
                        entry.Function,
                        first.Position ?? second.Position));
                }
            }

            sameCall.Add(entry);
        }
    }

    private void CheckOverlapWithLibrary(OverloadEntry entry)
    {
        if (!_library.ByFunction.TryGetValue(entry.Function, out ImmutableArray<OverloadBinding> ofLibrary))
        {
            return;
        }

        CelOverloadDecl overload = entry.Overload;
        foreach (OverloadBinding standard in ofLibrary)
        {
            CelOverloadDecl declaration = standard.Declaration;
            if (declaration.IsReceiverStyle == overload.IsReceiverStyle
                && declaration.ParameterTypes.Length == overload.ParameterTypes.Length
                && Overlap(declaration, overload))
            {
                _errors.Add(BuildErrors.StandardLibraryConflict(
                    "overload",
                    overload.OverloadId,
                    entry.Source,
                    _library.VersionText,
                    "it overlaps overload '" + declaration.OverloadId + "'",
                    entry.Position));
            }
        }
    }

    // Implementations against the declared overloads: one per identifier, none for an overload of the
    // version, each for a declared overload with a fitting signature, and one for each overload. A typed implementation
    // with a type of the application that no source describes is TypeNotDeclared, not a mismatch of the signature.
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
            else if (sameId[0].Unresolved is Type unresolved)
            {
                _errors.Add(BuildErrors.TypeNotDeclared("overload", id, unresolved.ToString()));
            }
            else if (_unique.TryGetValue(id, out OverloadEntry? declared)
                && sameId[0].IsTyped
                && !SignatureFits(sameId[0], declared.Overload))
            {
                _errors.Add(BuildErrors.ImplementationSignatureMismatch(
                    id, DeclarationText.Signature(declared.Function, declared.Overload), declared.Position));
            }
        }

        // Of the declarations of one identifier the first by source names the function; the error has the place of the first
        // one from a description in JSON.
        foreach ((string id, List<OverloadEntry> sameId) in _byId)
        {
            if (!_library.ById.ContainsKey(id) && !_implementations.ContainsKey(id))
            {
                _errors.Add(BuildErrors.MissingImplementation(id, sameId[0].Function, sameId.Find(static entry => entry.Position is not null)?.Position));
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
        foreach ((string id, OverloadEntry entry) in _unique)
        {
            OverloadImplementation implementation = _implementations[id][0].Implementation;
            bindings.Add(new OverloadBinding(entry.Function, entry.Overload, implementation, 1, entry.Overload.Cost, entry.Source, isApplication: true));
        }

        return bindings.MoveToImmutable();
    }

    // By code, then by message, ordinal; an error found twice is reported once.
    private ImmutableArray<CelError> Errors()
    {
        _errors.Sort(static (left, right) =>
        {
            int byCode = left.Code.CompareTo(right.Code);
            if (byCode != 0)
            {
                return byCode;
            }

            int byMessage = string.CompareOrdinal(left.Message, right.Message);
            return byMessage != 0
                ? byMessage
                : (left.SourceLocation?.Offset ?? int.MaxValue).CompareTo(right.SourceLocation?.Offset ?? int.MaxValue);
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
            || !FitsResult(implementation.ResultType, implementation.ResultTakesNull, overload.ResultType))
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

    // A result: also list(Class), the type of a list of a class of the application, fits a declared list(dyn), as dyn takes any
    // element. A parameter has no such row: a list of a class has no expected type.
    private static bool FitsResult(CelType? clrMapped, bool takesNull, CelType declared)
    {
        if (Fits(clrMapped, takesNull, declared))
        {
            return true;
        }

        CelType erased = AssignabilityRules.Erase(declared);
        return clrMapped is { Kind: CelTypeKind.List } && erased.Kind == CelTypeKind.List && IsListOfClassFitsDyn(clrMapped, erased);
    }

    private static bool IsListOfClassFitsDyn(CelType clrList, CelType declaredList)
    {
        CelType element = clrList.Parameters[0];
        CelType declaredElement = declaredList.Parameters[0];
        return element.Kind == CelTypeKind.Object && declaredElement.Kind == CelTypeKind.Dyn;
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
    private CelType? Undeclared(CelOverloadDecl overload)
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

    // An object type that no type source describes, or a type of time, optional or decimal, which no extension set
    // declares yet.
    private CelType? Undeclared(CelType type)
    {
        if ((type.Kind == CelTypeKind.Object && !_typeNames.Contains(type.Name)) || type.Kind is CelTypeKind.Any or CelTypeKind.Optional)
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

    // An overload that passed the checks: its function, its source and, for a description in JSON, its position. Two
    // overloads are ordered by the source text, ordinal, the position in the JSON text, then the function.
    private sealed record OverloadEntry(string Function, CelOverloadDecl Overload, string Source, CelSourceLocation? Position)
    {
        internal int CompareTo(OverloadEntry other)
        {
            int bySource = string.CompareOrdinal(Source, other.Source);
            if (bySource != 0)
            {
                return bySource;
            }

            int byPosition = (Position?.Offset ?? int.MaxValue).CompareTo(other.Position?.Offset ?? int.MaxValue);
            return byPosition != 0 ? byPosition : string.CompareOrdinal(Function, other.Function);
        }
    }

    // A description of a type source — an object or an enumeration type — with its source and its place in the order
    // of the calls. Two declarations are ordered by the source text, ordinal, then by the position in the JSON text,
    // then by the order of the calls, so a message does not depend on that order but for one source.
    private sealed record TypeDeclaration(object Description, TypeSource Source, int Order)
    {
        internal CelSourceLocation? Position => Source.PositionOf(Description);

        internal static int Compare(TypeDeclaration? left, TypeDeclaration? right)
        {
            int byText = string.CompareOrdinal(left!.Source.Text, right!.Source.Text);
            if (byText != 0)
            {
                return byText;
            }

            int byPosition = (left.Position?.Offset ?? int.MaxValue).CompareTo(right.Position?.Offset ?? int.MaxValue);
            return byPosition != 0 ? byPosition : left.Order.CompareTo(right.Order);
        }
    }
}
