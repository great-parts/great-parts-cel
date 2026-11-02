using System;
using System.Buffers;

namespace Great.Parts.Cel.Tests.Values;

// Memory over an array that is not the array itself, so a bytes value holds the memory manager.
internal sealed class ArrayMemoryManager(byte[] bytes) : MemoryManager<byte>
{
    public override Span<byte> GetSpan() => bytes;

    public override MemoryHandle Pin(int elementIndex = 0) => throw new NotSupportedException();

    public override void Unpin()
    {
    }

    protected override void Dispose(bool disposing)
    {
    }
}
