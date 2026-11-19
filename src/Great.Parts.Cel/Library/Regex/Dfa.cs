using System;
using System.Buffers;
using Great.Parts.Cel.Library.Text;

namespace Great.Parts.Cel.Library.Regex;

// How the automaton goes on from the state where no thread of the match is alive, to the next place where one may begin.
internal enum DfaSkip
{
    // Reads each code point.
    None,

    // The literal prefix that every match begins with, of at least two code units, found by SubstringSearch.
    Prefix,

    // The code units that can leave the state, at most three or all ASCII, found by a search of SearchValues.
    Units,
}

// The deterministic automaton of a program, built once, when a pattern that is a literal is prepared (DfaBuilder), for a program
// whose only conditions of zero width are the beginning and the end of the text. It answers the question that Matcher answers — does
// some part of the text match — by one table lookup for each code point, so in time linear in the text with a small constant. The
// tables never change after the build, so any number of threads read them at once.
//
// A state is a set of instructions of the program, entered by reading a code point; the unanchored search of the engine is in the
// states: the closure of the first instruction is in every one. Row 0 of the table is the state that no text can leave with a match,
// row 1 is the state that has matched; a row holds, for each class of code points, the offset of the next row, so a step is
// table[offset + class]. The end of the text is not a code point: a row has its own answer for it. A program that begins with \A
// has no state of no live thread, so the automaton reaches row 0 and stops after the first position that cannot go on.
internal sealed class Dfa
{
    // The offset of the state that cannot match; the offset of the one that has matched is the stride.
    private const int DeadOffset = 0;

    private readonly ushort[] table;
    private readonly bool[] endAccepts;
    private readonly CodePointClasses classes;
    private readonly int stride;
    private readonly int initial;
    private readonly int restOffset;
    private readonly char[]? prefix;
    private readonly SearchValues<char>? units;
    private readonly int unitCount;

    // The row of the state of no live thread is restRow, below 2 when the program has none; the skip is a prefix, or units, or none.
    internal Dfa(ushort[] table, bool[] endAccepts, CodePointClasses classes, int initialRow, int restRow, char[]? prefix, SearchValues<char>? units, int unitCount)
    {
        this.table = table;
        this.endAccepts = endAccepts;
        this.classes = classes;
        stride = classes.Count;
        initial = initialRow * stride;
        this.prefix = prefix;
        this.units = units;
        this.unitCount = unitCount;
        restOffset = restRow < 2 || (prefix is null && units is null) ? -1 : restRow * stride;
    }

    // The number of states that can still change the answer; the two rows of the answers are not counted.
    internal int StateCount => (table.Length / stride) - 2;

    // The number of classes of code points, the columns of the table.
    internal int ClassCount => stride;

    // What the tables hold, in bytes: the transitions, the answers at the end of the text, and the classes (their table of the code
    // points below 128, the starts of the intervals and their class numbers). The weight of the entry of the cache of patterns counts
    // it in units of 16 bytes.
    internal int TableBytes => (table.Length * sizeof(ushort)) + endAccepts.Length + classes.TableBytes;

    // How the search for a match goes on from the state of no live thread.
    internal DfaSkip Skip => prefix is not null ? DfaSkip.Prefix : units is not null ? DfaSkip.Units : DfaSkip.None;

    // The literal prefix that every match begins with, when Skip is Prefix; empty otherwise.
    internal ReadOnlySpan<char> Prefix => prefix;

    // The number of code units that leave the state of no live thread, when Skip is Units; 0 otherwise.
    internal int UnitCount => unitCount;

    // Whether some part of the text matches.
    internal bool IsMatch(ReadOnlySpan<char> text)
    {
        int offset = initial;
        int index = 0;
        while (offset > stride)
        {
            if (offset == restOffset)
            {
                // No thread is alive: only a place where a match may begin changes the state, so go to the next one.
                int distance = SkipFrom(text, index);
                if (distance < 0)
                {
                    return endAccepts[offset / stride];
                }

                index += distance;
            }

            if (index >= text.Length)
            {
                return endAccepts[offset / stride];
            }

            offset = table[offset + ClassAt(text, ref index)];
        }

        return offset > DeadOffset;
    }

    // The class of the code point at an index of the text, and the index after it. As in Matcher: a pair of surrogates is one code
    // point, a lone surrogate is U+FFFD.
    private int ClassAt(ReadOnlySpan<char> text, ref int index)
    {
        char unit = text[index];
        if (unit < 0xD800 || unit > 0xDFFF)
        {
            index++;
            return classes.OfUnit(unit);
        }

        int codePoint = Matcher.Read(text, index, out int width);
        index += width;
        return classes.Of(codePoint);
    }

    // The distance from an index to the next place where a match may begin, or -1 when there is none in the rest of the text. A
    // prefix is every place where it occurs, and the units are every place where one of them stands; no other place can begin a
    // match, since the state leaves only by the first code point of the prefix or by one of the units. The prefix is at most the length
    // that SubstringSearch keeps on the stack, and each search is followed by a read of the whole prefix or of a unit, so the
    // searches cost no more than the text, whatever the number of the places that they find.
    private int SkipFrom(ReadOnlySpan<char> text, int index)
    {
        ReadOnlySpan<char> rest = text[index..];
        if (prefix is not null)
        {
            long ignored = 0;
            return SubstringSearch.IndexOf(rest, prefix, ref ignored);
        }

        return rest.IndexOfAny(units!);
    }
}
