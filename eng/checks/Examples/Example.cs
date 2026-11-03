using System.Collections.Generic;
using System.Linq;

namespace Great.Parts.Cel.Eng.Checks;

/// <summary>One line of the expected output of an example and the place in the file that shows it.</summary>
/// <param name="Text">The expected line.</param>
/// <param name="Position">The place of the line: the comment <c>// Output:</c>, or the line of the <c>text</c> block.</param>
internal sealed record ExpectedLine(string Text, Position Position);

/// <summary>
/// The option <c>skip: &lt;reason&gt;</c> of an example, which is neither built nor run. <c>suppressions</c> counts it under the key
/// <c>example</c> and judges the reason.
/// </summary>
/// <param name="Position">The line and column of the form: the opening fence of the block, or the start tag <c>&lt;code&gt;</c>.</param>
/// <param name="Reason">The reason as written; empty when there is none.</param>
/// <param name="Description">What a finding names, quoted, such as <c>'csharp skip'</c>.</param>
/// <param name="ReasonPlace">Where the reason is, as the end of the message of a finding.</param>
internal sealed record ExampleSkip(Position Position, string Reason, string Description, string ReasonPlace);

/// <summary>
/// A C# example of the documentation: a fenced block of <c>README.md</c> or <c>docs/**/*.md</c>, or a <c>&lt;code&gt;</c> element of the XML
/// documentation (<c>docs/design/eng-docs.md</c>, "examples"). Every line of the code keeps its column in the file, so the compiler
/// can point at the file.
/// </summary>
/// <param name="Path">The path of the file that holds the example, relative to the root, with <c>/</c>.</param>
/// <param name="FirstLine">The line of the file that holds <paramref name="Lines"/>[0].</param>
/// <param name="Lines">The lines of the code, one for each line of the file from <paramref name="FirstLine"/> on.</param>
/// <param name="Target">The target framework of the example: <c>net10.0</c> or <c>net11.0</c>.</param>
/// <param name="Skip">The option <c>skip</c>, or <see langword="null"/> when the example is built and run.</param>
/// <param name="Expected">The lines of the expected output; empty when the example shows none, and then it must print nothing.</param>
internal sealed record Example(
    string Path,
    int FirstLine,
    IReadOnlyList<string> Lines,
    string Target,
    ExampleSkip? Skip,
    IReadOnlyList<ExpectedLine> Expected)
{
    /// <summary>The default target framework of an example.</summary>
    internal const string DefaultTarget = "net10.0";

    /// <summary>The target framework of an example that uses the <c>decimal</c> API.</summary>
    internal const string DecimalTarget = "net11.0";

    /// <summary>The place of the first line of the code that is not empty, at its first character.</summary>
    internal Position Start
    {
        get
        {
            int index = Lines.ToList().FindIndex(line => !string.IsNullOrWhiteSpace(line));
            return index < 0 ? new Position(FirstLine, 1) : new Position(FirstLine + index, Lines[index].Length - Lines[index].TrimStart().Length + 1);
        }
    }
}
