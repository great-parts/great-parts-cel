using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Great.Parts.Cel.Eng.Findings;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Great.Parts.Cel.Eng.Tests.Support;

/// <summary>A code of the repository's own checks.</summary>
/// <param name="Code">The code, such as <c>ENG604</c> or <c>ARCH001</c>.</param>
/// <param name="FieldName">The name of the <see cref="EngCodes"/> field of an <c>ENG</c> code; <see langword="null"/> for <c>ARCH</c>.</param>
internal sealed record RegisteredCode(string Code, string? FieldName);

/// <summary>The logic of the code registry test (docs/design/eng-checks.md, "Code registry test").</summary>
internal static class CodeRegistry
{
    private static readonly Regex CodePattern = new(@"^ENG\d{3}$", RegexOptions.CultureInvariant);

    private static readonly string[] TestDirectories = ["eng/tests", "tests"];

    /// <summary>The <c>ENG</c> codes: the constant fields of the type, in the order of declaration.</summary>
    /// <param name="type">The class of the codes, <see cref="EngCodes"/>.</param>
    internal static IReadOnlyList<RegisteredCode> EngCodesOf(Type type) => [.. type
        .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
        .Where(field => field.IsLiteral)
        .Select(field => new RegisteredCode((string)field.GetRawConstantValue()!, field.Name))];

    /// <summary>The <c>ARCH</c> codes: the <c>Rule ID</c> column of the tables of the release files.</summary>
    /// <param name="releaseTexts">The content of the files <c>AnalyzerReleases.*.md</c>.</param>
    internal static IReadOnlyList<RegisteredCode> ArchCodesIn(IEnumerable<string> releaseTexts)
    {
        List<RegisteredCode> codes = [];
        foreach (string text in releaseTexts)
        {
            int column = -1;
            foreach (string line in text.Split('\n'))
            {
                string[] cells = line.Split('|', StringSplitOptions.TrimEntries);
                int header = Array.IndexOf(cells, "Rule ID");
                if (header >= 0)
                {
                    column = header;
                }
                else if (column >= 0 && column < cells.Length && cells.Length > 1 && cells[column].StartsWith("ARCH", StringComparison.Ordinal))
                {
                    codes.Add(new RegisteredCode(cells[column], null));
                }
            }
        }

        return codes;
    }

    /// <summary>The problems with the values of the <c>ENG</c> constants: not <c>ENG</c> and three digits, not the field name in upper case, repeated.</summary>
    /// <param name="codes">The fields as codes with their names.</param>
    internal static IReadOnlyList<string> ValueProblems(IReadOnlyList<RegisteredCode> codes)
    {
        List<string> problems = [];
        foreach (RegisteredCode code in codes)
        {
            if (!CodePattern.IsMatch(code.Code))
            {
                problems.Add($"{code.FieldName}: the value {code.Code} is not ENG and three digits");
            }

            if (!string.Equals(code.FieldName?.ToUpperInvariant(), code.Code, StringComparison.Ordinal))
            {
                problems.Add($"{code.FieldName}: the value {code.Code} is not the field name in upper case");
            }
        }

        problems.AddRange(codes
            .GroupBy(code => code.Code, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => $"{group.Key}: the value occurs {group.Count().ToString(CultureInfo.InvariantCulture)} times"));
        return problems;
    }

    /// <summary>
    /// The codes without a test, as <c>&lt;code&gt; has no test</c>. A code has a test when a source contains it as a token of code:
    /// the identifier of its <see cref="EngCodes"/> field or a string literal equal to the code. A comment does not count;
    /// the text of a branch that a preprocessor directive turns off does.
    /// </summary>
    /// <param name="codes">The codes of the repository.</param>
    /// <param name="testSources">The content of the files <c>*Tests.cs</c>, without the file of the registry test.</param>
    internal static IReadOnlyList<string> CodesWithoutTest(IEnumerable<RegisteredCode> codes, IEnumerable<string> testSources)
    {
        HashSet<string> used = new(StringComparer.Ordinal);
        foreach (string source in testSources)
        {
            foreach (SyntaxToken token in Tokens(source))
            {
                if (token.IsKind(SyntaxKind.IdentifierToken) || token.IsKind(SyntaxKind.StringLiteralToken))
                {
                    used.Add(token.ValueText);
                }
            }
        }

        return [.. codes
            .Where(code => !used.Contains(code.Code) && (code.FieldName is null || !used.Contains(code.FieldName)))
            .Select(code => $"{code.Code} has no test")];
    }

    /// <summary>The <c>*Tests.cs</c> files under <c>eng/tests/</c> and <c>tests/</c>, except the registry test and build output.</summary>
    /// <param name="root">The repository root.</param>
    internal static IReadOnlyList<string> TestFiles(string root) => [.. TestDirectories
        .Select(directory => Path.Combine(root, directory))
        .Where(Directory.Exists)
        .SelectMany(directory => Directory.EnumerateFiles(directory, "*Tests.cs", SearchOption.AllDirectories))
        .Where(file => Path.GetFileName(file) != "CodeRegistryTests.cs")
        .Where(file => !Path.GetRelativePath(root, file)
            .Split(Path.DirectorySeparatorChar)
            .Any(segment => segment is "bin" or "obj" or "cel-spec"))
        .Order(StringComparer.Ordinal)];

    private static IEnumerable<SyntaxToken> Tokens(string source)
    {
        SyntaxNode root = CSharpSyntaxTree.ParseText(source).GetRoot();
        foreach (SyntaxToken token in root.DescendantTokens())
        {
            yield return token;
        }

        // Text of a branch that no directive of the file activates is not code for the parser; its tokens count too.
        foreach (SyntaxTrivia trivia in root.DescendantTrivia(descendIntoTrivia: false).Where(trivia => trivia.IsKind(SyntaxKind.DisabledTextTrivia)))
        {
            foreach (SyntaxToken token in Tokens(trivia.ToString()))
            {
                yield return token;
            }
        }
    }
}
