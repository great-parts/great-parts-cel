namespace Great.Parts.Cel.Types;

// Assignability of CEL types for overload candidates. Type parameters and their substitution come with
// the complete type checking; until then a type is assignable to another when they are equal or when
// either of them is dyn.
internal static class AssignabilityRules
{
    internal static bool IsAssignable(CelType from, CelType to) =>
        from.Kind == CelTypeKind.Dyn || to.Kind == CelTypeKind.Dyn || from.Equals(to);
}
