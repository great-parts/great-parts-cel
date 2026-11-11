using System.Collections.Immutable;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Types;

namespace Great.Parts.Cel.Library.Extensions.Strings;

// The table of the extension set strings, version 5: the overloads in the order of cel-go, each with its implementation and its cost
// from the cost page, 1 and the part of the arguments that the call node charges before the function runs. The overload string_format
// has its own argument cost and the check of the call at compilation. The identifiers are those of cel-go, letter for letter.
internal static class StringsExtension
{
    internal const string SetName = "strings";
    internal const uint Version = 5;
    private const string Source = "extension strings v5";

    // The definition of the version, or null for a version the set does not have.
    internal static CatalogExtension? Create(uint version) =>
        version == Version ? new CatalogExtension(SetName, Version, Overloads()) : null;

    private static ImmutableArray<OverloadBinding> Overloads()
    {
        CelType @string = CelType.String;
        CelType @int = CelType.Int;
        CelType strings = CelType.ListOf(@string);
        CelType values = CelType.ListOf(CelType.Dyn);
        return
        [
            Member("charAt", "string_char_at_int", [@string, @int], @string, StringFunctions.CharAt),
            Member("indexOf", "string_index_of_string", [@string, @string], @int, StringFunctions.IndexOfString),
            Member("indexOf", "string_index_of_string_int", [@string, @string, @int], @int, StringFunctions.IndexOfStringInt),
            Member("lastIndexOf", "string_last_index_of_string", [@string, @string], @int, StringFunctions.LastIndexOfString),
            Member("lastIndexOf", "string_last_index_of_string_int", [@string, @string, @int], @int, StringFunctions.LastIndexOfStringInt),
            Member("lowerAscii", "string_lower_ascii", [@string], @string, StringFunctions.LowerAscii),
            Member("replace", "string_replace_string_string", [@string, @string, @string], @string, StringFunctions.ReplaceAll),
            Member("replace", "string_replace_string_string_int", [@string, @string, @string, @int], @string, StringFunctions.ReplaceLimited),
            Member("split", "string_split_string", [@string, @string], strings, StringFunctions.SplitAll),
            Member("split", "string_split_string_int", [@string, @string, @int], strings, StringFunctions.SplitLimited),
            Member("substring", "string_substring_int", [@string, @int], @string, StringFunctions.SubstringFrom),
            Member("substring", "string_substring_int_int", [@string, @int, @int], @string, StringFunctions.SubstringRange),
            Member("trim", "string_trim", [@string], @string, StringFunctions.Trim),
            Member("upperAscii", "string_upper_ascii", [@string], @string, StringFunctions.UpperAscii),
            new OverloadBinding("join", new CelOverloadDecl("list_join", true, [strings], @string), StringFunctions.Join, 1, StringFunctions.JoinArgumentCost, Source),
            new OverloadBinding("join", new CelOverloadDecl("list_join_string", true, [strings, @string], @string), StringFunctions.Join, 1, StringFunctions.JoinArgumentCost, Source),
            new OverloadBinding(
                "format",
                new CelOverloadDecl("string_format", true, [@string, values], @string),
                StringFunctions.Format,
                1,
                StringFunctions.FormatArgumentCost,
                Source,
                validator: FormatCheck.Validate),
            new OverloadBinding("strings.quote", new CelOverloadDecl("strings_quote", false, [@string], @string), StringFunctions.Quote, 1, Costs.Text, Source),
            Member("reverse", "string_reverse", [@string], @string, StringFunctions.Reverse),
        ];
    }

    // An overload called on a string, the first parameter, that passes over its strings: 1 + ⌈Lₐ/10⌉ before the function runs.
    private static OverloadBinding Member(
        string function,
        string overloadId,
        ImmutableArray<CelType> parameterTypes,
        CelType resultType,
        OverloadImplementation implementation) =>
        new(function, new CelOverloadDecl(overloadId, true, parameterTypes, resultType), implementation, 1, Costs.Text, Source);
}
