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

    /// <summary>The output of a C# example of the documentation differs from the output shown next to it.</summary>
    public const string Eng301 = "ENG301";

    /// <summary>A C# example of the documentation ended with an exception, with an exit code other than 0, or did not end within the time limit.</summary>
    public const string Eng302 = "ENG302";

    /// <summary>The page of error codes differs from the members of <c>CelErrorCode</c>: a member has no row, a row has no member, or a row has another number or stage.</summary>
    public const string Eng303 = "ENG303";

    /// <summary>An external link of the documentation answered 404 or 410.</summary>
    public const string Eng401 = "ENG401";

    /// <summary>The domain of an external link of the documentation does not exist.</summary>
    public const string Eng402 = "ENG402";

    /// <summary>A relative link of the documentation names a file that does not exist.</summary>
    public const string Eng403 = "ENG403";

    /// <summary>A link of the documentation names an anchor that the page does not have.</summary>
    public const string Eng404 = "ENG404";

    /// <summary>An external link of the documentation failed in another way than 404 or 410: 429, 5xx, a timeout or another failed response. It is a warning.</summary>
    public const string Eng409 = "ENG409";

    /// <summary>A mutant survived: the tests ran and none of them failed. It is an error when the mutation score is below the threshold, otherwise a warning.</summary>
    public const string Eng501 = "ENG501";

    /// <summary>A mutant has no test that covers its code. It is an error when the mutation score is below the threshold, otherwise a warning.</summary>
    public const string Eng502 = "ENG502";

    /// <summary>The mutation score of the changed code is below the threshold of the configuration.</summary>
    public const string Eng503 = "ENG503";

    /// <summary>A changed file of the project under test was not mutated: it is missing in the report, or it has mutants but none was counted.</summary>
    public const string Eng504 = "ENG504";

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

    /// <summary>A skip list of a suite has no valid table or an invalid entry: a value outside its form, a repeated entry or overlapping scopes of one case, a verdict without a registry row, the wrong order.</summary>
    public const string Eng800 = "ENG800";

    /// <summary>A case of a suite failed, and no entry of the skip list of the suite applies to the cell of the run.</summary>
    public const string Eng801 = "ENG801";

    /// <summary>A case of a suite passed, and an entry of the skip list of the suite applies to the cell of the run: the entry has to be removed.</summary>
    public const string Eng802 = "ENG802";

    /// <summary>An entry of the skip list of a suite names a case that the suite does not have.</summary>
    public const string Eng804 = "ENG804";

    /// <summary>A test ran under JIT but not under Native AOT: the report of the Native AOT run does not have it.</summary>
    public const string Eng810 = "ENG810";

    /// <summary>A test ran under Native AOT but not under JIT: the report of the JIT run does not have it.</summary>
    public const string Eng811 = "ENG811";

    /// <summary>A new signature of a divergence from cel-go.</summary>
    public const string Eng820 = "ENG820";

    /// <summary>
    /// The Go of the oracle, named by its handshake, differs from the <c>go</c> line of <c>eng/oracle/go.mod</c>; the run stops
    /// before the first case.
    /// </summary>
    public const string Eng821 = "ENG821";

    /// <summary>A time zone offset differs from Go.</summary>
    public const string Eng822 = "ENG822";
}
