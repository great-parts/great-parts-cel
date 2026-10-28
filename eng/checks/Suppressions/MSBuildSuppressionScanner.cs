using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

namespace Great.Parts.Cel.Eng.Checks;

/// <summary>
/// Reads the forms of the MSBuild files <c>*.csproj</c>, <c>*.props</c> and <c>*.targets</c>: <c>NoWarn</c> and
/// <c>WarningsNotAsErrors</c> with the IDs they add, and the properties of <see cref="MSBuildProperties"/> with a value
/// other than the safe one (<c>docs/design/eng-checks-source.md</c>, "suppressions"). A property counts in any property
/// group, with or without a condition; a file without the property has no form. The reason is the XML comment that ends in
/// the line above the element. A file that is not well-formed XML has no forms: the build rejects it.
/// </summary>
internal sealed class MSBuildSuppressionScanner : ISuppressionScanner
{
    private const string ReasonPlace = "reason comment in the line above";
    private const string PropertyGroup = "PropertyGroup";
    private static readonly string[] Extensions = [".csproj", ".props", ".targets"];
    private static readonly string[] IdProperties = ["NoWarn", "WarningsNotAsErrors"];

    /// <inheritdoc/>
    public IEnumerable<SuppressionForm> Scan(CheckContext context) => context.Files
        .Where(file => Extensions.Any(extension => file.Path.EndsWith(extension, StringComparison.OrdinalIgnoreCase)))
        .SelectMany(file => ScanFile(file.Path, file.ReadText()));

    // A comment node is directly above an element when no other node comes between them. The reader gives the line where
    // the content of a comment starts, which is the line of its opening '<!--'; the comment ends after the line breaks of
    // its text.
    private static List<SuppressionForm> ScanFile(string path, string text)
    {
        XmlReaderSettings settings = new() { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, IgnoreWhitespace = true };
        List<SuppressionForm> forms = [];
        List<Comment> comments = [];
        Stack<string> parents = new();
        try
        {
            using StringReader input = new(text);
            using XmlReader reader = XmlReader.Create(input, settings);
            IXmlLineInfo lineInfo = (IXmlLineInfo)reader;
            bool more = reader.Read();
            while (more)
            {
                if (reader.NodeType == XmlNodeType.Comment)
                {
                    comments.Add(new Comment(lineInfo.LineNumber, reader.Value));
                    more = reader.Read();
                }
                else if (reader.NodeType == XmlNodeType.Element && parents.TryPeek(out string? parent) && parent == PropertyGroup)
                {
                    Position position = new(lineInfo.LineNumber, lineInfo.LinePosition - 1);
                    XElement element = (XElement)XNode.ReadFrom(reader);
                    AddForm(forms, path, element, position, comments);
                    comments.Clear();
                    more = !reader.EOF;
                }
                else
                {
                    comments.Clear();
                    Track(reader, parents);
                    more = reader.Read();
                }
            }
        }
        catch (XmlException)
        {
            return [];
        }

        return forms;
    }

    private static void Track(XmlReader reader, Stack<string> parents)
    {
        if (reader.NodeType == XmlNodeType.Element && !reader.IsEmptyElement)
        {
            parents.Push(reader.LocalName);
        }
        else if (reader.NodeType == XmlNodeType.EndElement)
        {
            parents.Pop();
        }
    }

    private static void AddForm(List<SuppressionForm> forms, string path, XElement element, Position position, List<Comment> comments)
    {
        string name = element.Name.LocalName;
        string value = element.Value.Trim();
        string[] ids = IdProperties.Contains(name, StringComparer.OrdinalIgnoreCase) ? AddedIds(value) : [];
        if (ids.Length > 0)
        {
            forms.Add(Form(path, element, position, comments, ids, ids));
        }
        else if (MSBuildProperties.Find(name) is { } property && property.IsForm(value))
        {
            forms.Add(Form(path, element, position, comments, [property.Key], []));
        }
    }

    // The IDs of the value of NoWarn or WarningsNotAsErrors: a reference such as $(NoWarn) is not an ID; a bare number is
    // a compiler warning.
    private static string[] AddedIds(string value) =>
    [
        .. value.Split([';', ','], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Where(id => !id.Contains("$(", StringComparison.Ordinal) && !id.Contains("@(", StringComparison.Ordinal) && !id.Contains("%(", StringComparison.Ordinal))
            .Select(SuppressionReason.NormalizeId),
    ];

    private static SuppressionForm Form(string path, XElement element, Position position, List<Comment> comments, IReadOnlyList<string> keys, IReadOnlyList<string> ruleIds)
    {
        string name = element.Name.LocalName;
        string value = string.Join(' ', element.Value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return new SuppressionForm(path, position, $"'<{name}>{value}</{name}>'", ReasonPlace, keys, ruleIds, ReasonAbove(comments, position.Line));
    }

    // The comments directly above the element: each ends in the line before the next one starts.
    private static string ReasonAbove(List<Comment> comments, int elementLine)
    {
        List<string> reasons = [];
        int expectedEnd = elementLine - 1;
        for (int index = comments.Count - 1; index >= 0 && comments[index].EndLine == expectedEnd; index--)
        {
            reasons.Insert(0, comments[index].Text);
            expectedEnd = comments[index].StartLine - 1;
        }

        return string.Join(' ', reasons);
    }

    private readonly record struct Comment(int StartLine, string Text)
    {
        internal int EndLine => StartLine + new SourceLines(Text).Locate(Text.Length).Line - 1;
    }
}
