using System.Text;

namespace Great.Parts.Cel.Generator;

// Writes the text of the created file: four spaces for a level and "\n" for a line end, so the text does not depend on the
// machine.
internal sealed class CodeWriter
{
    private readonly StringBuilder text = new();
    private int level;

    internal void Line(string line)
    {
        if (line.Length > 0)
        {
            text.Append(' ', level * 4).Append(line);
        }

        text.Append('\n');
    }

    internal void EmptyLine() => text.Append('\n');

    internal void Indent() => level++;

    internal void Outdent() => level--;

    // A line, then an opening brace on the next one; the body is one level deeper.
    internal void Open(string line)
    {
        Line(line);
        Line("{");
        Indent();
    }

    internal void Close()
    {
        Outdent();
        Line("}");
    }

    public override string ToString() => text.ToString();
}
