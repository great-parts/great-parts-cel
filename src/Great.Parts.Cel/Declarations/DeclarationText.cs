using System;
using System.Collections.Immutable;
using System.Text;
using Great.Parts.Cel.Types;

namespace Great.Parts.Cel.Declarations;

// The texts that declarations produce: the overload identifier that the delegate forms of the builder
// derive, and the signature of an overload in build errors.
internal static class DeclarationText
{
    // <name>_<type1>_…_<typeN>, each type written as in an environment description in JSON without spaces;
    // the name alone without parameters.
    internal static string DerivedOverloadId(string function, ReadOnlySpan<string> parameterTypes)
    {
        var id = new StringBuilder(function);
        foreach (string parameterType in parameterTypes)
        {
            id.Append('_').Append(parameterType);
        }

        return id.ToString();
    }

    // The JSON notation of a type: int_wrapper, list<int>, map<string,dyn>, type<int>, ~A.
    internal static string Json(CelType type)
    {
        var text = new StringBuilder();
        AppendJson(text, type);
        return text.ToString();
    }

    // The signature in the notation of the language definition: isInn(string) -> bool, string.isInn() -> bool.
    internal static string Signature(string function, CelOverloadDecl overload)
    {
        ImmutableArray<CelType> parameters = overload.ParameterTypes;
        var text = new StringBuilder();
        int first = 0;
        if (overload.IsReceiverStyle)
        {
            text.Append(parameters[0]).Append('.');
            first = 1;
        }

        text.Append(function).Append('(');
        for (int i = first; i < parameters.Length; i++)
        {
            if (i > first)
            {
                text.Append(", ");
            }

            text.Append(parameters[i]);
        }

        return text.Append(") -> ").Append(overload.ResultType).ToString();
    }

    private static void AppendJson(StringBuilder text, CelType type)
    {
        switch (type.Kind)
        {
            case CelTypeKind.Wrapper:
                text.Append(type.Parameters[0].Name).Append("_wrapper");
                break;
            case CelTypeKind.TypeParameter:
                text.Append('~').Append(type.Name);
                break;
            case CelTypeKind.List or CelTypeKind.Map or CelTypeKind.Type:
                text.Append(type.Name).Append('<');
                for (int i = 0; i < type.Parameters.Length; i++)
                {
                    if (i > 0)
                    {
                        text.Append(',');
                    }

                    AppendJson(text, type.Parameters[i]);
                }

                text.Append('>');
                break;
            default:
                text.Append(type.Name);
                break;
        }
    }
}
