using Great.Parts.Cel.Diagnostics;

namespace Great.Parts.Cel.Declarations;

// The build errors of declarations, with the messages of the error code table. A build error has the
// stage Build and no place in an expression. An error that names a declaration of an environment description in JSON
// carries the position of that declaration in the JSON text, and so do MissingImplementation, ImplementationSignatureMismatch
// and TypeNotDeclared, whose text names no source, for the overload, or the variable, overload or field, from the JSON.
internal static class BuildErrors
{
    internal static CelError DuplicateVariable(string name, string source1, string source2, CelSourceLocation? sourceLocation = null) =>
        Create(CelErrorCode.DuplicateVariable, "variable '" + name + "' is declared by both " + source1 + " and " + source2, sourceLocation);

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
            sourceLocation);

    // {kind} is variable, function or overload; {version} is v1.
    internal static CelError StandardLibraryConflict(string kind, string name, string source, string version, string detail, CelSourceLocation? sourceLocation = null) =>
        Create(
            CelErrorCode.StandardLibraryConflict,
            kind + " '" + name + "' of " + source + " conflicts with standard library " + version + ": " + detail,
            sourceLocation);

    internal static CelError MissingImplementation(string overloadId, string function, CelSourceLocation? sourceLocation = null) =>
        Create(CelErrorCode.MissingImplementation, "overload '" + overloadId + "' of function '" + function + "' has no implementation", sourceLocation);

    // {declarationKind} is variable, overload or field; the hint is empty, or names the extension set that declares the type
    // (from the part of the types of time, optional and decimal on, those too).
    internal static CelError TypeNotDeclared(string declarationKind, string name, string type, CelSourceLocation? sourceLocation = null, string hint = "") =>
        Create(
            CelErrorCode.TypeNotDeclared,
            declarationKind + " '" + name + "' has type '" + type + "', which no type source or extension of the environment declares" + hint,
            sourceLocation);

    internal static CelError DuplicateType(string name, string source1, string source2, CelSourceLocation? sourceLocation) =>
        Create(CelErrorCode.DuplicateType, "type '" + name + "' is declared by both " + source1 + " and " + source2, sourceLocation);

    internal static CelError UnusedImplementation(string overloadId) =>
        Create(CelErrorCode.UnusedImplementation, "implementation '" + overloadId + "' has no matching overload declaration");

    internal static CelError ImplementationSignatureMismatch(string overloadId, string signature, CelSourceLocation? sourceLocation = null) =>
        Create(
            CelErrorCode.ImplementationSignatureMismatch,
            "implementation '" + overloadId + "' does not match the signature " + signature + " of the overload",
            sourceLocation);

    internal static CelError DuplicateOverloadId(string overloadId, string source1, string source2, CelSourceLocation? sourceLocation = null) =>
        Create(CelErrorCode.DuplicateOverloadId, "overload id '" + overloadId + "' is used by both " + source1 + " and " + source2, sourceLocation);

    internal static CelError DuplicateImplementation(string overloadId) =>
        Create(CelErrorCode.DuplicateImplementation, "overload id '" + overloadId + "' has more than one implementation");

    internal static CelError InvalidFunctionName(string name, string source, CelSourceLocation? sourceLocation = null) =>
        Create(
            CelErrorCode.InvalidFunctionName,
            "function name '" + name + "' of " + source + " is not an identifier or a qualified name; operators and internal functions cannot be declared",
            sourceLocation);

    internal static CelError InvalidVariableName(string name, string source, CelSourceLocation? sourceLocation = null) =>
        Create(CelErrorCode.InvalidVariableName, "variable name '" + name + "' of " + source + " is not an identifier or a qualified name", sourceLocation);

    internal static CelError ContainerConflict(string container1, string source1, string container2, string source2, CelSourceLocation? sourceLocation = null) =>
        Create(
            CelErrorCode.ContainerConflict,
            "name resolution container '" + container1 + "' of " + source1 + " conflicts with container '" + container2 + "' of " + source2,
            sourceLocation);

    // {versions} are the numbers of the catalog in ascending order, written for the message.
    internal static CelError UnknownExtensionVersion(string name, string version, string versions, CelSourceLocation? sourceLocation = null) =>
        Create(
            CelErrorCode.UnknownExtensionVersion,
            "extension '" + name + "' has no version " + version + "; available versions: " + versions,
            sourceLocation);

    internal static CelError ExtensionVersionConflict(string name, string version1, string version2, CelSourceLocation? sourceLocation = null) =>
        Create(CelErrorCode.ExtensionVersionConflict, "extension '" + name + "' is added in versions " + version1 + " and " + version2, sourceLocation);

    internal static CelError DuplicateExtension(string name, string version, string source1, string source2, CelSourceLocation? sourceLocation = null) =>
        Create(
            CelErrorCode.DuplicateExtension,
            "extension '" + name + "' version " + version + " is added as two different sets: " + source1 + " and " + source2,
            sourceLocation);

    // {dependent} is the label of the set that asks, such as extension strings v5.
    internal static CelError MissingExtensionDependency(string dependent, string name, CelSourceLocation? sourceLocation = null) =>
        Create(
            CelErrorCode.MissingExtensionDependency,
            dependent + " requires extension '" + name + "', which is not added to the environment",
            sourceLocation);

    // A function of the application with the form of a macro that a set of the environment brings.
    internal static CelError ExtensionMacroConflict(string function, string source, string extension, string macro, CelSourceLocation? sourceLocation = null) =>
        Create(
            CelErrorCode.StandardLibraryConflict,
            "function '" + function + "' of " + source + " conflicts with " + extension + ": it has the form of the macro '" + macro + "'",
            sourceLocation);
    internal static CelError TimeZoneDatabaseConflict() =>
        Create(CelErrorCode.TimeZoneDatabaseConflict, "a different time zone database is already added to the environment");

    private static CelError Create(CelErrorCode code, string message, CelSourceLocation? sourceLocation = null) =>
        new(code, CelErrorStage.Build, message, default, sourceLocation, null);
}
