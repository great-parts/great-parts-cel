using System;

namespace Great.Parts.Cel.Filtering;

/// <summary>
/// Where the parts of a path are in the filter: the parts of the field written there, or one range for a path of the options, whose
/// parts have no place of their own.
/// </summary>
internal readonly struct PathPlace
{
    private readonly FilterMember _member;
    private readonly bool _isWritten;

    // The index of the part of the member that the first part of the path stands for: a path of the elements of a list starts at the
    // part of the list.
    private readonly int _offset;

    internal PathPlace(FilterMember member, bool isWritten, int offset = 0)
    {
        _member = member;
        _isWritten = isWritten;
        _offset = offset;
    }

    /// <summary>The range of the whole field.</summary>
    internal Range Whole => _member.Start.._member.End;

    /// <summary>The range of a part of a field written in the filter; a path of the options has the range of the whole value.</summary>
    internal Range Part(int index) =>
        _isWritten ? _member.Segments[_offset + index].Start.._member.Segments[_offset + index].End : Whole;

    /// <summary>Whether a part of a field was written as a string in quotes.</summary>
    internal bool IsQuoted(int index) => _isWritten && _member.Segments[_offset + index].Quoted is not null;

    /// <summary>The place of the path that starts at the part of a list.</summary>
    internal PathPlace Element(int listIndex) => new(_member, _isWritten, _offset + listIndex);
}
