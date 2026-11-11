using Xunit;

namespace Great.Parts.Cel.Tests.Examples.Library;

// The classes of characters and the folding of case. The classes \d \D \w \W \s \S, the word boundary
// \b \B and the POSIX classes [[:alpha:]] and the others are ASCII only: the Arabic-Indic digit, the
// fullwidth digit, the letters é, я, ß, the no-break space and the em space are none of \d, \w, \s or [[:alpha:]]
// and the like, \s is [\t\n\f\r ] and has no vertical tab, [[:space:]] has it, and \b is the boundary of ASCII
// word characters, so there is none between the start of the text and é. \pN, \p{Greek}, \p{Lu} and the
// other categories and scripts are those of Unicode 17.0.0, with \PN, \P{Greek} and \p{^Greek} (the same as
// \P{Greek}) and \P{^Greek} (the same as \p{Greek}), inside a class too, and \p{Any}.
// The flag i folds by orbits of simple case folding: (?i)k matches K, k and the Kelvin sign U+212A,
// (?i)s matches the long s U+017F, (?i)σ matches Σ and the final ς, (?i)ǆ matches the three forms of the
// digraph, (?i)ß matches the capital sharp s U+1E9E (and not SS), (?i)i matches neither the dotted İ nor the
// dotless ı (they have no simple folding), and an orbit beyond the BMP works the same: U+10428 and U+10400.
// Folding is applied to the classes written in the pattern, so (?i)\w, which is ASCII, matches the long s
// and the Kelvin sign, which fold to s and k; \W is the complement of that, and so does not match them, and
// (?i)[[:alpha:]] matches them too (this is what Go does).
// The values are those of regexp.MatchString of Go 1.27.3. Each row is checked in four expressions, see
// MatchesRows.
[Trait("Requirement", "F-6")]
[Trait("Requirement", "R-5")]
public sealed class MatchesClassTests
{
    private const string Rows = """
        "5" | ^\d$ | true
        "٣" | ^\d$ | false
        "３" | ^\d$ | false
        "a" | ^\d$ | false
        "5" | ^\D$ | false
        "٣" | ^\D$ | true
        "a" | ^\D$ | true
        "a" | ^\w$ | true
        "Z" | ^\w$ | true
        "_" | ^\w$ | true
        "5" | ^\w$ | true
        "é" | ^\w$ | false
        "я" | ^\w$ | false
        "ß" | ^\w$ | false
        "-" | ^\w$ | false
        "é" | ^\W$ | true
        "-" | ^\W$ | true
        "a" | ^\W$ | false
        "_" | ^\W$ | false
        " " | ^\s$ | true
        "\t" | ^\s$ | true
        "\n" | ^\s$ | true
        "\f" | ^\s$ | true
        "\r" | ^\s$ | true
        "\v" | ^\s$ | false
        "\u00a0" | ^\s$ | false
        "\u2003" | ^\s$ | false
        "\u0085" | ^\s$ | false
        "\u00a0" | ^\S$ | true
        "a" | ^\S$ | true
        " " | ^\S$ | false
        "\v" | ^\S$ | true
        "é" | \b | false
        "aé" | \b | true
        "a" | \b | true
        "" | \b | false
        " " | \b | false
        "é" | \B | true
        "a" | \B | false
        "" | \B | true
        " a" | \B | true
        "é" | \bé | false
        "aé" | \bé | true
        "é" | é\b | false
        "éa" | é\b | true
        "a" | ^[[:alpha:]]$ | true
        "Z" | ^[[:alpha:]]$ | true
        "é" | ^[[:alpha:]]$ | false
        "1" | ^[[:alpha:]]$ | false
        "a" | ^[[:alnum:]]$ | true
        "1" | ^[[:alnum:]]$ | true
        "é" | ^[[:alnum:]]$ | false
        "٣" | ^[[:alnum:]]$ | false
        "5" | ^[[:digit:]]$ | true
        "٣" | ^[[:digit:]]$ | false
        " " | ^[[:space:]]$ | true
        "\v" | ^[[:space:]]$ | true
        "\u00a0" | ^[[:space:]]$ | false
        "A" | ^[[:upper:]]$ | true
        "a" | ^[[:upper:]]$ | false
        "É" | ^[[:upper:]]$ | false
        "a" | ^[[:lower:]]$ | true
        "A" | ^[[:lower:]]$ | false
        "é" | ^[[:lower:]]$ | false
        "!" | ^[[:punct:]]$ | true
        "_" | ^[[:punct:]]$ | true
        "é" | ^[[:punct:]]$ | false
        "a" | ^[[:punct:]]$ | false
        "_" | ^[[:word:]]$ | true
        "a" | ^[[:word:]]$ | true
        "é" | ^[[:word:]]$ | false
        "f" | ^[[:xdigit:]]$ | true
        "F" | ^[[:xdigit:]]$ | true
        "g" | ^[[:xdigit:]]$ | false
        "9" | ^[[:xdigit:]]$ | true
        "a" | ^[[:ascii:]]$ | true
        "é" | ^[[:ascii:]]$ | false
        "\0" | ^[[:ascii:]]$ | true
        " " | ^[[:blank:]]$ | true
        "\t" | ^[[:blank:]]$ | true
        "\n" | ^[[:blank:]]$ | false
        "\u0001" | ^[[:cntrl:]]$ | true
        "\u007f" | ^[[:cntrl:]]$ | true
        "a" | ^[[:cntrl:]]$ | false
        "a" | ^[[:graph:]]$ | true
        " " | ^[[:graph:]]$ | false
        "!" | ^[[:graph:]]$ | true
        "é" | ^[[:graph:]]$ | false
        "a" | ^[[:print:]]$ | true
        " " | ^[[:print:]]$ | true
        "\n" | ^[[:print:]]$ | false
        "é" | ^[[:print:]]$ | false
        "1" | ^[[:^alpha:]]$ | true
        "a" | ^[[:^alpha:]]$ | false
        "é" | ^[[:^alpha:]]$ | true
        "1" | ^[^[:alpha:]]$ | true
        "a" | ^[^[:alpha:]]$ | false
        "é" | ^[^[:alpha:]]$ | true
        "5" | ^[\d_]$ | true
        "_" | ^[\d_]$ | true
        "a" | ^[\d_]$ | false
        "٣" | ^[\d_]$ | false
        "5" | ^[\D]$ | false
        "a" | ^[\D]$ | true
        "5" | ^[^\D]$ | true
        "a" | ^[^\D]$ | false
        "-" | ^[\w-]$ | true
        "a" | ^[\w-]$ | true
        "é" | ^[\w-]$ | false
        "a" | ^\pL$ | true
        "é" | ^\pL$ | true
        "α" | ^\pL$ | true
        "я" | ^\pL$ | true
        "汉" | ^\pL$ | true
        "1" | ^\pL$ | false
        "_" | ^\pL$ | false
        "a" | ^\p{L}$ | true
        "é" | ^\p{L}$ | true
        "1" | ^\p{L}$ | false
        "a" | ^\PL$ | false
        "1" | ^\PL$ | true
        "_" | ^\PL$ | true
        "5" | ^\pN$ | true
        "٣" | ^\pN$ | true
        "Ⅷ" | ^\pN$ | true
        "½" | ^\pN$ | true
        "a" | ^\pN$ | false
        "5" | ^\p{Nd}$ | true
        "٣" | ^\p{Nd}$ | true
        "Ⅷ" | ^\p{Nd}$ | false
        "½" | ^\p{Nd}$ | false
        "A" | ^\p{Lu}$ | true
        "a" | ^\p{Lu}$ | false
        "É" | ^\p{Lu}$ | true
        "Σ" | ^\p{Lu}$ | true
        "a" | ^\p{Ll}$ | true
        "A" | ^\p{Ll}$ | false
        "é" | ^\p{Ll}$ | true
        "σ" | ^\p{Ll}$ | true
        " " | ^\p{Zs}$ | true
        "\u00a0" | ^\p{Zs}$ | true
        "\u2003" | ^\p{Zs}$ | true
        "\t" | ^\p{Zs}$ | false
        "$" | ^\p{Sc}$ | true
        "€" | ^\p{Sc}$ | true
        "a" | ^\p{Sc}$ | false
        "!" | ^\p{P}$ | true
        "_" | ^\p{P}$ | true
        "a" | ^\p{P}$ | false
        "«" | ^\p{P}$ | true
        "α" | ^\p{Greek}$ | true
        "Σ" | ^\p{Greek}$ | true
        "a" | ^\p{Greek}$ | false
        "я" | ^\p{Greek}$ | false
        "a" | ^\p{Latin}$ | true
        "é" | ^\p{Latin}$ | true
        "α" | ^\p{Latin}$ | false
        "я" | ^\p{Cyrillic}$ | true
        "a" | ^\p{Cyrillic}$ | false
        "汉" | ^\p{Han}$ | true
        "a" | ^\p{Han}$ | false
        "α" | ^\P{Greek}$ | false
        "a" | ^\P{Greek}$ | true
        "α" | ^\p{^Greek}$ | false
        "a" | ^\p{^Greek}$ | true
        "α" | ^\P{^Greek}$ | true
        "a" | ^\P{^Greek}$ | false
        "α" | ^[\p{Greek}a]$ | true
        "a" | ^[\p{Greek}a]$ | true
        "b" | ^[\p{Greek}a]$ | false
        "α" | ^[^\p{Greek}]$ | false
        "a" | ^[^\p{Greek}]$ | true
        "α" | ^[\P{Greek}]$ | false
        "a" | ^[\P{Greek}]$ | true
        "a" | ^\p{Any}$ | true
        "\n" | ^\p{Any}$ | true
        "\U0001f600" | ^\p{Any}$ | true
        "K" | (?i)k | true
        "k" | (?i)k | true
        "\u212a" | (?i)k | true
        "j" | (?i)k | false
        "K" | (?i)\x{212A} | true
        "k" | (?i)\x{212A} | true
        "\u212a" | (?i)\x{212A} | true
        "S" | (?i)s | true
        "s" | (?i)s | true
        "\u017f" | (?i)s | true
        "z" | (?i)s | false
        "Σ" | (?i)σ | true
        "\u03c2" | (?i)σ | true
        "σ" | (?i)σ | true
        "Ǆ" | (?i)ǆ | true
        "ǅ" | (?i)ǆ | true
        "ǆ" | (?i)ǆ | true
        "ß" | (?i)ß | true
        "\u1e9e" | (?i)ß | true
        "SS" | (?i)ß | false
        "ss" | (?i)ß | false
        "I" | (?i)i | true
        "i" | (?i)i | true
        "\u0130" | (?i)i | false
        "\u0131" | (?i)i | false
        "B" | (?i)[a-c] | true
        "d" | (?i)[a-c] | false
        "A" | (?i)[^a] | false
        "b" | (?i)[^a] | true
        "a" | (?i)\p{Lu} | true
        "A" | (?i)\p{Lu} | true
        "1" | (?i)\p{Lu} | false
        "\U00010400" | (?i)\x{10428} | true
        "\U00010428" | (?i)\x{10428} | true
        "a" | (?i)\x{10428} | false
        "\u017f" | (?i)^\w$ | true
        "\u212a" | (?i)^\w$ | true
        "k" | (?i)^\w$ | true
        "é" | (?i)^\w$ | false
        "\u017f" | (?i)^\W$ | false
        "\u212a" | (?i)^\W$ | false
        "-" | (?i)^\W$ | true
        "é" | (?i)^\W$ | true
        "\u017f" | (?i)^[[:alpha:]]$ | true
        "\u212a" | (?i)^[[:alpha:]]$ | true
        "é" | (?i)^[[:alpha:]]$ | false
        "5" | (?i)^\d$ | true
        "٣" | (?i)^\d$ | false
        """;

    private readonly CelEnvironment environment = new CelEnvironmentBuilder(CelStandardLibrary.V1)
        .AddVariable<string>("s")
        .AddVariable<string>("p")
        .Build()
        .GetEnvironmentOrThrow();

    [Fact]
    public void EveryRow_GivesItsValueInAllFourForms()
    {
        Assert.Empty(MatchesRows.CheckValues(environment, Rows));
    }
}
