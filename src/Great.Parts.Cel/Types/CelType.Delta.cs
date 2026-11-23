namespace Great.Parts.Cel.Types;

public sealed partial class CelType
{
    /// <summary>
    /// <c>date</c>, a calendar date from 0001-01-01 to 9999-12-31 without a time of day or a time zone; a value of the
    /// .NET type <see cref="System.DateOnly"/>. An environment knows the type only when the <c>calendar</c> extension is
    /// added to it; otherwise a declaration that uses it is a build error
    /// <see cref="Diagnostics.CelErrorCode.TypeNotDeclared"/>.
    /// </summary>
    public static CelType Date { get; } = new(CelTypeKind.Date, "date", []);

    /// <summary>
    /// <c>time_of_day</c>, a time of day from 00:00 to 23:59:59.9999999 with a precision of 100 nanoseconds, without a date
    /// or a time zone; a value of the .NET type <see cref="System.TimeOnly"/>. An environment knows the type only when the
    /// <c>calendar</c> extension is added to it; otherwise a declaration that uses it is a build error
    /// <see cref="Diagnostics.CelErrorCode.TypeNotDeclared"/>.
    /// </summary>
    public static CelType TimeOfDay { get; } = new(CelTypeKind.TimeOfDay, "time_of_day", []);
}
