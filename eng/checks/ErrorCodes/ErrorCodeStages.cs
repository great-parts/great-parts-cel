namespace Great.Parts.Cel.Eng.Checks;

/// <summary>
/// The stage of an error code, which follows from its number as the documentation of <c>CelErrorCode</c> states: 1000–1999
/// <c>Parse</c>, 2000–2999 <c>Check</c>, 3000–3999 <c>Evaluate</c>, 4000–4999 <c>Build</c>, 9000–9999 <c>Any</c>.
/// </summary>
internal static class ErrorCodeStages
{
    /// <summary>Returns the stage of the number, or <see langword="null"/> when the number is in none of the ranges.</summary>
    /// <param name="number">The number of the error code.</param>
    internal static string? Of(int number) => number switch
    {
        >= 1000 and <= 1999 => "Parse",
        >= 2000 and <= 2999 => "Check",
        >= 3000 and <= 3999 => "Evaluate",
        >= 4000 and <= 4999 => "Build",
        >= 9000 and <= 9999 => "Any",
        _ => null,
    };
}
