namespace Great.Parts.Cel.Generator;

// The names that the file of descriptions of the call sites gives and the registrations and the converters of collections use: the
// class of the file, the hint name, and the static method that converts an element of an object type.
internal static class CallSiteNames
{
    // The class is internal, in the global namespace, and has one file for the assembly.
    internal const string TypesClassName = "CelCallSiteTypes";

    internal const string TypesClass = "global::" + TypesClassName;

    internal const string HintName = TypesClassName + ".g.cs";

    // The method of the type that has the nested class `Type_Acme_Line`: `Convert_Acme_Line`, and `ConvertNotNull_Acme_Line` for a place
    // that does not take null, where a null element is an error value and not the null value.
    internal static string Converter(string className, bool strict) =>
        (strict ? "ConvertNotNull_" : "Convert_") + className.Substring(className.IndexOf('_') + 1);

    // A place of a class or of an interface that its annotation does not allow to be null. A structure has no null.
    internal static bool IsStrict(ObjectShape shape) =>
        shape.Kind != ObjectKind.Struct && shape.Nullability == Nullability.NotNull;
}
