namespace Great.Parts.Cel.Eng.Checks;

/// <summary>One run of a repeat: a file and the span of the run in its text.</summary>
/// <param name="File">The index of the file in the C# files of the run.</param>
/// <param name="Start">The offset of the first character of the first token.</param>
/// <param name="End">The offset after the last character of the last token.</param>
internal readonly record struct DuplicateSite(int File, int Start, int End)
{
    /// <summary>Returns true when the two runs are in one file and share at least one character.</summary>
    /// <param name="other">The run to compare with.</param>
    internal bool Overlaps(DuplicateSite other) => File == other.File && Start < other.End && other.Start < End;
}

/// <summary>Two runs of equal tokens. The first run is the earlier one by file path and position.</summary>
/// <param name="First">The earlier run.</param>
/// <param name="Second">The later run.</param>
/// <param name="Tokens">The number of tokens of each run.</param>
internal readonly record struct RawDuplicate(DuplicateSite First, DuplicateSite Second, int Tokens);

/// <summary>A pair of repeated runs with its places in the files, as the ratchet names them.</summary>
/// <param name="FirstPath">The path of the file of the first place, relative to the root, with <c>/</c>.</param>
/// <param name="First">The line and column of the first place.</param>
/// <param name="SecondPath">The path of the file of the second place.</param>
/// <param name="Second">The line and column of the second place.</param>
/// <param name="Tokens">The number of tokens of each run.</param>
internal readonly record struct DuplicatePair(string FirstPath, Position First, string SecondPath, Position Second, int Tokens);
