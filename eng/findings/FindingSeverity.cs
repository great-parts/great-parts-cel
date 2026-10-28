namespace Great.Parts.Cel.Eng.Findings;

/// <summary>The severity of a finding. An error changes the exit code of a check; a warning does not.</summary>
public enum FindingSeverity
{
    /// <summary>The finding fails the check.</summary>
    Error,

    /// <summary>The finding is printed and does not change the exit code.</summary>
    Warning,
}
