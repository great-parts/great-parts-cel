using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Great.Parts.Cel.Eng.Findings;

namespace Great.Parts.Cel.Eng.Checks;

/// <summary>
/// Checks that the page of error codes matches the members of <c>CelErrorCode</c> (<c>docs/design/eng-docs.md</c>, "error-codes"): a
/// member without a row, a row without a member, another number, another stage or another summary is <c>ENG303</c>. The members come
/// from the Release build of <c>net10.0</c> that step 1 of <c>eng/docs</c> made; the page is given by <c>--page</c>. With <c>--write</c> the command
/// first rewrites the table of the page from the members and their summaries, and the rest of the page stays as it is.
/// </summary>
internal sealed class ErrorCodesCommand : ICheckCommand
{
    /// <summary>The option of the command line: the page of error codes.</summary>
    internal const string PageOption = "--page";

    /// <summary>The flag of the command line: rewrite the table of the page from the members of the library.</summary>
    internal const string WriteOption = "--write";

    /// <summary>The built library, relative to the repository root.</summary>
    internal const string LibraryPath = "src/Great.Parts.Cel/bin/Release/net10.0/Great.Parts.Cel.dll";

    /// <inheritdoc/>
    public string Name => "error-codes";

    /// <inheritdoc/>
    public IEnumerable<Finding> Run(CheckContext context)
    {
        if (!context.Options.TryGetValue(PageOption, out string? given))
        {
            throw new CheckInputException($"the command {Name} needs the option {PageOption}");
        }

        string pagePath = Path.GetFullPath(given, context.Root);
        if (!File.Exists(pagePath))
        {
            throw new CheckInputException($"the page {given} does not exist");
        }

        string library = Path.Combine(context.Root, LibraryPath);
        List<ErrorCodeMember> members = ErrorCodeLibrary.Read(library, Path.ChangeExtension(library, ".xml"));

        string text = File.ReadAllText(pagePath);
        string newline = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        List<string> lines = [.. text.ReplaceLineEndings("\n").Split('\n')];
        ErrorCodesTable table = Table(lines, given);

        if (context.Options.ContainsKey(WriteOption))
        {
            lines = table.Rewrite(lines, members);
            string rewritten = string.Join(newline, lines);
            if (rewritten != text)
            {
                File.WriteAllText(pagePath, rewritten, new UTF8Encoding(false));
            }

            table = Table(lines, given);
        }

        string relative = Path.GetRelativePath(context.Root, pagePath).Replace('\\', '/');
        return table.Compare(relative, members).ToList();
    }

    private static ErrorCodesTable Table(IReadOnlyList<string> lines, string page) =>
        ErrorCodesTable.Find(lines)
        ?? throw new CheckInputException($"the page {page} has no table with the columns Code, Name, Stage and When it occurs");
}
