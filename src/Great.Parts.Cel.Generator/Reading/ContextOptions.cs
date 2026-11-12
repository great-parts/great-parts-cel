using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace Great.Parts.Cel.Generator;

// The options of a context, from [CelTypeContextOptions] on the class: the naming policy (the numeric value of CelNamingPolicy)
// and whether the attributes of System.Text.Json are ignored. The defaults are the policy CamelCase and false.
internal readonly record struct ContextOptions(int NamingPolicy, bool IgnoreJson)
{
    internal static ContextOptions Read(INamedTypeSymbol context)
    {
        AttributeData? attribute = Attributes.Find(context, WellKnown.CelTypeContextOptionsAttribute);
        int policy = 0;
        bool ignoreJson = false;
        if (attribute is not null)
        {
            foreach (KeyValuePair<string, TypedConstant> argument in attribute.NamedArguments)
            {
                if (argument.Key == "NamingPolicy" && argument.Value.Value is int value)
                {
                    policy = value;
                }
                else if (argument.Key == "IgnoreJsonAttributes" && argument.Value.Value is bool flag)
                {
                    ignoreJson = flag;
                }
            }
        }

        return new ContextOptions(policy, ignoreJson);
    }
}
