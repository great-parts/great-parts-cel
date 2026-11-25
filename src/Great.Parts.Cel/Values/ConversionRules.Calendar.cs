using System;
using System.Runtime.CompilerServices;
using Great.Parts.Cel.Types;

namespace Great.Parts.Cel.Values;

// The rows of DateOnly and TimeOnly and the Nullable<> of each as input, result, parameter, element and value: the CEL types date and
// time_of_day of the calendar set, exactly, with no rounding, since the precision of both is 100 nanoseconds. Like char and the wide
// integers these rows are leaves, so lists, optionals and dictionaries of them are rows too; CEL has no wrapper of a date, so a
// Nullable<> has the type of its value. A key of a dictionary is never a date: the keys of a map are int, uint, bool and string.
internal static partial class ConversionRules
{
    private static class CalendarRows
    {
        internal static ClrRow[] Create()
        {
            var dateRow = new ReadRow<DateOnly>(CelType.Date, narrowing: false, leaf: true, static (CelValue value, out DateOnly result) => value.TryGetDate(out result));
            var timeRow = new ReadRow<TimeOnly>(CelType.TimeOfDay, narrowing: false, leaf: true, static (CelValue value, out TimeOnly result) => value.TryGetTimeOfDay(out result));
            return [dateRow, timeRow, new NullableRow<DateOnly>(dateRow, wrapped: false), new NullableRow<TimeOnly>(timeRow, wrapped: false)];
        }
    }

    // DateOnly, TimeOnly and the Nullable of each by their static type.
    private static bool TryFromCalendar<T>(T value, out CelValue result)
    {
        if (typeof(T) == typeof(DateOnly))
        {
            result = CelValue.FromDate(Unsafe.As<T, DateOnly>(ref value));
        }
        else if (typeof(T) == typeof(TimeOnly))
        {
            result = CelValue.FromTimeOfDay(Unsafe.As<T, TimeOnly>(ref value));
        }
        else if (typeof(T) == typeof(DateOnly?))
        {
            return FromNullable(Unsafe.As<T, DateOnly?>(ref value), out result);
        }
        else if (typeof(T) == typeof(TimeOnly?))
        {
            return FromNullable(Unsafe.As<T, TimeOnly?>(ref value), out result);
        }
        else
        {
            result = default;
            return false;
        }

        return true;
    }

    // A boxed DateOnly or TimeOnly in a place of the type dyn; null for a value of another type.
    private static CelValue? FromBoxedCalendar(object? value) => value switch
    {
        DateOnly date => CelValue.FromDate(date),
        TimeOnly time => CelValue.FromTimeOfDay(time),
        _ => null,
    };

    // The CEL type of DateOnly and TimeOnly of the input rows, or null for another .NET type.
    private static CelType? CalendarInputType(Type type)
    {
        if (type == typeof(DateOnly))
        {
            return CelType.Date;
        }

        return type == typeof(TimeOnly) ? CelType.TimeOfDay : null;
    }
}
