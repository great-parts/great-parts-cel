using System;
using System.Buffers;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Library.Extensions.Strings;

// The builder of a string result whose length is known only as it is built. Before it appends l code units to a result of length T it
// charges ⌈(T + l)/10⌉ − ⌈T/10⌉ to the meter, so the charges add up to ⌈Lᵣ/10⌉ for the length Lᵣ of the result, and a cost limit that
// is exceeded stops the build before the memory is taken. The buffer is an array rented from the shared pool and returned by
// Dispose, so a call that ends — also by the exception of an exceeded limit — gives it back. The first rental is not charged.
// A result that is first built in an order and then rearranged, as the pairs of a map are, charges the units of its pieces as they
// are appended and the rest with Charge, and writes the rearranged text with WriteCharged: the units charged stay those of the
// length of the final result.
internal ref struct ResultBuilder
{
    private readonly IEvaluationMeter? _meter;
    private char[]? _buffer;
    private int _length;

    // The units that the meter has been charged for, from which the next charge is taken; equal to the length unless a caller
    // charged ahead or removed text.
    private long _charged;

    internal ResultBuilder(IEvaluationMeter? meter)
    {
        _meter = meter;
        _buffer = null;
        _length = 0;
        _charged = 0;
    }

    // The code units written so far.
    internal readonly int Length => _length;

    // The text written so far.
    internal readonly ReadOnlySpan<char> Written => _buffer is null ? default : _buffer.AsSpan(0, _length);

    // Appends the units; they are charged first.
    internal void Append(scoped ReadOnlySpan<char> units)
    {
        if (units.IsEmpty)
        {
            return;
        }

        Charge(units.Length);
        char[] buffer = EnsureCapacity((long)_length + units.Length);
        units.CopyTo(buffer.AsSpan(_length));
        _length += units.Length;
    }

    // Charges for l units that are not written yet, the units of the result that WriteCharged will give room for.
    internal void Charge(long units)
    {
        _meter?.Charge(Costs.Tenths((ulong)(_charged + units)) - Costs.Tenths((ulong)_charged));
        _charged += units;
    }

    // Makes room for l units that were charged already and returns them to be filled in; l is more than 0.
    internal Span<char> WriteCharged(int units)
    {
        char[] buffer = EnsureCapacity((long)_length + units);
        Span<char> room = buffer.AsSpan(_length, units);
        _length += units;
        return room;
    }

    // Removes the units from start up to start + count; the text after them moves down. What was charged stays charged.
    internal void Remove(int start, int count)
    {
        char[] buffer = _buffer!;
        buffer.AsSpan(start + count, _length - start - count).CopyTo(buffer.AsSpan(start));
        _length -= count;
    }

    // The result as a string; the builder stays usable.
    internal readonly string ToResultString() => _length == 0 ? string.Empty : new string(_buffer.AsSpan(0, _length));

    // Gives the buffer back to the pool.
    internal void Dispose()
    {
        if (_buffer is not null)
        {
            ArrayPool<char>.Shared.Return(_buffer);
            _buffer = null;
        }
    }

    // The buffer with room for a total length.
    private char[] EnsureCapacity(long total)
    {
        if (_buffer is null)
        {
            _buffer = ArrayPool<char>.Shared.Rent(Capacity(Math.Max(total, 256)));
        }
        else if (total > _buffer.Length)
        {
            char[] larger = ArrayPool<char>.Shared.Rent(Capacity(Math.Max(total, (long)_buffer.Length * 2)));
            _buffer.AsSpan(0, _length).CopyTo(larger);
            ArrayPool<char>.Shared.Return(_buffer);
            _buffer = larger;
        }

        return _buffer;
    }

    // The capacity to rent for a length. A length beyond the largest array of .NET asks for the largest int: the runtime then refuses
    // the rental with OutOfMemoryException, as it refuses any allocation it cannot make.
    private static int Capacity(long length) => length > Array.MaxLength ? int.MaxValue : (int)length;
}
