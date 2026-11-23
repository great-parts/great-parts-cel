using System;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Library.Time;

// The cost functions of the arguments of the overloads of the set calendar: the part of a cost that the call node charges before the
// implementation runs, beyond the fixed unit of the overload. The strings that the set reads — the text of a date or of a time of day
// and the name of a time zone — cost ⌈L/10⌉ as the strings of the functions of time do (TimeCosts.Text); the text that it writes is
// known from the value, before the string is created. An argument of another kind adds nothing, since the implementation rejects it.
internal static class CalendarCosts
{
    // ⌈Lᵣ/10⌉ for string(date): the text is always 10 characters.
    internal static readonly CelCostFunction DateText = static _ => 1;

    // ⌈Lᵣ/10⌉ for string(time_of_day): Lᵣ is 8, or 9 and the digits of the fraction.
    internal static readonly CelCostFunction TimeOfDayText = static arguments =>
        arguments.Length > 0 && arguments[0].TryGetTimeOfDay(out TimeOnly value)
            ? ((ulong)CalendarTextRules.TimeOfDayLength(value) + 9) / 10
            : 0;
}
