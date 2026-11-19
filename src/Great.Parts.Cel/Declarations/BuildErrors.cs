using Great.Parts.Cel.Diagnostics;

namespace Great.Parts.Cel.Declarations;

// The build errors of declarations, with the messages of the error code table. A build error has the
// stage Build and no place in an expression, so no fragment either. An error that names a declaration of an environment
// description in JSON carries the position of that declaration in the JSON text, and so do MissingImplementation,
// ImplementationSignatureMismatch and TypeNotDeclared, whose text names no source, for the overload, or the variable, overload or
// field, from the JSON. The subject of each is the name that the column "Parts" of the error table gives for its code; a build
// error has no CEL types.
internal static class BuildErrors
{
    internal static CelError DuplicateVariable(string name, string source1, string source2, CelSourceLocation? sourceLocation = null) =>
        Create(CelErrorCode.DuplicateVariable, "variable '" + name + "' is declared by both " + source1 + " and " + source2, name, sourceLocation);

    internal static CelError OverlappingOverloads(
        string overload1,
        string source1,
        string overload2,
        string source2,
        string function,
        CelSourceLocation? sourceLocation = null) =>
        Create(
            CelErrorCode.OverlappingOverloads,
            "overload '" + overload1 + "' of " + source1 + " overlaps overload '" + overload2 + "' of " + source2 + " of function '" + function + "'",
            function,
            sourceLocation);

    // {kind} is variable, function or overload; {version} is v1.
    internal static CelError StandardLibraryConflict(string kind, string name, string source, string version, string detail, CelSourceLocation? sourceLocation = null) =>
        Create(
            CelErrorCode.StandardLibraryConflict,
            kind + " '" + name + "' of " + source + " conflicts with standard library " + version + ": " + detail,
            name,
            sourceLocation);

    internal static CelError MissingImplementation(string overloadId, string function, CelSourceLocation? sourceLocation = null) =>
        Create(CelErrorCode.MissingImplementation, "overload '" + overloadId + "' of function '" + function + "' has no implementation", overloadId, sourceLocation);

    // {declarationKind} is variable, overload, field or input; the hint is empty, or names the extension set that declares the type
    // (from the part of the types of time, optional and decimal on, those too). The subject is the type: the full name of a .NET type
    // or the name of a CEL type.
    internal static CelError TypeNotDeclared(string declarationKind, string name, string type, CelSourceLocation? sourceLocation = null, string hint = "") =>
        Create(
            CelErrorCode.TypeNotDeclared,
            declarationKind + " '" + name + "' has type '" + type + "', which no type source or extension of the environment declares" + hint,
            type,
            sourceLocation);

    internal static CelError DuplicateType(string name, string source1, string source2, CelSourceLocation? sourceLocation) =>
        Create(CelErrorCode.DuplicateType, "type '" + name + "' is declared by both " + source1 + " and " + source2, name, sourceLocation);

    internal static CelError UnusedImplementation(string overloadId) =>
        Create(CelErrorCode.UnusedImplementation, "implementation '" + overloadId + "' has no matching overload declaration", overloadId);

    internal static CelError ImplementationSignatureMismatch(string overloadId, string signature, CelSourceLocation? sourceLocation = null) =>
        Create(
            CelErrorCode.ImplementationSignatureMismatch,
            "implementation '" + overloadId + "' does not match the signature " + signature + " of the overload",
            overloadId,
            sourceLocation);

    internal static CelError DuplicateOverloadId(string overloadId, string source1, string source2, CelSourceLocation? sourceLocation = null) =>
        Create(CelErrorCode.DuplicateOverloadId, "overload id '" + overloadId + "' is used by both " + source1 + " and " + source2, overloadId, sourceLocation);

    internal static CelError DuplicateImplementation(string overloadId) =>
        Create(CelErrorCode.DuplicateImplementation, "overload id '" + overloadId + "' has more than one implementation", overloadId);

    internal static CelError InvalidFunctionName(string name, string source, CelSourceLocation? sourceLocation = null) =>
        Create(
            CelErrorCode.InvalidFunctionName,
            "function name '" + name + "' of " + source + " is not an identifier or a qualified name; operators and internal functions cannot be declared",
            name,
            sourceLocation);

    internal static CelError InvalidVariableName(string name, string source, CelSourceLocation? sourceLocation = null) =>
        Create(CelErrorCode.InvalidVariableName, "variable name '" + name + "' of " + source + " is not an identifier or a qualified name", name, sourceLocation);

    internal static CelError ContainerConflict(string container1, string source1, string container2, string source2, CelSourceLocation? sourceLocation = null) =>
        Create(
            CelErrorCode.ContainerConflict,
            "name resolution container '" + container1 + "' of " + source1 + " conflicts with container '" + container2 + "' of " + source2,
            null,
            sourceLocation);

    // {versions} are the numbers of the catalog in ascending order, written for the message.
    internal static CelError UnknownExtensionVersion(string name, string version, string versions, CelSourceLocation? sourceLocation = null) =>
        Create(
            CelErrorCode.UnknownExtensionVersion,
            "extension '" + name + "' has no version " + version + "; available versions: " + versions,
            name,
            sourceLocation);

    internal static CelError ExtensionVersionConflict(string name, string version1, string version2, CelSourceLocation? sourceLocation = null) =>
        Create(CelErrorCode.ExtensionVersionConflict, "extension '" + name + "' is added in versions " + version1 + " and " + version2, name, sourceLocation);

    internal static CelError DuplicateExtension(string name, string version, string source1, string source2, CelSourceLocation? sourceLocation = null) =>
        Create(
            CelErrorCode.DuplicateExtension,
            "extension '" + name + "' version " + version + " is added as two different sets: " + source1 + " and " + source2,
            name,
            sourceLocation);

    // {dependent} is the label of the set that asks, such as extension strings v5; the subject is the set that is missing.
    internal static CelError MissingExtensionDependency(string dependent, string name, CelSourceLocation? sourceLocation = null) =>
        Create(
            CelErrorCode.MissingExtensionDependency,
            dependent + " requires extension '" + name + "', which is not added to the environment",
            name,
            sourceLocation);

    // A function of the application with the form of a macro that a set of the environment brings.
    internal static CelError ExtensionMacroConflict(string function, string source, string extension, string macro, CelSourceLocation? sourceLocation = null) =>
        Create(
            CelErrorCode.StandardLibraryConflict,
            "function '" + function + "' of " + source + " conflicts with " + extension + ": it has the form of the macro '" + macro + "'",
            function,
            sourceLocation);

    // The two types are the full names of the .NET types in the order of string.CompareOrdinal; the subject is the first.
    internal static CelError InputConflict(string type1, string type2) =>
        Create(CelErrorCode.InputConflict, "the environment input is declared as both '" + type1 + "' and '" + type2 + "'", type1);

    internal static CelError TimeZoneDatabaseConflict() =>
        Create(CelErrorCode.TimeZoneDatabaseConflict, "a different time zone database is already added to the environment", null);

    private static CelError Create(CelErrorCode code, string message, string? subject, CelSourceLocation? sourceLocation = null) =>
        new(code, CelErrorStage.Build, message, default, sourceLocation, null, 0, subject);
}
