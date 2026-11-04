using Great.Parts.Cel.Diagnostics;

namespace Great.Parts.Cel.Declarations;

// The build errors of declarations, with the messages of the error code table. A build error has the
// stage Build and no place in an expression.
internal static class BuildErrors
{
    internal static CelError DuplicateVariable(string name, string source1, string source2) =>
        Create(CelErrorCode.DuplicateVariable, "variable '" + name + "' is declared by both " + source1 + " and " + source2);

    internal static CelError OverlappingOverloads(string overload1, string source1, string overload2, string source2, string function) =>
        Create(
            CelErrorCode.OverlappingOverloads,
            "overload '" + overload1 + "' of " + source1 + " overlaps overload '" + overload2 + "' of " + source2 + " of function '" + function + "'");

    // {kind} is variable, function or overload; {version} is v1.
    internal static CelError StandardLibraryConflict(string kind, string name, string source, string version, string detail) =>
        Create(CelErrorCode.StandardLibraryConflict, kind + " '" + name + "' of " + source + " conflicts with standard library " + version + ": " + detail);

    internal static CelError MissingImplementation(string overloadId, string function) =>
        Create(CelErrorCode.MissingImplementation, "overload '" + overloadId + "' of function '" + function + "' has no implementation");

    // {declarationKind} is variable or overload; the hint is empty until the types of time, optional and decimal are supported.
    internal static CelError TypeNotDeclared(string declarationKind, string name, string type) =>
        Create(
            CelErrorCode.TypeNotDeclared,
            declarationKind + " '" + name + "' has type '" + type + "', which no type source or extension of the environment declares");

    internal static CelError UnusedImplementation(string overloadId) =>
        Create(CelErrorCode.UnusedImplementation, "implementation '" + overloadId + "' has no matching overload declaration");

    internal static CelError ImplementationSignatureMismatch(string overloadId, string signature) =>
        Create(CelErrorCode.ImplementationSignatureMismatch, "implementation '" + overloadId + "' does not match the signature " + signature + " of the overload");

    internal static CelError DuplicateOverloadId(string overloadId, string source1, string source2) =>
        Create(CelErrorCode.DuplicateOverloadId, "overload id '" + overloadId + "' is used by both " + source1 + " and " + source2);

    internal static CelError DuplicateImplementation(string overloadId) =>
        Create(CelErrorCode.DuplicateImplementation, "overload id '" + overloadId + "' has more than one implementation");

    internal static CelError InvalidFunctionName(string name, string source) =>
        Create(
            CelErrorCode.InvalidFunctionName,
            "function name '" + name + "' of " + source + " is not an identifier or a qualified name; operators and internal functions cannot be declared");

    internal static CelError InvalidVariableName(string name, string source) =>
        Create(CelErrorCode.InvalidVariableName, "variable name '" + name + "' of " + source + " is not an identifier or a qualified name");

    internal static CelError ContainerConflict(string container1, string source1, string container2, string source2) =>
        Create(
            CelErrorCode.ContainerConflict,
            "name resolution container '" + container1 + "' of " + source1 + " conflicts with container '" + container2 + "' of " + source2);

    private static CelError Create(CelErrorCode code, string message) =>
        new(code, CelErrorStage.Build, message, default, null, null);
}
