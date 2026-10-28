namespace Great.Parts.Cel.Properties;

/// <summary>
/// The settings every property of the project passes to CsCheck, so a run checks the same cases on every machine.
/// </summary>
internal static class PropertySettings
{
    /// <summary>The seed of every property: a CsCheck seed string. Changing it changes the checked cases.</summary>
    public const string Seed = "0Zq4tSbL9xA1";

    /// <summary>The number of iterations of a property.</summary>
    public const long Iterations = 1000;
}
