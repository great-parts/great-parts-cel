using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Types;
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

    internal static AsyncOverloadImplementation OfAsync(CelAsyncFunctionImplementation implementation) =>
        (arguments, _, cancellationToken) => implementation(arguments, cancellationToken);

    internal static AsyncOverloadImplementation OfAsync<TResult>(Func<CancellationToken, ValueTask<TResult>> implementation) =>
        (_, site, cancellationToken) => Complete(implementation(cancellationToken), site.Function, site);

    internal static AsyncOverloadImplementation OfAsync<T1, TResult>(string overloadId, Func<T1, CancellationToken, ValueTask<TResult>> implementation) =>
        (arguments, site, cancellationToken) =>
            TryArgument(arguments.AsSpan(), 0, overloadId, in site, out T1 first, out CelValue error)
                ? Complete(implementation(first, cancellationToken), overloadId, site)
                : new ValueTask<CelValue>(error);

    internal static AsyncOverloadImplementation OfAsync<T1, T2, TResult>(string overloadId, Func<T1, T2, CancellationToken, ValueTask<TResult>> implementation) =>
        (arguments, site, cancellationToken) =>
            TryArgument(arguments.AsSpan(), 0, overloadId, in site, out T1 first, out CelValue error)
            && TryArgument(arguments.AsSpan(), 1, overloadId, in site, out T2 second, out error)
                ? Complete(implementation(first, second, cancellationToken), overloadId, site)
                : new ValueTask<CelValue>(error);

    internal static AsyncOverloadImplementation OfAsync<T1, T2, T3, TResult>(string overloadId, Func<T1, T2, T3, CancellationToken, ValueTask<TResult>> implementation) =>
        (arguments, site, cancellationToken) =>
            TryArgument(arguments.AsSpan(), 0, overloadId, in site, out T1 first, out CelValue error)
            && TryArgument(arguments.AsSpan(), 1, overloadId, in site, out T2 second, out error)
            && TryArgument(arguments.AsSpan(), 2, overloadId, in site, out T3 third, out error)
                ? Complete(implementation(first, second, third, cancellationToken), overloadId, site)
                : new ValueTask<CelValue>(error);

    internal static AsyncOverloadImplementation OfAsync<T1, T2, T3, T4, TResult>(string overloadId, Func<T1, T2, T3, T4, CancellationToken, ValueTask<TResult>> implementation) =>
        (arguments, site, cancellationToken) =>
            TryArgument(arguments.AsSpan(), 0, overloadId, in site, out T1 first, out CelValue error)
            && TryArgument(arguments.AsSpan(), 1, overloadId, in site, out T2 second, out error)
            && TryArgument(arguments.AsSpan(), 2, overloadId, in site, out T3 third, out error)
            && TryArgument(arguments.AsSpan(), 3, overloadId, in site, out T4 fourth, out error)
                ? Complete(implementation(first, second, third, fourth, cancellationToken), overloadId, site)
                : new ValueTask<CelValue>(error);

    // The result of a task that already succeeded is converted at once, without a state machine; otherwise the conversion waits for the task.
    private static ValueTask<CelValue> Complete<TResult>(ValueTask<TResult> task, string overloadId, CallSite site) =>
        task.IsCompletedSuccessfully
            ? new ValueTask<CelValue>(Result(task.GetAwaiter().GetResult(), overloadId, in site))
            : CompleteLater(task, overloadId, site);

    private static async ValueTask<CelValue> CompleteLater<TResult>(ValueTask<TResult> task, string overloadId, CallSite site) =>
        Result(await task.ConfigureAwait(false), overloadId, in site);

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
                ObjectArgumentText,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"argument '{index + 1}' of overload '{overloadId}' of function '{site.Function}' cannot be converted from '{ObjectRules.TypeName(argument, site.Types)}' to .NET type '{typeof(T)}'"),
                new ArgumentRefusal(overloadId, ValueTypeRules.TypeOfValue(argument, site.Types)))
            : site.Error(CelErrorCode.ArgumentConversion, ArgumentConversionText<T>.Format, overloadId, site.Function, index + 1, (byte)argument.Kind);
        return false;
    }

    // The build checks that the result type is an input type, so the conversion always has a row.
    // A set is sorted and a collection without an index copied, charged to the meter of the call; an error value that the
    // conversion makes — a set whose elements CEL cannot sort, an undefined JSON element — has the place of the call.
    private static CelValue Result<T>(T value, string overloadId, in CallSite site) =>
        site.Placed(ConversionRules.FromResult(value, overloadId, site.Meter, site.Function));

    // The message of ArgumentConversion to T: the overload identifier is the first part, the function name the
    // second, the number of the argument the number and the kind of its value the byte.
    private static class ArgumentConversionText<T>
    {
        internal static ErrorTemplate Format { get; } = new(
            static error => string.Create(
                CultureInfo.InvariantCulture,
                $"argument '{error.Number}' of overload '{(string)error.First!}' of function '{(string)error.Second!}' cannot be converted from '{ValueTypeNames.Of(ErrorValueParts.KindOf(error))}' to .NET type '{typeof(T)}'"),
            subject: static error => (string)error.First!,
            actualType: static error => ValueTypeRules.TypeOfKind(ErrorValueParts.KindOf(error), null));
    }

    // The overload and the type of the value that an argument is refused with, for the error whose message is its first part: the type
    // of an object argument is its description or its .NET type, which the kind alone does not name.
    private sealed record ArgumentRefusal(string OverloadId, CelType ActualType);

    private static readonly ErrorTemplate ObjectArgumentText = new(
        static error => (string)error.First!,
        subject: static error => ((ArgumentRefusal)error.Second!).OverloadId,
        actualType: static error => ((ArgumentRefusal)error.Second!).ActualType);
}
