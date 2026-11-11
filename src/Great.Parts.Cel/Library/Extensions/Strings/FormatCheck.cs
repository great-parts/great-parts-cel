using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Types;

namespace Great.Parts.Cel.Library.Extensions.Strings;

// The check at compilation of a call of format, the validator of the overload string_format. It reads the format string with the
// parser of evaluation and looks, in place of a value, at the static type of the element of the list literal that each clause takes,
// by the same table of letters and kinds. Only a call whose receiver is a string literal and whose argument is a list literal
// without optional elements is checked; anything else is left to evaluation. The first error ends the check.
internal static class FormatCheck
{
    internal static CallValidation Validate(CallShape call)
    {
        if (call.Receiver is not { StringConstant: { } format }
            || call.Arguments.Length != 1
            || call.Arguments[0] is not { Elements: { } elements, HasOptionalElements: false })
        {
            return CallValidation.Ok;
        }

        var parser = new FormatParser(format, elements.Length);
        while (true)
        {
            switch (parser.Next())
            {
                case FormatStep.End:
                    return elements.Length > parser.ClauseCount
                        ? Invalid(FormatRules.TooManyArguments(parser.ClauseCount, elements.Length), null)
                        : CallValidation.Ok;
                case FormatStep.Failure:
                    return Invalid(FormatRules.Detail(parser.Failure.Kind, parser.Failure.Number, null), null);
                case FormatStep.Clause:
                    CallValidation answer = CheckElement(parser.Clause.Specifier, elements[parser.Clause.Index]);
                    if (!answer.IsOk)
                    {
                        return answer;
                    }

                    break;
            }
        }
    }

    // The element against the letter. %s also looks into a list or a map literal, in order, at each element, key and value.
    private static CallValidation CheckElement(FormatSpecifier specifier, ValueShape element)
    {
        ValueShape? unfit = specifier == FormatSpecifier.String ? FirstUnfitForText(element) : Fits(specifier, element) ? null : element;
        return unfit is { } part
            ? Invalid(FormatRules.Detail(FormatRules.KindFailure(specifier), 0, TypeName(part.Type)), part.Offset)
            : CallValidation.Ok;
    }

    // The first part of the literal, outermost and leftmost, that %s does not take.
    private static ValueShape? FirstUnfitForText(ValueShape element)
    {
        if (!Fits(FormatSpecifier.String, element))
        {
            return element;
        }

        if (element.Elements is { } elements)
        {
            foreach (ValueShape item in elements)
            {
                if (FirstUnfitForText(item) is { } unfit)
                {
                    return unfit;
                }
            }
        }

        if (element.Entries is { } entries)
        {
            foreach ((ValueShape key, ValueShape value) in entries)
            {
                if ((FirstUnfitForText(key) ?? FirstUnfitForText(value)) is { } unfit)
                {
                    return unfit;
                }
            }
        }

        return null;
    }

    // Whether the static type of the part fits the letter: dyn fits every letter, and the other types by the kind of the table.
    private static bool Fits(FormatSpecifier specifier, ValueShape part) =>
        part.Type.Kind == CelTypeKind.Dyn || FormatRules.Accepts(specifier, FormatRules.KindOf(part.Type));

    // The name of the static type in the message; a wrapper is named by the primitive it wraps.
    private static string TypeName(CelType type) => type.Kind == CelTypeKind.Wrapper ? type.Parameters[0].Name : type.Name;

    private static CallValidation Invalid(string detail, int? offset) =>
        new(CelErrorCode.InvalidFormatString, detail, offset);
}
