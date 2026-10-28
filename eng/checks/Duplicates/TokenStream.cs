using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Great.Parts.Cel.Eng.Checks;

/// <summary>
/// The tokens of the C# files under one symbol set, one sequence for all the files: the tokens of the active code without
/// trivia, so comments, white space, preprocessor directives and inactive branches are not in it. A token is its kind: every
/// identifier is the identifier kind and every literal is its literal kind, while keywords and punctuation have kinds of their
/// own. A window of the search never crosses a file boundary, see <see cref="FileOf"/>.
/// </summary>
internal sealed class TokenStream
{
    private readonly ushort[] _kinds;
    private readonly int[] _starts;
    private readonly int[] _ends;
    private readonly int[] _files;

    private TokenStream(ushort[] kinds, int[] starts, int[] ends, int[] files)
    {
        _kinds = kinds;
        _starts = starts;
        _ends = ends;
        _files = files;
    }

    /// <summary>The number of tokens of all the files.</summary>
    internal int Count => _kinds.Length;

    /// <summary>Builds the stream of the files under the symbol set at the index.</summary>
    /// <param name="files">The C# files that are not generated, in the order of their paths.</param>
    /// <param name="setIndex">The index of the symbol set: the parse of each file under it is read.</param>
    internal static TokenStream Build(IReadOnlyList<CSharpFile> files, int setIndex)
    {
        List<ushort> kinds = [];
        List<int> starts = [];
        List<int> ends = [];
        List<int> owners = [];
        for (int file = 0; file < files.Count; file++)
        {
            foreach (SyntaxToken token in files[file].Parses[setIndex].Tree.GetRoot().DescendantTokens())
            {
                if (token.IsMissing || token.IsKind(SyntaxKind.EndOfFileToken))
                {
                    continue;
                }

                kinds.Add((ushort)token.RawKind);
                starts.Add(token.SpanStart);
                ends.Add(token.Span.End);
                owners.Add(file);
            }
        }

        return new TokenStream([.. kinds], [.. starts], [.. ends], [.. owners]);
    }

    /// <summary>Returns the kind of the token.</summary>
    /// <param name="index">The index in the stream.</param>
    internal ushort KindAt(int index) => _kinds[index];

    /// <summary>Returns the index of the file of the token.</summary>
    /// <param name="index">The index in the stream.</param>
    internal int FileOf(int index) => _files[index];

    /// <summary>Returns the offset of the first character of the token in the text of its file.</summary>
    /// <param name="index">The index in the stream.</param>
    internal int StartOf(int index) => _starts[index];

    /// <summary>Returns the offset after the last character of the token in the text of its file.</summary>
    /// <param name="index">The index in the stream.</param>
    internal int EndOf(int index) => _ends[index];

    /// <summary>Returns true when the stream has the same tokens in the same places as the other one.</summary>
    /// <param name="other">The stream to compare with.</param>
    internal bool SameAs(TokenStream other) =>
        _kinds.AsSpan().SequenceEqual(other._kinds)
        && _starts.AsSpan().SequenceEqual(other._starts)
        && _files.AsSpan().SequenceEqual(other._files);

    /// <summary>Returns true when the <paramref name="length"/> tokens from the two indexes have the same kinds.</summary>
    /// <param name="first">The index of the first token of the first run.</param>
    /// <param name="second">The index of the first token of the second run.</param>
    /// <param name="length">The number of tokens to compare.</param>
    internal bool Equal(int first, int second, int length) =>
        _kinds.AsSpan(first, length).SequenceEqual(_kinds.AsSpan(second, length));
}
