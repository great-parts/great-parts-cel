namespace Great.Parts.Cel.Generator;

// The text of a value that a row of the reading uses, and how many times the row names it. A value that is named more than once
// is read into a local first, so a getter of the application runs once. A part of a value (`.Value` of a Nullable<T>) counts
// its uses for the whole.
internal sealed class Operand
{
    private readonly Operand? whole;
    private readonly string text;
    private int uses;

    internal Operand(string text)
    {
        this.text = text;
    }

    private Operand(Operand whole, string text)
    {
        this.whole = whole;
        this.text = text;
    }

    internal int Uses => uses;

    // The text of the value, counted as one use.
    internal string Use()
    {
        if (whole is null)
        {
            uses++;
        }
        else
        {
            whole.Use();
        }

        return text;
    }

    internal Operand Part(string suffix) => new(whole ?? this, text + suffix);
}
