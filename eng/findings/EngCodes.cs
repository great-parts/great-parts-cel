namespace Great.Parts.Cel.Eng.Findings;

/// <summary>
/// The codes of the findings of the repository's own checks, one constant per code. A code is added together with the check
/// that writes it and takes the next free number of its group; numbers are not reused.
/// </summary>
public static class EngCodes
{
    /// <summary>A check or a rule is turned off or lowered without a reason next to it.</summary>
    public const string Eng101 = "ENG101";

    /// <summary>The reason next to the turning off of a check or a rule only repeats the rule ID.</summary>
    public const string Eng102 = "ENG102";

    /// <summary>A line of a package list <c>eng/packages/&lt;package&gt;.txt</c> does not parse.</summary>
    public const string Eng200 = "ENG200";

    /// <summary>A dependency group of a package has a dependency that the package list does not name.</summary>
    public const string Eng201 = "ENG201";

    /// <summary>A dependency group or a dependency of the package list is missing in the package.</summary>
    public const string Eng202 = "ENG202";

    /// <summary>A package has an entry that the package list does not name.</summary>
    public const string Eng203 = "ENG203";

    /// <summary>A file of the package list is missing in the package.</summary>
    public const string Eng204 = "ENG204";

    /// <summary>A package has no list <c>eng/packages/&lt;package&gt;.txt</c>.</summary>
    public const string Eng205 = "ENG205";

    /// <summary>A package list has no package.</summary>
    public const string Eng206 = "ENG206";

    /// <summary>The license expression of the package is missing or differs from the package list.</summary>
    public const string Eng207 = "ENG207";

    /// <summary>The file <c>eng/ratchet.json</c> is not valid: it is not JSON, has an unknown or repeated key, keys out of order, or a value that is not allowed.</summary>
    public const string Eng600 = "ENG600";

    /// <summary>A run of tokens of the C# sources repeats at another place after the identifiers and the literals are renamed.</summary>
    public const string Eng601 = "ENG601";

    /// <summary>A rule has more suppressions with a reason than the baseline allows.</summary>
    public const string Eng602 = "ENG602";

    /// <summary>The repository has more pairs of repeated code than the baseline allows.</summary>
    public const string Eng603 = "ENG603";

    /// <summary>The thresholds of <c>CodeMetricsConfig.txt</c> differ from the baseline.</summary>
    public const string Eng604 = "ENG604";

    /// <summary>A number of the repository is below its baseline: the baseline has to be lowered.</summary>
    public const string Eng605 = "ENG605";

    /// <summary>A commit of the checked range has no <c>Signed-off-by</c> trailer.</summary>
    public const string Eng701 = "ENG701";
}
