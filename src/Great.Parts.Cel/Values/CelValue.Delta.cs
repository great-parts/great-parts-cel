using System;

namespace Great.Parts.Cel.Values;

public readonly partial struct CelValue
{
    /// <summary>
    /// Creates a <c>date</c> value from a <see cref="DateOnly"/>, exactly.
    /// </summary>
    /// <remarks>
    /// Expressions use the value only in environments with the <c>calendar</c> extension set; in another environment a
    /// variable bound to it is an evaluation error <see cref="Diagnostics.CelErrorCode.BindingTypeMismatch"/> and a value
    /// in a <c>dyn</c> place is <see cref="Diagnostics.CelErrorCode.TypeNotAccessible"/>.
    /// </remarks>
    /// <param name="value">The date.</param>
    /// <returns>A value of the kind <see cref="CelValueKind.Date"/>; no memory is allocated.</returns>
    public static CelValue FromDate(DateOnly value)
    {
        return new CelValue(CelValueKind.Date, null, (ulong)value.DayNumber, 0);
    }

    /// <summary>
    /// Creates a <c>time_of_day</c> value from a <see cref="TimeOnly"/>, exactly: the precision of both is 100 nanoseconds.
    /// </summary>
    /// <remarks>
    /// Expressions use the value only in environments with the <c>calendar</c> extension set, as for
    /// <see cref="FromDate(DateOnly)"/>.
    /// </remarks>
    /// <param name="value">The time of day.</param>
    /// <returns>A value of the kind <see cref="CelValueKind.TimeOfDay"/>; no memory is allocated.</returns>
    public static CelValue FromTimeOfDay(TimeOnly value)
    {
        return new CelValue(CelValueKind.TimeOfDay, null, (ulong)value.Ticks, 0);
    }

    /// <summary>
    /// Reads a <c>date</c> value as a <see cref="DateOnly"/>, exactly.
    /// </summary>
    /// <param name="value">The date when the method returns <see langword="true"/>; otherwise <see langword="default"/>.</param>
    /// <returns><see langword="true"/> when the value has the kind <see cref="CelValueKind.Date"/>.</returns>
    public bool TryGetDate(out DateOnly value)
    {
        value = _kind == CelValueKind.Date ? DateOnly.FromDayNumber((int)_p0) : default;
        return _kind == CelValueKind.Date;
    }

    /// <summary>
    /// Reads a <c>time_of_day</c> value as a <see cref="TimeOnly"/>, exactly.
    /// </summary>
    /// <param name="value">The time of day when the method returns <see langword="true"/>; otherwise <see langword="default"/>.</param>
    /// <returns><see langword="true"/> when the value has the kind <see cref="CelValueKind.TimeOfDay"/>.</returns>
    public bool TryGetTimeOfDay(out TimeOnly value)
    {
        value = _kind == CelValueKind.TimeOfDay ? new TimeOnly((long)_p0) : default;
        return _kind == CelValueKind.TimeOfDay;
    }
}
