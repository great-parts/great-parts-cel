using System;
using System.Globalization;
using System.Runtime.CompilerServices;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Types;

namespace Great.Parts.Cel.Values;

// The rows of time: DateTimeOffset, DateTime, TimeSpan, CelTimestamp and CelDuration and the Nullable<> of each, as input
// and as result. A typed collection of time is not a row (the closed list of elements has no time).
internal static partial class ConversionRules
{
    // The marker in the second part of an error that a conversion made without a name: the node that reads the value names it.
    internal static readonly object UnnamedRefusal = new();

    private static readonly ErrorTemplate TicksOutOfRangeText = new(
        static error => error.Number.ToString(CultureInfo.InvariantCulture) + " ticks is out of the range of duration",
        expectedType: static _ => CelType.Duration,
        actualType: static _ => CelType.Int);

    /// <summary>
    /// The timestamp of a <see cref="DateTime"/>: its moment when its kind is <see cref="DateTimeKind.Utc"/>; any other kind
    /// is an error value <c>InvalidArgument</c>, since its meaning would depend on the time zone of the process.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="name">What the message names, or <see langword="null"/> when the reader of the value names it.</param>
    /// <returns>A timestamp or the error value, which has no place.</returns>
    internal static CelValue FromDateTime(DateTime value, string? name = null) => value.Kind == DateTimeKind.Utc
        ? CelValue.FromTimestamp(CelTimestamp.FromUtcTicks(value.Ticks))
        : CelValue.FromError(new CelError(
            CelErrorCode.InvalidArgument,
            CelError.FirstPart,
            null,
            0,
            Refusal(name, "a DateTime of the kind " + value.Kind + " is not an instant in UTC"),
            name is null ? UnnamedRefusal : null));

    /// <summary>
    /// The duration of a <see cref="TimeSpan"/>, exactly; a time span outside the range of a duration is an error value
    /// <c>ConversionRange</c> that has no place.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>A duration or the error value.</returns>
    internal static CelValue FromTimeSpan(TimeSpan value) => CelDuration.TryFromTicks(value.Ticks, out CelDuration duration)
        ? CelValue.FromDuration(duration)
        : CelValue.FromError(new CelError(CelErrorCode.ConversionRange, TicksOutOfRangeText, null, 0, number: value.Ticks));

    /// <summary>
    /// Gives a refusal that a conversion made without a name the name of the place that reads the value, as the text of
    /// <c>InvalidArgument</c> writes it: <c>a list of the application</c>, <c>a map of the application</c>. Any other value is
    /// returned as it is.
    /// </summary>
    /// <param name="value">The value an element or a value of a collection of the application converted to.</param>
    /// <param name="callee">The name of the place.</param>
    /// <returns>The value, or the error value with the name.</returns>
    internal static CelValue NameRefusal(CelValue value, string callee)
    {
        if (value.Kind == CelValueKind.Error && value.TryGetError(out CelError? error) && ReferenceEquals(error.Second, UnnamedRefusal))
        {
            return CelValue.FromError(new CelError(
                CelErrorCode.InvalidArgument,
                CelError.FirstPart,
                null,
                0,
                "invalid argument to '" + callee + "': " + (string)error.First!));
        }

        return value;
    }

    /// <summary>
    /// The exception message of <c>CelBindings.Add</c> for a value that has no CEL value: an undefined JSON element, a
    /// <see cref="DateTime"/> that is not an instant in UTC, a lone surrogate as a <see cref="char"/>, or an integer wider than 64
    /// bits out of range.
    /// </summary>
    /// <param name="refused">The error value that the conversion gave.</param>
    /// <returns>The message.</returns>
    internal static string BindingsRefusalMessage(CelValue refused)
    {
        if (!refused.TryGetError(out CelError? error))
        {
            return "The value is an undefined JsonElement, which has no CEL value.";
        }

        if (WideRules.IsOutOfRange(refused))
        {
            return "The value " + error.Message + ".";
        }

        return ReferenceEquals(error.Second, UnnamedRefusal)
            ? "The value is " + (string)error.First! + "."
            : "The value is an undefined JsonElement, which has no CEL value.";
    }

    internal static string Refusal(string? name, string text) => name is null ? text : "invalid argument to '" + name + "': " + text;

    // CelTimestamp, DateTimeOffset, DateTime, CelDuration, TimeSpan by their static type.
    private static bool TryFromTime<T>(T value, out CelValue result)
    {
        if (typeof(T) == typeof(CelTimestamp))
        {
            result = CelValue.FromTimestamp(Unsafe.As<T, CelTimestamp>(ref value));
        }
        else if (typeof(T) == typeof(DateTimeOffset))
        {
            result = CelValue.FromTimestamp(CelTimestamp.FromDateTimeOffset(Unsafe.As<T, DateTimeOffset>(ref value)));
        }
        else if (typeof(T) == typeof(DateTime))
        {
            result = FromDateTime(Unsafe.As<T, DateTime>(ref value));
        }
        else if (typeof(T) == typeof(CelDuration))
        {
            result = CelValue.FromDuration(Unsafe.As<T, CelDuration>(ref value));
        }
        else if (typeof(T) == typeof(TimeSpan))
        {
            result = FromTimeSpan(Unsafe.As<T, TimeSpan>(ref value));
        }
        else
        {
            result = default;
            return false;
        }

        return true;
    }

    // Nullable<X> of time: null, or the value of X.
    private static bool TryFromNullableTime<T>(T value, out CelValue result)
    {
        if (typeof(T) == typeof(DateTimeOffset?))
        {
            return FromNullable(Unsafe.As<T, DateTimeOffset?>(ref value), out result);
        }

        if (typeof(T) == typeof(DateTime?))
        {
            return FromNullable(Unsafe.As<T, DateTime?>(ref value), out result);
        }

        if (typeof(T) == typeof(CelTimestamp?))
        {
            return FromNullable(Unsafe.As<T, CelTimestamp?>(ref value), out result);
        }

        if (typeof(T) == typeof(TimeSpan?))
        {
            return FromNullable(Unsafe.As<T, TimeSpan?>(ref value), out result);
        }

        if (typeof(T) == typeof(CelDuration?))
        {
            return FromNullable(Unsafe.As<T, CelDuration?>(ref value), out result);
        }

        result = default;
        return false;
    }

    // The CEL type of the time types of the input rows, or null for another .NET type.
    private static CelType? TimeInputType(Type type)
    {
        if (type == typeof(CelTimestamp) || type == typeof(DateTimeOffset) || type == typeof(DateTime))
        {
            return CelType.Timestamp;
        }

        return type == typeof(CelDuration) || type == typeof(TimeSpan) ? CelType.Duration : null;
    }

    // CelTimestamp, DateTimeOffset, CelDuration and TimeSpan as a result or a parameter, and the Nullable<> of each: a value of the CEL
    // type of the row converts, except that the nanoseconds of a timestamp or a duration that are not a multiple of 100 do not fit the
    // narrower .NET type. CEL has no wrapper of time, so a Nullable<> has the type of its value, and no collection row has time.
    private static class TimeRows
    {
        internal static ClrRow[] Create()
        {
            var timestampRow = new ReadRow<CelTimestamp>(
                CelType.Timestamp,
                narrowing: false,
                static (CelValue value, out CelTimestamp result) => value.TryGetTimestamp(out result));
            var offsetRow = new ReadRow<DateTimeOffset>(
                CelType.Timestamp,
                narrowing: true,
                static (CelValue value, out DateTimeOffset result) =>
                {
                    result = default;
                    return value.TryGetTimestamp(out CelTimestamp timestamp) && timestamp.TryGetDateTimeOffset(out result);
                });
            var durationRow = new ReadRow<CelDuration>(
                CelType.Duration,
                narrowing: false,
                static (CelValue value, out CelDuration result) => value.TryGetDuration(out result));
            var spanRow = new ReadRow<TimeSpan>(
                CelType.Duration,
                narrowing: true,
                static (CelValue value, out TimeSpan result) =>
                {
                    result = default;
                    return value.TryGetDuration(out CelDuration duration) && duration.TryGetTimeSpan(out result);
                });
            return
            [
                timestampRow, offsetRow, durationRow, spanRow,
                new NullableRow<CelTimestamp>(timestampRow, wrapped: false),
                new NullableRow<DateTimeOffset>(offsetRow, wrapped: false),
                new NullableRow<CelDuration>(durationRow, wrapped: false),
                new NullableRow<TimeSpan>(spanRow, wrapped: false),
            ];
        }
    }
}
