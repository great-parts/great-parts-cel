using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Great.Parts.Cel.Eng.Checks;

/// <summary>
/// Reads the C# examples of the XML documentation of a source file (<c>docs/design/eng-docs.md</c>, "examples"): every <c>&lt;code&gt;</c>
/// element of a documentation comment. The attributes are its options: <c>target="net11.0"</c> and <c>skip="&lt;reason&gt;"</c>. The expected
/// output is the comments <c>// Output:</c> of the code.
/// </summary>
internal static class XmlDocExamples
{
    private const string CodeElement = "code";
    private const string SkipDescription = "'<code skip>'";
    private const string SkipReasonPlace = "reason in the skip attribute";

    /// <summary>
    /// Reads the examples of the file under every symbol set: an example under <c>#if NET11_0_OR_GREATER</c> is found, and an example
    /// that several sets read the same way is one example.
    /// </summary>
    /// <param name="file">The file with its parses.</param>
    internal static IReadOnlyList<Example> Read(CSharpFile file)
    {
        if (!file.Text.Contains("<code", StringComparison.Ordinal))
        {
            return [];
        }

        return [.. file.Find(tree => Examples(tree, file)).Select(place => place.Value)];
    }

    // The trees of the sources are parsed without documentation, so a comment is only trivia. The same text is parsed again under the
    // same symbols with the documentation on.
    private static IEnumerable<(TextSpan Span, Example Value)> Examples(SyntaxTree tree, CSharpFile file)
    {
        CSharpParseOptions options = ((CSharpParseOptions)tree.Options).WithDocumentationMode(DocumentationMode.Parse);
        SyntaxNode root = CSharpSyntaxTree.ParseText(file.Text, options, file.Path).GetRoot();
        foreach (XmlElementSyntax element in root.DescendantNodes(descendIntoTrivia: true).OfType<XmlElementSyntax>().Where(IsCode))
        {
            yield return (element.Span, Build(element, file.Path, file.Lines));
        }
    }

    private static bool IsCode(XmlElementSyntax element) =>
        element.StartTag.Name.Prefix is null && element.StartTag.Name.LocalName.ValueText == CodeElement;

    private static Example Build(XmlElementSyntax element, string path, SourceLines places)
    {
        (int firstLine, List<string> lines) = CodeLines(element, places);
        string? reason = AttributeValue(element, "skip");
        ExampleSkip? skip = reason is null
            ? null
            : new ExampleSkip(places.Locate(element.StartTag.SpanStart), reason.Trim(), SkipDescription, SkipReasonPlace);

        // A target other than the two that the examples use is read as the default: the build then reports what does not compile.
        string target = AttributeValue(element, "target") == Example.DecimalTarget ? Example.DecimalTarget : Example.DefaultTarget;
        return new Example(path, firstLine, lines, target, skip, ExpectedOutput.FromComments(lines, firstLine));
    }

    private static string? AttributeValue(XmlElementSyntax element, string name)
    {
        XmlTextAttributeSyntax? attribute = element.StartTag.Attributes
            .OfType<XmlTextAttributeSyntax>()
            .FirstOrDefault(candidate => candidate.Name.LocalName.ValueText == name);
        return attribute is null ? null : string.Concat(attribute.TextTokens.Select(token => token.ValueText));
    }

    // The code of the element, one string for each line of the file. The text of a token is put at its column, so the code keeps the
    // columns it has in the file: the exterior of the comment, '///', and the markup around it become spaces. The empty lines at the
    // start and at the end are dropped.
    private static (int FirstLine, List<string> Lines) CodeLines(XmlElementSyntax element, SourceLines places)
    {
        SortedDictionary<int, StringBuilder> byLine = [];
        foreach (SyntaxToken token in element.Content.SelectMany(node => node.DescendantTokens()).Where(token => !token.IsMissing))
        {
            Position at = places.Locate(token.SpanStart);
            if (!byLine.TryGetValue(at.Line, out StringBuilder? line))
            {
                line = new StringBuilder();
                byLine[at.Line] = line;
            }

            // The first text of a line is put at its column; the next ones follow it, so an entity, which is longer in the file than
            // the character it stands for, does not leave a gap.
            string text = TextOf(token);
            if (text.Length > 0)
            {
                line.Append(' ', line.Length == 0 ? at.Column - 1 : 0).Append(text);
            }
        }

        int[] filled = [.. byLine.Where(pair => pair.Value.ToString().Trim().Length > 0).Select(pair => pair.Key)];
        if (filled.Length == 0)
        {
            return (places.Locate(element.StartTag.SpanStart).Line, []);
        }

        List<string> lines = [];
        for (int number = filled[0]; number <= filled[^1]; number++)
        {
            lines.Add(byLine.TryGetValue(number, out StringBuilder? found) ? found.ToString().TrimEnd() : string.Empty);
        }

        return (filled[0], lines);
    }

    // The text of a token as the code: a line break and the markers of a CDATA section are nothing, an entity is its character.
    private static string TextOf(SyntaxToken token) => token.Kind() switch
    {
        SyntaxKind.XmlTextLiteralNewLineToken or SyntaxKind.XmlCDataStartToken or SyntaxKind.XmlCDataEndToken => string.Empty,
        SyntaxKind.XmlTextLiteralToken or SyntaxKind.XmlEntityLiteralToken => token.ValueText,
        _ => token.Text,
    };
}
