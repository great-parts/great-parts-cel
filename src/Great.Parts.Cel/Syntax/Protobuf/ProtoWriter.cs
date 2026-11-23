using System;
using System.Collections.Generic;
using System.Text;

namespace Great.Parts.Cel.Syntax;

// Writes protobuf messages without reflection and without a package. A write runs the same code three times over the same writer
// type (Encode): the first two only measure, the last fills the array. The measuring run records the size of each nested message in
// the order in which the messages begin, so the writing run, which begins the same messages in the same order, writes each length
// before the content and the array is allocated once. Fields are written in the order of their numbers and proto3 defaults of a
// field outside a oneof are left out, as protobuf writers do, so the bytes are what Google.Protobuf writes for the same message.
internal sealed class ProtoWriter
{
    private const int LengthDelimited = 2;
    private const int Fixed64 = 1;

    private readonly byte[]? _buffer;
    private readonly List<int> _sizes;
    private int _position;

    // The index of the next nested message, in the order in which the messages begin.
    private int _next;

    private ProtoWriter(byte[]? buffer, List<int> sizes)
    {
        _buffer = buffer;
        _sizes = sizes;
    }

    /// <summary>
    /// Runs a write three times and returns the bytes of the last run. The write must create the same messages in the same order every time.
    /// </summary>
    /// <param name="write">The write of the message.</param>
    /// <returns>The bytes.</returns>
    internal static byte[] Encode(Action<ProtoWriter> write)
    {
        // The first run lets the write collect what a later part of the message needs earlier (the positions of the nodes that the
        // macro calls hold, the ids of the arguments of decimal constants); its sizes are not used.
        write(new ProtoWriter(null, []));
        var measuring = new ProtoWriter(null, []);
        write(measuring);
        var writing = new ProtoWriter(new byte[measuring._position], measuring._sizes);
        write(writing);
        return writing._buffer!;
    }

    internal static int VarintSize(ulong value)
    {
        int size = 1;
        while (value >= 0x80)
        {
            value >>= 7;
            size++;
        }

        return size;
    }

    // A nested message: the tag, then the length when writing; the content follows. End finishes it.
    internal int Begin(int field, out int start)
    {
        Tag(field, LengthDelimited);
        int index = _next++;
        if (_buffer is null)
        {
            _sizes.Add(0);
        }
        else
        {
            Varint((ulong)_sizes[index]);
        }

        start = _position;
        return index;
    }

    internal void End(int index, int start)
    {
        if (_buffer is not null)
        {
            return;
        }

        int size = _position - start;
        _sizes[index] = size;
        _position += VarintSize((ulong)size);
    }

    // A varint field of a message, left out when it is 0.
    internal void Int64(int field, long value)
    {
        if (value != 0)
        {
            Int64Always(field, value);
        }
    }

    // A varint field that is written whatever its value: a member of a oneof, a key of a map entry.
    internal void Int64Always(int field, long value)
    {
        Tag(field, 0);
        Varint((ulong)value);
    }

    internal void UInt64Always(int field, ulong value)
    {
        Tag(field, 0);
        Varint(value);
    }

    internal void Bool(int field, bool value)
    {
        if (value)
        {
            BoolAlways(field, true);
        }
    }

    internal void BoolAlways(int field, bool value)
    {
        Tag(field, 0);
        Put(value ? (byte)1 : (byte)0);
    }

    internal void DoubleAlways(int field, double value)
    {
        Tag(field, Fixed64);
        ulong bits = (ulong)BitConverter.DoubleToInt64Bits(value);
        for (int i = 0; i < 8; i++)
        {
            Put((byte)(bits >> (8 * i)));
        }
    }

    // A string field of a message, left out when it is empty.
    internal void String(int field, string? value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            StringAlways(field, value);
        }
    }

    // A string field that is written whatever its value: a member of a oneof, an element of a repeated field.
    internal void StringAlways(int field, string value)
    {
        Tag(field, LengthDelimited);
        int count = Encoding.UTF8.GetByteCount(value);
        Varint((ulong)count);
        if (_buffer is null)
        {
            _position += count;
            return;
        }

        _ = Encoding.UTF8.GetBytes(value, _buffer.AsSpan(_position, count));
        _position += count;
    }

    internal void BytesAlways(int field, ReadOnlySpan<byte> value)
    {
        Tag(field, LengthDelimited);
        Varint((ulong)value.Length);
        if (_buffer is null)
        {
            _position += value.Length;
            return;
        }

        value.CopyTo(_buffer.AsSpan(_position));
        _position += value.Length;
    }

    // A repeated int32 field in the packed form that proto3 uses; nothing for no values.
    internal void PackedInt32(int field, ReadOnlySpan<int> values)
    {
        if (values.IsEmpty)
        {
            return;
        }

        int size = 0;
        foreach (int value in values)
        {
            size += VarintSize((ulong)(long)value);
        }

        Tag(field, LengthDelimited);
        Varint((ulong)size);
        foreach (int value in values)
        {
            Varint((ulong)(long)value);
        }
    }

    private void Tag(int field, int wireType) => Varint((ulong)((field << 3) | wireType));

    private void Varint(ulong value)
    {
        while (value >= 0x80)
        {
            Put((byte)(value | 0x80));
            value >>= 7;
        }

        Put((byte)value);
    }

    private void Put(byte value)
    {
        if (_buffer is not null)
        {
            _buffer[_position] = value;
        }

        _position++;
    }
}
