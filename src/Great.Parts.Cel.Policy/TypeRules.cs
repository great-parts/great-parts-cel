using Great.Parts.Cel.Types;

namespace Great.Parts.Cel.Policy;

// The assignability of types that the checks of a policy use, as IsAssignableType of cel-go has it: a type that is dyn, any or a type
// parameter accepts any type; a wrapper accepts null and the type it wraps; otherwise the types must have the same kind, name and
// number of parameters, and each parameter of the first must accept the one of the second.
internal static class TypeRules
{
    // Whether a value of the type "from" can be used where the type "target" is wanted.
    internal static bool IsAssignable(CelType target, CelType from)
    {
        if (ReferenceEquals(target, from))
        {
            return true;
        }

        if (target.Kind is CelTypeKind.Dyn or CelTypeKind.Any or CelTypeKind.TypeParameter)
        {
            return true;
        }

        if (target.Kind == CelTypeKind.Wrapper && from.Kind == CelTypeKind.Null)
        {
            return true;
        }

        CelType wanted = Unwrapped(target);
        CelType given = Unwrapped(from);
        if (wanted.Kind != given.Kind
            || !string.Equals(wanted.Name, given.Name, System.StringComparison.Ordinal)
            || wanted.Parameters.Length != given.Parameters.Length)
        {
            return false;
        }

        for (int i = 0; i < wanted.Parameters.Length; i++)
        {
            if (!IsAssignable(wanted.Parameters[i], given.Parameters[i]))
            {
                return false;
            }
        }

        return true;
    }

    // A wrapper of a primitive type has the kind, the name and the parameters of the primitive in cel-go.
    private static CelType Unwrapped(CelType type) => type.Kind == CelTypeKind.Wrapper ? type.Parameters[0] : type;
}
