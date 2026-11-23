using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.Unicode;
using Great.Parts.Cel.Diagnostics;

namespace Great.Parts.Cel.Syntax;

// Reads the fields of one protobuf message from a span: the field number and the wire type, then the value of the field by its type.
// A truncated varint, a length beyond the end, a wire type that does not match a known field, a group and a string that is not
// UTF-8 stop the read with InvalidProtobuf and the offset in the bytes of the whole message. A nested message is a reader of its own
// over the bytes of that message.
internal ref struct ProtoReader
{
    internal const int Varint = 0;
    internal const int Fixed64 = 1;
    internal const int LengthDelimited = 2;
    private const int Fixed32 = 5;

    private readonly ReadOnlySpan<byte> _data;
    private readonly int _baseOffset;
    private int _position;

    internal ProtoReader(ReadOnlySpan<byte> data, int baseOffset = 0)
    {
        _data = data;
        _baseOffset = baseOffset;
        _position = 0;
    }

    // The offset in the whole message of the next byte.
    internal readonly int Offset => _baseOffset + _position;

    // Reads the number and the wire type of the next field; false at the end of the message.
    internal bool TryReadField(out int field, out int wireType)
    {
        if (_position >= _data.Length)
        {
            field = 0;
            wireType = 0;
            return false;
        }

        ulong tag = ReadVarint();
        wireType = (int)(tag & 7);
        ulong number = tag >> 3;
        if (number is 0 or > int.MaxValue)
        {
            throw Fail("a field has the number " + number.ToString(CultureInfo.InvariantCulture));
        }

        field = (int)number;
        return true;
    }

    internal ulong ReadVarint()
    {
        ulong result = 0;
        for (int shift = 0; shift < 70; shift += 7)
        {
            if (_position >= _data.Length)
            {
                throw Fail("a varint is cut short");
            }

            byte next = _data[_position++];
            if (shift == 63 && next > 1)
            {
                throw Fail("a varint is longer than 64 bits");
            }

            result |= (ulong)(next & 0x7F) << shift;
            if (next < 0x80)
            {
                return result;
            }
        }

        throw Fail("a varint is longer than 10 bytes");
    }

    // The value of a varint field: the wire type is checked first.
    internal long ReadInt64(int wireType)
    {
        Expect(wireType, Varint);
        return (long)ReadVarint();
    }

    internal int ReadInt32(int wireType) => (int)ReadInt64(wireType);

    internal bool ReadBool(int wireType) => ReadInt64(wireType) != 0;

    internal double ReadDouble(int wireType)
    {
        Expect(wireType, Fixed64);
        if (_data.Length - _position < 8)
        {
            throw Fail("a double is cut short");
        }

        long bits = 0;
        for (int i = 0; i < 8; i++)
        {
            bits |= (long)_data[_position + i] << (8 * i);
        }

        _position += 8;
        return BitConverter.Int64BitsToDouble(bits);
    }

    internal string ReadString(int wireType)
    {
        ReadOnlySpan<byte> bytes = ReadBytes(wireType, out int start);
        if (!Utf8.IsValid(bytes))
        {
            throw FailAt(start, "a string is not UTF-8");
        }

        return Encoding.UTF8.GetString(bytes);
    }

    internal ReadOnlySpan<byte> ReadBytes(int wireType) => ReadBytes(wireType, out _);

    // The reader of a nested message; the length is checked against the end of this message.
    internal ProtoReader ReadMessage(int wireType)
    {
        ReadOnlySpan<byte> bytes = ReadBytes(wireType, out int start);
        return new ProtoReader(bytes, start);
    }

    // The values of a repeated int32 field, in the packed form or one value of the unpacked form, added to the list.
    internal void ReadInt32s(int wireType, List<int> values)
    {
        if (wireType != LengthDelimited)
        {
            values.Add(ReadInt32(wireType));
            return;
        }

        ProtoReader packed = ReadMessage(wireType);
        while (packed._position < packed._data.Length)
        {
            values.Add((int)(long)packed.ReadVarint());
        }
    }

    // Skips the value of a field that the message does not know, by its wire type.
    internal void Skip(int wireType)
    {
        switch (wireType)
        {
            case Varint:
                _ = ReadVarint();
                break;
            case Fixed64:
                Advance(8);
                break;
            case LengthDelimited:
                _ = ReadBytes(wireType, out _);
                break;
            case Fixed32:
                Advance(4);
                break;
            default:
                throw Fail("a field has the wire type " + wireType.ToString(CultureInfo.InvariantCulture) + ", which a message cannot skip");
        }
    }

    // A known field must have the wire type of its type.
    internal readonly void Expect(int wireType, int expected)
    {
        if (wireType != expected)
        {
            throw Fail("a field has the wire type " + wireType.ToString(CultureInfo.InvariantCulture) + " where " + expected.ToString(CultureInfo.InvariantCulture) + " is expected");
        }
    }

    internal readonly CelException Fail(string message) => FailAt(Offset, message);

    private static CelException FailAt(int offset, string message) =>
        ProtoErrors.Stop(CelErrorCode.InvalidProtobuf, "invalid protobuf: " + message + " at byte " + offset.ToString(CultureInfo.InvariantCulture));

    private ReadOnlySpan<byte> ReadBytes(int wireType, out int start)
    {
        Expect(wireType, LengthDelimited);
        ulong length = ReadVarint();
        if (length > (ulong)(_data.Length - _position))
        {
            throw Fail("a length runs beyond the end of the message");
        }

        start = Offset;
        ReadOnlySpan<byte> bytes = _data.Slice(_position, (int)length);
        _position += (int)length;
        return bytes;
    }

    private void Advance(int count)
    {
        if (_data.Length - _position < count)
        {
            throw Fail("a fixed-size value is cut short");
        }

        _position += count;
    }
}
