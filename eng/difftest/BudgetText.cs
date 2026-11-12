using System;
using System.Globalization;

namespace Great.Parts.Cel.Eng.Difftest;

/// <summary>
/// The form of the time of <c>--budget</c> of <c>eng/difftest</c> (<c>docs/design/eng-difftest.md</c>, "Invocation").
/// </summary>
internal static class BudgetText
{
    /// <summary>Reads a positive whole number of seconds, minutes or hours: <c>30s</c>, <c>5m</c>, <c>2h</c>.</summary>
    /// <param name="value">The text.</param>
    /// <param name="budget">The time, or zero when the text is not of that form.</param>
    /// <returns><see langword="true"/> when the text is of that form.</returns>
    internal static bool TryParse(string value, out TimeSpan budget)
    {
        budget = TimeSpan.Zero;
        if (value.Length < 2
            || !int.TryParse(value.AsSpan(0, value.Length - 1), NumberStyles.None, CultureInfo.InvariantCulture, out int amount)
            || amount <= 0)
        {
            return false;
        }

        switch (value[^1])
        {
            case 's':
                budget = TimeSpan.FromSeconds(amount);
                return true;
            case 'm':
                budget = TimeSpan.FromMinutes(amount);
                return true;
            case 'h':
                budget = TimeSpan.FromHours(amount);
                return true;
            default:
                return false;
        }
    }
}
