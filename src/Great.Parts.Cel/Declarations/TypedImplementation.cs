using System;
using System.Globalization;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Declarations;

// The implementations that the builder makes of the delegates of the application. A typed delegate gets
// its arguments converted by the result rows of the type mapping and returns its result by the input rows;
// an argument that does not convert is ArgumentConversion at the call. Evaluation adds the exception
// boundary, the cost and the check of the result around the call.
internal static class TypedImplementation
{
    internal static OverloadImplementation Of(CelFunctionImplementation implementation) =>
        (ReadOnlySpan<CelValue> arguments, in CallSite _) => implementation(arguments);

    internal static OverloadImplementation Of<TResult>(Func<TResult> implementation) =>
        (ReadOnlySpan<CelValue> _, in CallSite site) => Result(implementation(), site.Function, site);

    internal static OverloadImplementation Of<T1, TResult>(string overloadId, Func<T1, TResult> implementation) =>
        (ReadOnlySpan<CelValue> arguments, in CallSite site) =>
            TryArgument(arguments, 0, overloadId, site, out T1 first, out CelValue error)
                ? Result(implementation(first), overloadId, site)
                : error;

    internal static OverloadImplementation Of<T1, T2, TResult>(string overloadId, Func<T1, T2, TResult> implementation) =>
        (ReadOnlySpan<CelValue> arguments, in CallSite site) =>
            TryArgument(arguments, 0, overloadId, site, out T1 first, out CelValue error)
            && TryArgument(arguments, 1, overloadId, site, out T2 second, out error)
                ? Result(implementation(first, second), overloadId, site)
                : error;

    internal static OverloadImplementation Of<T1, T2, T3, TResult>(string overloadId, Func<T1, T2, T3, TResult> implementation) =>
        (ReadOnlySpan<CelValue> arguments, in CallSite site) =>
            TryArgument(arguments, 0, overloadId, site, out T1 first, out CelValue error)
            && TryArgument(arguments, 1, overloadId, site, out T2 second, out error)
            && TryArgument(arguments, 2, overloadId, site, out T3 third, out error)
                ? Result(implementation(first, second, third), overloadId, site)
                : error;

    internal static OverloadImplementation Of<T1, T2, T3, T4, TResult>(string overloadId, Func<T1, T2, T3, T4, TResult> implementation) =>
        (ReadOnlySpan<CelValue> arguments, in CallSite site) =>
            TryArgument(arguments, 0, overloadId, site, out T1 first, out CelValue error)
            && TryArgument(arguments, 1, overloadId, site, out T2 second, out error)
            && TryArgument(arguments, 2, overloadId, site, out T3 third, out error)
            && TryArgument(arguments, 3, overloadId, site, out T4 fourth, out error)
                ? Result(implementation(first, second, third, fourth), overloadId, site)
                : error;

    // The argument at the index converted to T, or the error ArgumentConversion at the call. A CEL null
    // reaches the delegate only through a parameter whose declared type takes it, such as dyn as object. A
    // collection is a lazy view, or a copy charged to the meter of the call.
    private static bool TryArgument<T>(
        ReadOnlySpan<CelValue> arguments,
        int index,
        string overloadId,
        in CallSite site,
        out T value,
        out CelValue error)
    {
        CelValue argument = arguments[index];
        if (ConversionRules.TryToParameter(argument, site.Meter, out T? converted))
        {
            value = converted!;
            error = default;
            return true;
        }

        // Not read when the method returns false.
        value = default!;
        // An object argument is named by its description or its .NET type, resolved now: only an object that no declared
        // parameter type took gets here.
        error = argument.Kind == CelValueKind.Object
            ? site.Error(
                CelErrorCode.ArgumentConversion,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"argument '{index + 1}' of overload '{overloadId}' of function '{site.Function}' cannot be converted from '{ObjectRules.TypeName(argument, site.Types)}' to .NET type '{typeof(T)}'"))
            : site.Error(CelErrorCode.ArgumentConversion, ArgumentConversionText<T>.Format, overloadId, site.Function, index + 1, (byte)argument.Kind);
        return false;
    }

    // The build checks that the result type is an input type, so the conversion always has a row.
    // A set is sorted and a collection without an index copied, charged to the meter of the call; an error value that the
    // conversion makes — a set whose elements CEL cannot sort, an undefined JSON element — has the place of the call.
    private static CelValue Result<T>(T value, string overloadId, in CallSite site) =>
        site.Placed(ConversionRules.FromResult(value, overloadId, site.Meter));

    // The message of ArgumentConversion to T: the overload identifier is the first part, the function name the
    // second, the number of the argument the number and the kind of its value the byte.
    private static class ArgumentConversionText<T>
    {
        internal static Func<CelError, string> Format { get; } = static error => string.Create(
            CultureInfo.InvariantCulture,
            $"argument '{error.Number}' of overload '{(string)error.First!}' of function '{(string)error.Second!}' cannot be converted from '{ValueTypeNames.Of(ErrorValueParts.KindOf(error))}' to .NET type '{typeof(T)}'");
    }
}
