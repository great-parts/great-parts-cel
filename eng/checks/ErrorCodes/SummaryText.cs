using System;
using System.Text;
using System.Xml.Linq;

namespace Great.Parts.Cel.Eng.Checks;

/// <summary>
/// The text of a <c>&lt;summary&gt;</c> element of the XML documentation as one line of a Markdown table cell: the white space is one
/// space, a code element (<c>c</c>, <c>see</c> with <c>cref</c> or <c>langword</c>, <c>paramref</c>) is a code span, and a pipe is escaped.
/// </summary>
internal static class SummaryText
{
    /// <summary>Returns the text of the summary.</summary>
    /// <param name="summary">The <c>&lt;summary&gt;</c> element.</param>
    internal static string Of(XElement summary)
    {
        StringBuilder text = new();
        Append(text, summary);
        string line = string.Join(' ', text.ToString().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return line.Replace("|", "\\|", StringComparison.Ordinal);
    }

    private static void Append(StringBuilder text, XElement element)
    {
        foreach (XNode node in element.Nodes())
        {
            switch (node)
            {
                case XText content:
                    text.Append(content.Value);
                    break;
                case XElement { Name.LocalName: "c" or "code" } code:
                    text.Append('`').Append(code.Value.Trim()).Append('`');
                    break;
                case XElement child when CodeName(child) is { } name:
                    text.Append('`').Append(name).Append('`');
                    break;
                case XElement child:
                    Append(text, child);
                    break;
            }
        }
    }

    // The name that a reference element stands for: the member of a cref without its prefix and parameters, a keyword, a parameter.
    private static string? CodeName(XElement element)
    {
        string? name = element.Name.LocalName switch
        {
            "see" or "seealso" => element.Attribute("cref")?.Value ?? element.Attribute("langword")?.Value,
            "paramref" or "typeparamref" => element.Attribute("name")?.Value,
            _ => null,
        };
        if (name is null)
        {
            return null;
        }

        if (name.Length > 2 && name[1] == ':')
        {
            name = name[2..];
        }

        int parameters = name.IndexOf('(', StringComparison.Ordinal);
        if (parameters >= 0)
        {
            name = name[..parameters];
        }

        int dot = name.LastIndexOf('.');
        return dot >= 0 ? name[(dot + 1)..] : name;
    }
}
